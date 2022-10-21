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
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Caching.Memory;
    using Serilog;
    using slskd.Cryptography;

    public interface INetworkService
    {
        /// <summary>
        ///     Gets the collection of pending Agent file inquiries.
        /// </summary>
        ReadOnlyDictionary<Guid, TaskCompletionSource<(bool Exists, long Length)>> PendingFileInquiries { get; }

        /// <summary>
        ///     Gets the collection of pending Agent file uploads.
        /// </summary>
        ReadOnlyDictionary<Guid, (TaskCompletionSource<Stream> Stream, TaskCompletionSource Completion)> PendingFileUploads { get; }

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        ReadOnlyCollection<Agent> RegisteredAgents { get; }

        /// <summary>
        ///     Generates a random authentication challenge token for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <returns>The generated token.</returns>
        string GenerateAuthenticationChallengeToken(ConnectionId connectionId);

        /// <summary>
        ///     Retrieves a new share upload token for the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <param name="agentName">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        Guid GenerateShareUploadToken(string agentName);

        /// <summary>
        ///     Retrieves information about the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file information.</param>
        /// <param name="filename">The file for which to retrieve information.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>A value indicating whether the file exists, and the length in bytes.</returns>
        Task<(bool Exists, long Length)> GetFileInfo(string agentName, string filename, int timeout = 3000);

        /// <summary>
        ///     Retrieves an upload of the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        Task<(Stream Stream, TaskCompletionSource Completion)> GetFileUpload(string agentName, string filename, int timeout = 3000);

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent.</param>
        void RegisterAgent(ConnectionId connectionId, Agent agent);

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The connection ID associated with the registration.</param>
        /// <param name="record">The registration record, if removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        bool TryDeregisterAgent(ConnectionId connectionId, out (ConnectionId ConnectionId, Agent Agent) record);

        /// <summary>
        ///     Attempts to retrieve the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The connection ID associated with the registration.</param>
        /// <param name="record">The registration record, if found.</param>
        /// <returns>A value indicating whether the registration exists.</returns>
        bool TryGetAgentRegistration(ConnectionId connectionId, out (ConnectionId ConnectionId, Agent Agent) record);

        /// <summary>
        ///     Validates an authentication challenge response.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agent">The agent name.</param>
        /// <param name="challengeResponse">The challenge response provided by the agent.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        bool TryValidateAuthenticationChallengeResponse(ConnectionId connectionId, string agent, string challengeResponse);

        /// <summary>
        ///     Attempts to retrieve the specified share upload <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the agent attempting to use the token.</param>
        /// <returns>A value indicating whether the specified token exists.</returns>
        bool TryValidateShareUploadToken(Guid token, string agentName);
    }

    public class NetworkService : INetworkService
    {
        public NetworkService(
            IOptionsMonitor<Options> optionsMonitor,
            IHubContext<NetworkHub, INetworkHub> networkHub)
        {
            NetworkHub = networkHub;

            OptionsMonitor = optionsMonitor;
        }

        /// <summary>
        ///     Gets the collection of pending Agent file inquiries.
        /// </summary>
        public ReadOnlyDictionary<Guid, TaskCompletionSource<(bool Exists, long Length)>> PendingFileInquiries => new(PendingFileInquiryDictionary);

        /// <summary>
        ///     Gets the collection of pending Agent uploads.
        /// </summary>
        public ReadOnlyDictionary<Guid, (TaskCompletionSource<Stream> Stream, TaskCompletionSource Completion)> PendingFileUploads => new(PendingFileUploadDictionary);

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        public ReadOnlyCollection<Agent> RegisteredAgents => RegisteredAgentDictionary.Values.Select(v => v.Agent).ToList().AsReadOnly();

        private SemaphoreSlim AgentSyncRoot { get; } = new SemaphoreSlim(1, 1);
        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();
        private MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions());
        private IHubContext<NetworkHub, INetworkHub> NetworkHub { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ConcurrentDictionary<Guid, TaskCompletionSource<(bool Exists, long Length)>> PendingFileInquiryDictionary { get; } = new();
        private ConcurrentDictionary<Guid, (TaskCompletionSource<Stream> Stream, TaskCompletionSource Completion)> PendingFileUploadDictionary { get; } = new();
        private ConcurrentDictionary<string, (ConnectionId ConnectionId, Agent Agent)> RegisteredAgentDictionary { get; } = new();

        /// <summary>
        ///     Generates a random authentication challenge token for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <returns>The generated token.</returns>
        public string GenerateAuthenticationChallengeToken(ConnectionId connectionId)
        {
            var token = Cryptography.Random.GetBytes(32).ToBase62();

            // this token is only valid for this connection id for a short time this is important to prevent replay-type attacks.
            MemoryCache.Set(GetAuthTokenCacheKey(connectionId), token, TimeSpan.FromMinutes(1));
            Log.Debug("Cached auth token {Token} for ID {Id}", token, connectionId);
            return token;
        }

        /// <summary>
        ///     Retrieves a new share upload token for the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <param name="agentName">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        public Guid GenerateShareUploadToken(string agentName)
        {
            var token = Guid.NewGuid();

            MemoryCache.Set(GetShareTokenCacheKey(token), agentName, TimeSpan.FromMinutes(1));
            Log.Debug("Cached share upload token {Token} for agent {Agent}", token, agentName);

            return token;
        }

        /// <summary>
        ///     Retrieves information about the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file information.</param>
        /// <param name="filename">The file for which to retrieve information.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>A value indicating whether the file exists, and the length in bytes.</returns>
        public async Task<(bool Exists, long Length)> GetFileInfo(string agentName, string filename, int timeout = 3000)
        {
            var id = Guid.NewGuid();
            var tcs = new TaskCompletionSource<(bool Exists, long Length)>();

            if (!RegisteredAgentDictionary.TryGetValue(agentName, out var record))
            {
                throw new NotFoundException($"Agent {agentName} is not registered");
            }

            PendingFileInquiryDictionary.TryAdd(id, tcs);

            try
            {
                await NetworkHub.Clients.Client(record.ConnectionId).RequestFileInfoAsync(filename, id);
                Log.Information("Requested file information for {Filename} from Agent {Agent} with ID {Id}. Waiting for response.", filename, agentName, id);

                var task = await Task.WhenAny(tcs.Task, Task.Delay(timeout));

                if (task == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    throw new TimeoutException($"Timed out attempting to retrieve file information for {filename} from Agent {agentName}");
                }
            }
            finally
            {
                PendingFileInquiryDictionary.TryRemove(id, out _);
            }
        }

        /// <summary>
        ///     Retrieves an upload of the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        public async Task<(Stream Stream, TaskCompletionSource Completion)> GetFileUpload(string agentName, string filename, int timeout = 3000)
        {
            var id = Guid.NewGuid();

            // create a TCS for the upload stream. this is awaited below and completed in the API controller when the agent POSTs
            // the file
            var upload = new TaskCompletionSource<Stream>();

            // create a TCS for the upload itself. this is awaited by the API controller and completed by the transfer service
            // when the upload to the remote user is complete the API controller needs to wait until the remote transfer is
            // complete in order to keep the stream open for the duration
            var completion = new TaskCompletionSource();

            if (!RegisteredAgentDictionary.TryGetValue(agentName, out var record))
            {
                throw new NotFoundException($"Agent {agentName} is not registered");
            }

            PendingFileUploadDictionary.TryAdd(id, (upload, completion));

            try
            {
                await NetworkHub.Clients.Client(record.ConnectionId).RequestFileAsync(filename, id);
                Log.Information("Requested file {Filename} from Agent {Agent} with ID {Id}. Waiting for incoming connection.", filename, agentName, id);

                var task = await Task.WhenAny(upload.Task, Task.Delay(timeout));

                if (task == upload.Task)
                {
                    var stream = await upload.Task;
                    return (stream, completion);
                }
                else
                {
                    throw new TimeoutException($"Timed out attempting to retrieve the file {filename} from agent {agentName}");
                }
            }
            finally
            {
                PendingFileUploadDictionary.TryRemove(id, out _);
            }
        }

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent.</param>
        public void RegisterAgent(ConnectionId connectionId, Agent agent)
            => RegisteredAgentDictionary.AddOrUpdate(agent.Name, addValue: (connectionId, agent), updateValueFactory: (k, v) => (connectionId, agent));

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="record">The registration record, if one was removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        public bool TryDeregisterAgent(ConnectionId connectionId, out (ConnectionId ConnectionId, Agent Agent) record)
        {
            if (TryGetAgentRegistration(connectionId, out var found))
            {
                return RegisteredAgentDictionary.TryRemove(found.Agent.Name, out record);
            }

            record = default;
            return false;
        }

        /// <summary>
        ///     Attempts to retrieve the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="record">The registration record, if one exists.</param>
        /// <returns>A value indicating whether the registration exists.</returns>
        public bool TryGetAgentRegistration(ConnectionId connectionId, out (ConnectionId ConnectionId, Agent Agent) record)
        {
            var found = RegisteredAgentDictionary.Values.FirstOrDefault(v => v.ConnectionId == connectionId);
            record = found;

            return record != default;
        }

        /// <summary>
        ///     Validates an authentication challenge response.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agentName">The agent name.</param>
        /// <param name="challengeResponse">The challenge response provided by the agent.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        public bool TryValidateAuthenticationChallengeResponse(ConnectionId connectionId, string agentName, string challengeResponse)
        {
            if (!MemoryCache.TryGetValue(GetAuthTokenCacheKey(connectionId), out var challengeToken))
            {
                Log.Debug("Auth challenge for {Id} failed: no challenge token cached for ID", connectionId);
                return false;
            }

            if (!OptionsMonitor.CurrentValue.Network.Agents.TryGetValue(agentName, out var agentOptions))
            {
                Log.Debug("Auth challenge for {Id} failed: no configuration for agent '{Agent}'", connectionId, agentName);
                return false;
            }

            var key = agentOptions.Secret.FromBase62();
            var tokenBytes = ((string)challengeToken).FromBase62();
            var expectedResponse = Aes.Encrypt(tokenBytes, key).ToBase62();

            return expectedResponse == challengeResponse;
        }

        /// <summary>
        ///     Attempts to retrieve the specified share upload <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the agent attempting to use the token.</param>
        /// <returns>A value indicating whether the specified token exists.</returns>
        public bool TryValidateShareUploadToken(Guid token, string agentName)
        {
            if (MemoryCache.TryGetValue(GetShareTokenCacheKey(token), out var cachedAgentName) && (string)cachedAgentName == agentName)
            {
                return true;
            }

            return false;
        }

        private string GetAuthTokenCacheKey(string connectionId) => $"{connectionId}.auth";

        private string GetShareTokenCacheKey(Guid token) => $"{token}.share";
    }
}