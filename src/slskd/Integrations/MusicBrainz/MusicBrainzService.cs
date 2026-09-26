// <copyright file="MusicBrainzService.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Integrations.MusicBrainz
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using static slskd.Options.IntegrationsOptions;

    /// <summary>
    ///     Looks up releases on MusicBrainz.
    /// </summary>
    public interface IMusicBrainzService
    {
        /// <summary>
        ///     Gets a value indicating whether lookups are enabled.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        ///     Searches for releases.
        /// </summary>
        /// <param name="query">A MusicBrainz search query; plain text, or Lucene syntax such as <c>artist:"x" AND release:"y"</c>.</param>
        /// <param name="limit">The maximum number of releases to return, from 1 to 100.</param>
        /// <param name="offset">The number of releases to skip.</param>
        /// <param name="dismax">Whether to search the query as plain text across titles, artists and labels, rather than as Lucene syntax.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the search result.</returns>
        Task<MusicBrainzReleaseSearchResult> SearchReleasesAsync(string query, int limit = 25, int offset = 0, bool dismax = false, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets a release and its track list.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the release, or null if MusicBrainz has no release with the ID.</returns>
        Task<MusicBrainzRelease> GetReleaseAsync(Guid id, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Looks up releases on MusicBrainz.
    /// </summary>
    public sealed class MusicBrainzService : IMusicBrainzService, IDisposable
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromHours(1);
        private static readonly TimeSpan ReleaseCacheDuration = TimeSpan.FromDays(1);

        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicBrainzService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="optionsMonitor">The options monitor.</param>
        /// <param name="log">The logger.</param>
        public MusicBrainzService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<slskd.Options> optionsMonitor,
            ILogger<MusicBrainzService> log)
        {
            HttpClientFactory = httpClientFactory;
            OptionsMonitor = optionsMonitor;
            Log = log;
        }

        /// <summary>
        ///     Gets a value indicating whether lookups are enabled.
        /// </summary>
        public bool Enabled => !MusicBrainzOptions.Disabled;

        private IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());
        private IHttpClientFactory HttpClientFactory { get; }
        private DateTime LastRequest { get; set; } = DateTime.MinValue;
        private ILogger<MusicBrainzService> Log { get; }
        private MusicBrainzOptions MusicBrainzOptions => OptionsMonitor.CurrentValue.Integrations.MusicBrainz;
        private IOptionsMonitor<slskd.Options> OptionsMonitor { get; }
        private SemaphoreSlim RequestGate { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        ///     Searches for releases.
        /// </summary>
        /// <param name="query">A MusicBrainz search query; plain text, or Lucene syntax such as <c>artist:"x" AND release:"y"</c>.</param>
        /// <param name="limit">The maximum number of releases to return, from 1 to 100.</param>
        /// <param name="offset">The number of releases to skip.</param>
        /// <param name="dismax">Whether to search the query as plain text across titles, artists and labels, rather than as Lucene syntax.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the search result.</returns>
        public async Task<MusicBrainzReleaseSearchResult> SearchReleasesAsync(string query, int limit = 25, int offset = 0, bool dismax = false, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("A query is required", nameof(query));
            }

            limit = Math.Clamp(limit, 1, 100);
            offset = Math.Max(offset, 0);

            var path = $"ws/2/release?query={Uri.EscapeDataString(query.Trim())}&limit={limit}&offset={offset}{(dismax ? "&dismax=true" : string.Empty)}&fmt=json";

            var node = await GetCachedAsync($"search:{path}", path, SearchCacheDuration, cancellationToken);
            return MusicBrainzParser.ParseReleaseSearch(node);
        }

        /// <summary>
        ///     Gets a release and its track list.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the release, or null if MusicBrainz has no release with the ID.</returns>
        public async Task<MusicBrainzRelease> GetReleaseAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var path = $"ws/2/release/{id}?inc=recordings+artist-credits+labels+release-groups&fmt=json";

            var node = await GetCachedAsync($"release:{id}", path, ReleaseCacheDuration, cancellationToken);
            return node is null ? null : MusicBrainzParser.ParseRelease(node);
        }

        /// <summary>
        ///     Releases the cache and the request gate.
        /// </summary>
        public void Dispose()
        {
            (Cache as IDisposable)?.Dispose();
            RequestGate.Dispose();
        }

        private static JsonNode TryParse(string body)
        {
            try
            {
                return JsonNode.Parse(body);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private async Task<JsonNode> GetCachedAsync(string key, string path, TimeSpan duration, CancellationToken cancellationToken)
        {
            if (!Enabled)
            {
                throw new InvalidOperationException("MusicBrainz lookups are disabled");
            }

            var baseUrl = MusicBrainzOptions.Url.TrimEnd('/');
            key = $"{baseUrl}|{key}";

            if (Cache.TryGetValue(key, out JsonNode cached))
            {
                return cached;
            }

            var node = await GetAsync(new Uri($"{baseUrl}/{path}"), cancellationToken);

            // cache misses too, so a bad ID doesn't cost a request every time
            Cache.Set(key, node, duration);
            return node;
        }

        private async Task<JsonNode> GetAsync(Uri uri, CancellationToken cancellationToken)
        {
            // musicbrainz.org answers 503 when it is busy or its rate limit is exceeded; back off and try twice more
            for (var attempt = 1; ; attempt++)
            {
                using var response = await SendAsync(uri, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < 3)
                {
                    Log.LogDebug("MusicBrainz rate limited a request to {Uri}; retrying", uri);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var message = TryParse(body)?["error"]?.GetValue<string>();
                    throw new MusicBrainzException($"MusicBrainz returned {(int)response.StatusCode} {response.ReasonPhrase}{(message is null ? string.Empty : $": {message}")}");
                }

                return TryParse(body) ?? throw new MusicBrainzException("MusicBrainz returned a response that is not JSON");
            }
        }

        private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
        {
            await RequestGate.WaitAsync(cancellationToken);

            try
            {
                var wait = LastRequest.AddMilliseconds(MusicBrainzOptions.RequestInterval) - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken);
                }

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RequestTimeout);

                using var request = new HttpRequestMessage(HttpMethod.Get, uri);

                // MusicBrainz asks every client to identify itself this way
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue(Program.AppName, Program.SemanticVersion));
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("(+https://slskd.org)"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Log.LogDebug("Requesting {Uri} from MusicBrainz", uri);

                try
                {
                    return await HttpClientFactory.CreateClient().SendAsync(request, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new MusicBrainzException($"MusicBrainz did not respond within {RequestTimeout.TotalSeconds} seconds");
                }
                catch (HttpRequestException ex)
                {
                    throw new MusicBrainzException($"Failed to reach MusicBrainz: {ex.Message}", ex);
                }
            }
            finally
            {
                LastRequest = DateTime.UtcNow;
                RequestGate.Release();
            }
        }
    }
}
