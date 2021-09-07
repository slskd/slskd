// <copyright file="ManagementService.cs" company="slskd Team">
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

namespace slskd.Management
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Application and Soulseek client management.
    /// </summary>
    public class ManagementService : IManagementService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ManagementService"/> class.
        /// </summary>
        /// <param name="optionsMonitor">The options monitor used to derive application options.</param>
        /// <param name="applicationStateMonitor">The state monitor for application service state.</param>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="sharedFileCache">The shared file cache.</param>
        /// <param name="httpClientFactory">The HttpClientFactory to use.</param>
        public ManagementService(
            IOptionsMonitor<Options> optionsMonitor,
            IStateMonitor<ApplicationState> applicationStateMonitor,
            ISoulseekClient soulseekClient,
            ISharedFileCache sharedFileCache,
            IHttpClientFactory httpClientFactory)
        {
            OptionsMonitor = optionsMonitor;
            ApplicationStateMonitor = applicationStateMonitor;
            Client = soulseekClient;
            SharedFileCache = sharedFileCache;
            HttpClientFactory = httpClientFactory;
        }

        private IHttpClientFactory HttpClientFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ManagementService>();

        /// <summary>
        ///     Gets the current state of the connection to the Soulseek server.
        /// </summary>
        public ServerState ServerState =>
            new ServerState()
            {
                Address = Client.Address,
                IPEndPoint = Client.IPEndPoint,
                State = Client.State,
                Username = Client.Username,
            };

        /// <summary>
        ///     Gets the current state of the connection to the Soulseek server.
        /// </summary>
        public SharedFileCacheState SharedFileCacheState => SharedFileCache.State.CurrentValue;

        private ISoulseekClient Client { get; }
        private Options Options => OptionsMonitor.CurrentValue;
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IStateMonitor<ApplicationState> ApplicationStateMonitor { get; }
        private ISharedFileCache SharedFileCache { get; }

        /// <summary>
        ///     Connects the Soulseek client to the server using the configured username and password.
        /// </summary>
        /// <returns>The operation context.</returns>
        public Task ConnectServerAsync()
            => Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);

        /// <summary>
        ///     Disconnects the Soulseek client from the server.
        /// </summary>
        /// <param name="message">An optional message containing the reason for the disconnect.</param>
        /// <param name="exception">An optional Exception to associate with the disconnect.</param>
        public void DisconnectServer(string message = null, Exception exception = null)
            => Client.Disconnect(message, exception ?? new IntentionalDisconnectException(message));

        /// <summary>
        ///     Re-scans shared directories.
        /// </summary>
        /// <returns>The operation context.</returns>
        public Task RescanSharesAsync() => SharedFileCache.FillAsync();

        /// <summary>
        ///     Gets the version of the latest application release.
        /// </summary>
        /// <returns>The operation context.</returns>
        public async Task CheckVersionAsync()
        {
            if (Program.InformationalVersion.EndsWith("65534"))
            {
                Log.Information("Skipping version check for Canary build; check for updates manually.");
            }

            try
            {
                using var http = HttpClientFactory.CreateClient();
                http.DefaultRequestHeaders.UserAgent.TryParseAdd(Program.AppName + Program.Version);

                Log.Information("Checking {LatestReleaseUrl} for latest version", Program.RepositoryAPILatestReleaseUrl);

                var response = await http.GetFromJsonAsync<JsonDocument>(Program.RepositoryAPILatestReleaseUrl);
                var latestVersion = Version.Parse(response.RootElement.GetProperty("tag_name").GetString());
                var currentVersion = Version.Parse(Program.InformationalVersion);

                if (latestVersion > currentVersion)
                {
                    ApplicationStateMonitor.SetValue(state => state with { LatestVersion = latestVersion.ToString(), UpdateAvailable = true });
                    Log.Information("A new version is available! {CurrentVersion} -> {LatestVersion}", currentVersion, latestVersion);
                }
                else
                {
                    Log.Information("Version {Version} is up to date.", currentVersion);
                }

            }
            catch (Exception ex)
            {
                Log.Warning("Failed to check version: {Message}", ex.Message);
                throw;
            }
        }
    }
}