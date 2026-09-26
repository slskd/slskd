// <copyright file="CoverArtService.cs" company="JP Dillingham">
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
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using static slskd.Options.IntegrationsOptions;

    /// <summary>
    ///     Finds cover art for releases.
    /// </summary>
    public interface ICoverArtService
    {
        /// <summary>
        ///     Gets the front cover of a release.
        /// </summary>
        /// <param name="releaseId">The release MBID.</param>
        /// <param name="releaseGroupId">The release group MBID, if known; its cover is used when the release has none.</param>
        /// <param name="artist">The release artist, used to look the cover up elsewhere.</param>
        /// <param name="title">The release title, used to look the cover up elsewhere.</param>
        /// <param name="size">The approximate width wanted: 250, 500 or 1200.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the cover, or null if none was found.</returns>
        Task<CoverImage> GetCoverAsync(Guid releaseId, Guid? releaseGroupId, string artist, string title, int size, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Finds cover art for releases: first on the Cover Art Archive, which is where MusicBrainz keeps covers, and then,
    ///     if enabled, on Deezer and iTunes by artist and title.
    /// </summary>
    public sealed class CoverArtService : ICoverArtService, IDisposable
    {
        private const int MaximumImageBytes = 5 * 1024 * 1024;

        private static readonly TimeSpan ArchiveTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan FoundCacheDuration = TimeSpan.FromDays(1);
        private static readonly TimeSpan MissingCacheDuration = TimeSpan.FromHours(1);

        // the archive redirects to archive.org, which some networks can't reach; after a timeout it is skipped for a while
        // so that every cover doesn't wait for it
        private static readonly TimeSpan ArchiveBackoff = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     Initializes a new instance of the <see cref="CoverArtService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="optionsMonitor">The options monitor.</param>
        /// <param name="log">The logger.</param>
        public CoverArtService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<slskd.Options> optionsMonitor,
            ILogger<CoverArtService> log)
        {
            HttpClientFactory = httpClientFactory;
            OptionsMonitor = optionsMonitor;
            Log = log;
        }

        private DateTime ArchiveSkippedUntil { get; set; } = DateTime.MinValue;
        private IMemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 * 1024 * 1024 });
        private IHttpClientFactory HttpClientFactory { get; }
        private ILogger<CoverArtService> Log { get; }
        private MusicBrainzOptions MusicBrainzOptions => OptionsMonitor.CurrentValue.Integrations.MusicBrainz;
        private IOptionsMonitor<slskd.Options> OptionsMonitor { get; }
        private SemaphoreSlim Concurrency { get; } = new SemaphoreSlim(4, 4);

        /// <summary>
        ///     Reduces a name to lowercase words without accents, punctuation or a bracketed edition such as "(Remastered)",
        ///     for comparing names from different catalogues.
        /// </summary>
        /// <param name="text">The name.</param>
        /// <returns>The reduced name.</returns>
        public static string Simplify(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var withoutEdition = Regex.Replace(text, @"\s*[\(\[][^\)\]]*[\)\]]", " ");
            var builder = new StringBuilder();

            foreach (var character in withoutEdition.Replace("&", " and ").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
            }

            return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>
        ///     Decides whether an album found elsewhere is the release: the artists must match, and one title must start
        ///     with the other, ignoring editions.
        /// </summary>
        /// <param name="foundArtist">The artist of the album found.</param>
        /// <param name="foundTitle">The title of the album found.</param>
        /// <param name="artist">The release artist.</param>
        /// <param name="title">The release title.</param>
        /// <returns>A value indicating whether the album is the release.</returns>
        public static bool IsSameAlbum(string foundArtist, string foundTitle, string artist, string title)
        {
            var (a, b) = (Simplify(foundArtist), Simplify(artist));
            var (x, y) = (Simplify(foundTitle), Simplify(title));

            if (a.Length == 0 || b.Length == 0 || x.Length == 0 || y.Length == 0)
            {
                return false;
            }

            var sameArtist = a == b || a.StartsWith(b + " ", StringComparison.Ordinal) || b.StartsWith(a + " ", StringComparison.Ordinal);
            var sameTitle = x == y || x.StartsWith(y + " ", StringComparison.Ordinal) || y.StartsWith(x + " ", StringComparison.Ordinal);

            return sameArtist && sameTitle;
        }

        /// <summary>
        ///     Gets the front cover of a release.
        /// </summary>
        /// <param name="releaseId">The release MBID.</param>
        /// <param name="releaseGroupId">The release group MBID, if known; its cover is used when the release has none.</param>
        /// <param name="artist">The release artist, used to look the cover up elsewhere.</param>
        /// <param name="title">The release title, used to look the cover up elsewhere.</param>
        /// <param name="size">The approximate width wanted: 250, 500 or 1200.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the cover, or null if none was found.</returns>
        public async Task<CoverImage> GetCoverAsync(Guid releaseId, Guid? releaseGroupId, string artist, string title, int size, CancellationToken cancellationToken = default)
        {
            if (MusicBrainzOptions.Disabled)
            {
                throw new InvalidOperationException("MusicBrainz lookups are disabled");
            }

            size = size <= 250 ? 250 : size <= 500 ? 500 : 1200;

            var key = $"release:{releaseId}:{size}";

            if (Cache.TryGetValue(key, out CoverImage cached))
            {
                return cached;
            }

            await Concurrency.WaitAsync(cancellationToken);

            try
            {
                if (Cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                var cover = await FromArchiveAsync($"release/{releaseId}", size, cancellationToken);

                if (cover is null && releaseGroupId is not null)
                {
                    cover = await FromArchiveAsync($"release-group/{releaseGroupId}", size, cancellationToken);
                }

                if (cover is null && MusicBrainzOptions.CoverFallback && !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
                {
                    // the same album has many releases; look it up once for all of them
                    var albumKey = $"album:{Simplify(artist)}|{Simplify(title)}:{size}";

                    if (!Cache.TryGetValue(albumKey, out cover))
                    {
                        cover = await FromDeezerAsync(artist, title, size, cancellationToken)
                            ?? await FromITunesAsync(artist, title, size, cancellationToken);

                        Remember(albumKey, cover);
                    }
                }

                Remember(key, cover);
                return cover;
            }
            finally
            {
                Concurrency.Release();
            }
        }

        /// <summary>
        ///     Releases the cache and the concurrency limiter.
        /// </summary>
        public void Dispose()
        {
            (Cache as IDisposable)?.Dispose();
            Concurrency.Dispose();
        }

        private static string Text(JsonNode node)
            => node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

        private void Remember(string key, CoverImage cover)
        {
            Cache.Set(key, cover, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cover is null ? MissingCacheDuration : FoundCacheDuration,
                Size = Math.Max(cover?.Data.Length ?? 0, 1),
            });
        }

        private async Task<CoverImage> FromArchiveAsync(string path, int size, CancellationToken cancellationToken)
        {
            if (DateTime.UtcNow < ArchiveSkippedUntil)
            {
                return null;
            }

            try
            {
                return await DownloadAsync(new Uri($"https://coverartarchive.org/{path}/front-{size}"), "Cover Art Archive", ArchiveTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                Log.LogInformation("The Cover Art Archive did not respond within {Timeout} seconds; skipping it for {Backoff} minutes", ArchiveTimeout.TotalSeconds, ArchiveBackoff.TotalMinutes);
                ArchiveSkippedUntil = DateTime.UtcNow.Add(ArchiveBackoff);
                return null;
            }
        }

        private async Task<CoverImage> FromDeezerAsync(string artist, string title, int size, CancellationToken cancellationToken)
        {
            var query = Uri.EscapeDataString($"artist:\"{artist}\" album:\"{title}\"");
            var found = await SearchAsync(new Uri($"https://api.deezer.com/search/album?q={query}&limit=10"), cancellationToken);

            var album = (found?["data"] as JsonArray)?
                .FirstOrDefault(item => IsSameAlbum(Text(item?["artist"]?["name"]), Text(item?["title"]), artist, title));

            var image = Text(album?[size <= 250 ? "cover_medium" : size <= 500 ? "cover_big" : "cover_xl"]);
            return image is null ? null : await TryDownloadAsync(new Uri(image), "Deezer", cancellationToken);
        }

        private async Task<CoverImage> FromITunesAsync(string artist, string title, int size, CancellationToken cancellationToken)
        {
            var term = Uri.EscapeDataString($"{artist} {title}");
            var found = await SearchAsync(new Uri($"https://itunes.apple.com/search?term={term}&entity=album&limit=10"), cancellationToken);

            var album = (found?["results"] as JsonArray)?
                .FirstOrDefault(item => IsSameAlbum(Text(item?["artistName"]), Text(item?["collectionName"]), artist, title));

            // artwork URLs name their size, and any size can be asked for
            var image = Text(album?["artworkUrl100"])?.Replace("100x100bb", $"{size}x{size}bb", StringComparison.Ordinal);
            return image is null ? null : await TryDownloadAsync(new Uri(image), "iTunes", cancellationToken);
        }

        private async Task<JsonNode> SearchAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await SendAsync(uri, SearchTimeout, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Log.LogDebug("Cover search {Uri} returned {Status}", uri, (int)response.StatusCode);
                    return null;
                }

                return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex) when (ex is TimeoutException or HttpRequestException or System.Text.Json.JsonException)
            {
                Log.LogDebug("Cover search {Uri} failed: {Message}", uri, ex.Message);
                return null;
            }
        }

        private async Task<CoverImage> TryDownloadAsync(Uri uri, string source, CancellationToken cancellationToken)
        {
            try
            {
                return await DownloadAsync(uri, source, SearchTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        private async Task<CoverImage> DownloadAsync(Uri uri, string source, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await SendAsync(uri, timeout, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound || !response.IsSuccessStatusCode)
                {
                    return null;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    || response.Content.Headers.ContentLength > MaximumImageBytes)
                {
                    return null;
                }

                var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                return data.Length is 0 or > MaximumImageBytes ? null : new CoverImage(data, contentType, source);
            }
            catch (HttpRequestException ex)
            {
                Log.LogDebug("Failed to download cover {Uri}: {Message}", uri, ex.Message);
                return null;
            }
        }

        private async Task<HttpResponseMessage> SendAsync(Uri uri, TimeSpan timeout, CancellationToken cancellationToken)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(Program.AppName, Program.SemanticVersion));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("(+https://slskd.org)"));

            try
            {
                // read the whole body within the timeout, so that a stalled download times out too
                return await HttpClientFactory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"{uri.Host} did not respond within {timeout.TotalSeconds} seconds");
            }
        }
    }
}
