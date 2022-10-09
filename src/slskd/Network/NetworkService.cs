// <copyright file="NetworkService.cs" company="slskd Team">
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

namespace slskd.Network
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Serilog;
    using slskd.Cryptography;

    public interface INetworkService
    {
        /// <summary>
        ///     Gets the collection of pending Agent uploads.
        /// </summary>
        ReadOnlyDictionary<Guid, (TaskCompletionSource<Stream> Upload, TaskCompletionSource Completion)> PendingUploads { get; }

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        ReadOnlyDictionary<string, string> RegisteredAgents { get; }

        /// <summary>
        ///     Retrieves an upload of the specified <paramref name="filename"/> from the specified <paramref name="agent"/>.
        /// </summary>
        /// <param name="agent">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        Task<(Stream Stream, TaskCompletionSource Completion)> GetUpload(string agent, string filename, int timeout = 3000);

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The name of the agent.</param>
        void RegisterAgent(string connectionId, string agent);

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        bool TryRemoveAgentRegistration(string connectionId, out string agent);

        /// <summary>
        ///     Generates a random authentication challenge token for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <returns>The generated token.</returns>
        string GenerateAuthenticationChallengeToken(string connectionId);

        /// <summary>
        ///     Validates an authentication challenge response.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agent">The agent name.</param>
        /// <param name="challengeResponse">The challenge response provided by the agent.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        bool TryValidateAuthenticationChallengeResponse(string connectionId, string agent, string challengeResponse);
    }

    public class NetworkService : INetworkService
    {
        public NetworkService(
            IOptionsMonitor<Options> optionsMonitor,
            IHttpClientFactory httpClientFactory,
            IHubContext<NetworkHub> networkHub)
        {
            NetworkHub = networkHub;
            HttpClientFactory = httpClientFactory;

            OptionsMonitor = optionsMonitor;
        }

        /// <summary>
        ///     Gets the collection of pending Agent uploads.
        /// </summary>
        public ReadOnlyDictionary<Guid, (TaskCompletionSource<Stream> Upload, TaskCompletionSource Completion)> PendingUploads => new(PendingUploadDictionary);

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        public ReadOnlyDictionary<string, string> RegisteredAgents => new(RegisteredAgentDictionary);

        private ConcurrentDictionary<Guid, (TaskCompletionSource<Stream> Upload, TaskCompletionSource Completion)> PendingUploadDictionary { get; } = new();
        private ConcurrentDictionary<string, string> RegisteredAgentDictionary { get; } = new();
        private MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions());
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHttpClientFactory HttpClientFactory { get; }
        private HttpClient HttpClient { get; set; }
        private HubConnection HubConnection { get; set; }
        private IHubContext<NetworkHub> NetworkHub { get; set; }
        private string LastOptionsHash { get; set; }
        private SemaphoreSlim ConfigurationSyncRoot { get; } = new SemaphoreSlim(1, 1);
        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The name of the agent.</param>
        public void RegisterAgent(string connectionId, string agent)
            => RegisteredAgentDictionary.TryAdd(connectionId, agent);

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The name of the agent, if a registration was removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        public bool TryRemoveAgentRegistration(string connectionId, out string agent)
            => RegisteredAgentDictionary.TryRemove(connectionId, out agent);

        /// <summary>
        ///     Generates a random authentication challenge token for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <returns>The generated token.</returns>
        public string GenerateAuthenticationChallengeToken(string connectionId)
        {
            var token = Cryptography.Random.GetBytes(16).ToBase62();

            // this token is only valid for this connection id for a short time
            // this is important to prevent replay-type attacks.
            MemoryCache.Set(connectionId, token, TimeSpan.FromMinutes(1));
            Log.Debug("Cached token {Token} for ID {Id}", token, connectionId);
            return token;
        }

        /// <summary>
        ///     Validates an authentication challenge response.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agent">The agent name.</param>
        /// <param name="challengeResponse">The challenge response provided by the agent.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        public bool TryValidateAuthenticationChallengeResponse(string connectionId, string agent, string challengeResponse)
        {
            if (!MemoryCache.TryGetValue(connectionId, out var challengeToken))
            {
                Log.Debug("Auth challenge for {Id} failed: no challenge token cached for ID", connectionId);
                return false;
            }

            if (!OptionsMonitor.CurrentValue.Network.Agents.TryGetValue(agent, out var agentOptions))
            {
                Log.Debug("Auth challenge for {Id} failed: no configuration for agent '{Agent}'", connectionId, agent);
                return false;
            }

            var key = agentOptions.Secret.FromBase62();
            var tokenBytes = ((string)challengeToken).FromBase62();
            var expectedResponse = Aes.Encrypt(tokenBytes, key).ToBase62();

            return expectedResponse == challengeResponse;
        }

        /// <summary>
        ///     Retrieves an upload of the specified <paramref name="filename"/> from the specified <paramref name="agent"/>.
        /// </summary>
        /// <param name="agent">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        public async Task<(Stream Stream, TaskCompletionSource Completion)> GetUpload(string agent, string filename, int timeout = 3000)
        {
            var id = Guid.NewGuid();

            // create a TCS for the upload stream. this is awaited below and completed in
            // the API controller when the agent POSTs the file
            var upload = new TaskCompletionSource<Stream>();

            // create a TCS for the upload itself. this is awaited by the API controller
            // and completed by the transfer service when the upload to the remote user is complete
            // the API controller needs to wait until the remote transfer is complete in order to
            // keep the stream open for the duration
            var completion = new TaskCompletionSource();

            PendingUploadDictionary.TryAdd(id, (upload, completion));

            await NetworkHub.RequestFileAsync(agent, filename, id);
            Log.Information("Requested file {Filename} from Agent {Agent} with ID {Id}. Waiting for incoming connection.", filename, agent, id);

            var task = await Task.WhenAny(upload.Task, Task.Delay(timeout));

            if (task == upload.Task)
            {
                var stream = await upload.Task;
                return (stream, completion);
            }
            else
            {
                throw new TimeoutException($"Timed out attempting to retrieve the file {filename} from agent {agent}");
            }
        }
    }
}
