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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Caching.Memory;
    using Serilog;
    using slskd.Cryptography;

    public interface INetworkService
    {
        /// <summary>
        ///     Gets the collection of pending Agent file uploads.
        /// </summary>
        ReadOnlyDictionary<Guid, (TaskCompletionSource<Stream> Stream, TaskCompletionSource Completion)> PendingFileUploads { get; }

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
        Task<(Stream Stream, TaskCompletionSource Completion)> GetFileUpload(string agent, string filename, int timeout = 3000);

        /// <summary>
        ///     Retrieves a new share upload token for the specified <paramref name="agent"/>.
        /// </summary>
        /// <remarks>
        ///     The token is cached internally, and is only valid while it remains in the cache.
        /// </remarks>
        /// <param name="agent">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        Guid GetShareUploadToken(string agent);

        /// <summary>
        ///     Attempts to retrieve the specified share upload <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agent">The name of the agent attempting to use the token.</param>
        /// <returns>A value indicating whether the specified token exists.</returns>
        bool TryGetShareUploadToken(Guid token, out string agent);

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The name of the agent.</param>
        void RegisterAgent(string connectionId, string agent);

        /// <summary>
        ///     Attempts to retrieve the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agent">The agent, if registered.</param>
        /// <returns>A value indicating whether the registration exists.</returns>
        bool TryGetAgentRegistration(string connectionId, out string agent);

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent, if removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        bool TryRemoveAgentRegistration(string connectionId, out string agent);

        /// <summary>
        ///     Generates a random authentication challenge token for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <remarks>
        ///     The token is cached internally, and is only valid while it remains in the cache.
        /// </remarks>
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
            IHubContext<NetworkHub> networkHub)
        {
            NetworkHub = networkHub;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the collection of pending Agent uploads.
        /// </summary>
        public ReadOnlyDictionary<Guid, (TaskCompletionSource<Stream> Stream, TaskCompletionSource Completion)> PendingFileUploads => new(PendingFileUploadDictionary);

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        public ReadOnlyDictionary<string, string> RegisteredAgents => new(RegisteredAgentDictionary);

        private ConcurrentDictionary<Guid, (TaskCompletionSource<Stream> Stream, TaskCompletionSource Completion)> PendingFileUploadDictionary { get; } = new();
        private ConcurrentDictionary<string, string> RegisteredAgentDictionary { get; } = new();
        private MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions());
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHubContext<NetworkHub> NetworkHub { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        ///     Retrieves a new share upload token for the specified <paramref name="agent"/>.
        /// </summary>
        /// <remarks>
        ///     The token is cached internally, and is only valid while it remains in the cache.
        /// </remarks>
        /// <param name="agent">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        public Guid GetShareUploadToken(string agent)
        {
            var token = Guid.NewGuid();
            MemoryCache.Set(GetShareTokenCacheKey(token), agent, TimeSpan.FromMinutes(1));
            Log.Debug("Cached share upload token {Token} for agent {Agent}", token, agent);

            return token;
        }

        /// <summary>
        ///     Attempts to retrieve the specified share upload <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agent">The name of the agent attempting to use the token.</param>
        /// <returns>A value indicating whether the specified token exists.</returns>
        public bool TryGetShareUploadToken(Guid token, out string agent)
        {
            agent = null;

            if (MemoryCache.TryGetValue(GetShareTokenCacheKey(token), out agent))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The name of the agent.</param>
        public void RegisterAgent(string connectionId, string agent)
            => RegisteredAgentDictionary.TryAdd(connectionId, agent);

        /// <summary>
        ///     Attempts to retrieve the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agent">The agent, if registered.</param>
        /// <returns>A value indicating whether the registration exists.</returns>
        public bool TryGetAgentRegistration(string connectionId, out string agent)
            => RegisteredAgentDictionary.TryGetValue(connectionId, out agent);

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
            MemoryCache.Set(GetAuthTokenCacheKey(connectionId), token, TimeSpan.FromMinutes(1));
            Log.Debug("Cached auth token {Token} for ID {Id}", token, connectionId);
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
            if (!MemoryCache.TryGetValue(GetAuthTokenCacheKey(connectionId), out var challengeToken))
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
        public async Task<(Stream Stream, TaskCompletionSource Completion)> GetFileUpload(string agent, string filename, int timeout = 3000)
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

            PendingFileUploadDictionary.TryAdd(id, (upload, completion));

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

        private string GetAuthTokenCacheKey(string connectionId) => $"{connectionId}.auth";
        private string GetShareTokenCacheKey(Guid token) => $"{token}.share";

        private void Configure(Options options)
        {
            SyncRoot.Wait();

            try
            {
            }
            finally
            {
                SyncRoot.Release();
            }
        }
    }
}
