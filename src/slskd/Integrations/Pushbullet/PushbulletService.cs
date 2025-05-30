﻿// <copyright file="PushbulletService.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Integrations.Pushbullet
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using static slskd.Options.IntegrationOptions;

    /// <summary>
    ///     Pushbullet integration service.
    /// </summary>
    public class PushbulletService : IPushbulletService
    {
        private static readonly string PushUri = "https://api.pushbullet.com/v2/pushes";

        /// <summary>
        ///     Initializes a new instance of the <see cref="PushbulletService"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HttpClientFactory to use.</param>
        /// <param name="optionsMonitor">The options monitor used to derive application options.</param>
        /// <param name="log">The logger.</param>
        public PushbulletService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<Options> optionsMonitor,
            ILogger<PushbulletService> log)
        {
            HttpClientFactory = httpClientFactory;
            OptionsMonitor = optionsMonitor;
            Log = log;

            RecentlySent = new MemoryCache(new MemoryCacheOptions());
        }

        private bool Disposed { get; set; }
        private IHttpClientFactory HttpClientFactory { get; }
        private ILogger<PushbulletService> Log { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private PushbulletOptions PushbulletOptions => OptionsMonitor.CurrentValue.Integration.Pushbullet;
        private IMemoryCache RecentlySent { get; }

        /// <summary>
        ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Sends a push notification to Pushbullet.
        /// </summary>
        /// <param name="title">The notification title.</param>
        /// <param name="cacheKey">A unique cache key for the notification.</param>
        /// <param name="body">The notification body.</param>
        /// <returns>The operation context.</returns>
        public Task PushAsync(string title, string cacheKey, string body)
        {
            if (!PushbulletOptions.Enabled)
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A notification title must be supplied", nameof(title));
            }

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("A notification cache key must be supplied", nameof(cacheKey));
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                throw new ArgumentException("A notification body must be supplied", nameof(body));
            }

            if (RecentlySent.TryGetValue(cacheKey, out _))
            {
                return Task.CompletedTask;
            }

            RecentlySent.Set(cacheKey, value: true, absoluteExpirationRelativeToNow: TimeSpan.FromMilliseconds(PushbulletOptions.CooldownTime));

            return PushInternalAsync(title, body);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    RecentlySent.Dispose();
                }

                Disposed = true;
            }
        }

        private async Task PushInternalAsync(string title, string body)
        {
            try
            {
                title = $"{PushbulletOptions.NotificationPrefix} {title}";

                var json = JsonSerializer.Serialize(new
                {
                    title,
                    body,
                    type = "note",
                });

                var content = new StringContent(json);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                content.Headers.Add("Access-Token", PushbulletOptions.AccessToken);

                using var http = HttpClientFactory.CreateClient();

                http.DefaultRequestHeaders.UserAgent.TryParseAdd($"{Program.AppName} v{Program.FullVersion}");

                Log.LogDebug("Sending Pushbullet notification {Title} {Body}", title, body);

                await Retry.Do(
                    task: async () =>
                    {
                        using var response = await http.PostAsync(PushUri, content);
                        response.EnsureSuccessStatusCode();
                    },
                    isRetryable: (attempts, ex) => true,
                    onFailure: (attempts, ex) => Log.LogWarning("Failed attempt #{Attempts} to send Pushbullet notification {Title} {Body}: {Message}", attempts, title, body, ex.Message),
                    maxAttempts: PushbulletOptions.RetryAttempts,
                    maxDelayInMilliseconds: 30000);

                Log.LogInformation("Sent Pushbullet notification {Title} {Body}", title, body);
            }
            catch (RetryException ex)
            {
                Log.LogError(ex, "Fatal error retrying send of Pushbullet notification {Title} {Body}: {Message}", title, body, ex.Message);
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to send Pushbullet notification {Title} {Body} after {Attempts} attempts: {Message}", title, body, PushbulletOptions.RetryAttempts, ex.Message);
            }
        }
    }
}