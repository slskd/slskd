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
        ///     Gets the name of the local host.
        /// </summary>
        string LocalHostName { get; }

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
        string GenerateAuthenticationChallengeToken(string connectionId);

        /// <summary>
        ///     Retrieves a new share upload token for the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <param name="agentName">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        Guid GenerateShareUploadToken(string agentName);

        /// <summary>
        ///     Retrieves a stream of the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="id">A unique ID for the stream.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file.</returns>
        Task<Stream> GetFileStream(string agentName, string filename, Guid id, int timeout = 3000);

        /// <summary>
        ///     Safely attempts to close a stream obtained with <see cref="GetFileStream"/>.
        /// </summary>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">If the transfer associated with the stream failed, the exception that caused the failure.</param>
        void TryCloseFileStream(Guid id, Exception exception = null);

        /// <summary>
        ///     Notifies the caller of <see cref="GetFileStream"/> of a failure to obtain a file stream from the requested agent.
        /// </summary>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">The remote exception that caused the failure.</param>
        void NotifyFileStreamFailure(Guid id, Exception exception);

        /// <summary>
        ///     Retrieves information about the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file information.</param>
        /// <param name="filename">The file for which to retrieve information.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>A value indicating whether the file exists, and the length in bytes.</returns>
        Task<(bool Exists, long Length)> GetFileInfo(string agentName, string filename, int timeout = 3000);

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileInfo"/> request.
        /// </summary>
        /// <param name="agentName">The agent that provided the response.</param>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        void HandleGetFileInfoResponse(string agentName, Guid id, (bool Exists, long Length) response);

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileStream"/> request, returning when the corresponding file upload is complete.
        /// </summary>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        /// <returns>The operation context.</returns>
        Task HandleGetFileStreamResponse(Guid id, Stream response);

        void HandleGetFileStreamCompletion(string agentName, string filename);

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent.</param>
        void RegisterAgent(string connectionId, Agent agent);

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The connection ID associated with the registration.</param>
        /// <param name="record">The registration record, if removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        bool TryDeregisterAgent(string connectionId, out (string ConnectionId, Agent Agent) record);

        /// <summary>
        ///     Attempts to retrieve the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The connection ID associated with the registration.</param>
        /// <param name="record">The registration record, if found.</param>
        /// <returns>A value indicating whether the registration exists.</returns>
        bool TryGetAgentRegistration(string connectionId, out (string ConnectionId, Agent Agent) record);

        /// <summary>
        ///     Validates an authentication challenge response.
        /// </summary>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <param name="agentName">The agent name.</param>
        /// <param name="challengeResponse">The challenge response provided by the agent.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        bool TryValidateAuthenticationChallengeResponse(string connectionId, string agentName, string challengeResponse);

        /// <summary>
        ///     Attempts to validate the file upload response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="filename">The name of the file being uploaded.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        bool TryValidateFileUploadCredential(Guid token, string agentName, string filename, string credential);

        /// <summary>
        ///     Attempts to validate the share upload response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        bool TryValidateShareUploadCredential(Guid token, string agentName, string credential);
    }

    public class NetworkService : INetworkService
    {
        public NetworkService(
            IWaiter waiter,
            IOptionsMonitor<Options> optionsMonitor,
            IHubContext<NetworkHub, INetworkHub> networkHub)
        {
            Waiter = waiter;
            NetworkHub = networkHub;

            OptionsMonitor = optionsMonitor;
        }

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        public ReadOnlyCollection<Agent> RegisteredAgents => RegisteredAgentDictionary.Values.Select(v => v.Agent).ToList().AsReadOnly();

        /// <summary>
        ///     Gets the name of the local host.
        /// </summary>
        public string LocalHostName => "local";

        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();
        private MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions());
        private IHubContext<NetworkHub, INetworkHub> NetworkHub { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ConcurrentDictionary<Guid, TaskCompletionSource<(bool Exists, long Length)>> PendingFileInquiryDictionary { get; } = new();
        private ConcurrentDictionary<WaitKey, Guid> WaitIdDictionary { get; } = new();
        private ConcurrentDictionary<string, (string ConnectionId, Agent Agent)> RegisteredAgentDictionary { get; } = new();
        private IWaiter Waiter { get; }

        /// <summary>
        ///     Generates a random authentication challenge token for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <param name="connectionId">The ID of the agent connection.</param>
        /// <returns>The generated token.</returns>
        public string GenerateAuthenticationChallengeToken(string connectionId)
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
        ///     Retrieves a stream of the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <param name="agentName">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="id">A unique ID for the stream.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        public async Task<Stream> GetFileStream(string agentName, string filename, Guid id, int timeout = 3000)
        {
            if (!RegisteredAgentDictionary.TryGetValue(agentName, out var record))
            {
                throw new NotFoundException($"Agent {agentName} is not registered");
            }

            // cache the id to prevent replay attacks; the agent should respond within the timeout, otherwise
            // when it does we will no longer know about it
            MemoryCache.Set(GetFileTokenCacheKey(filename, id), agentName, TimeSpan.FromMilliseconds(timeout));
            Log.Debug("Cached file upload token {Token} for agent {Agent}", id, agentName);

            // create a wait for the agent response. this wait will be completed in the response handler,
            // ultimately called from the API controller when the agent makes an HTTP request to return the file
            var key = new WaitKey(nameof(GetFileStream), id);
            var wait = Waiter.Wait<Stream>(key, timeout);

            await NetworkHub.Clients.Client(record.ConnectionId).RequestFile(filename, id);
            Log.Information("Requested file {Filename} from Agent {Agent} with ID {Id}. Waiting for incoming connection.", filename, agentName, id);

            var task = await Task.WhenAny(wait, Task.Delay(timeout));

            if (task == wait)
            {
                // send the stream back to the caller so it can be used to feed data to the remote client
                Log.Information("Agent {Agent} provided file stream for file {Filename} with ID {Id}", agentName, filename, id);
                var stream = await wait;
                return stream;
            }
            else
            {
                throw new TimeoutException($"Timed out attempting to retrieve the file {filename} from agent {agentName}");
            }
        }

        /// <summary>
        ///     Notifies the caller of <see cref="GetFileStream"/> of a failure to obtain a file stream from the requested agent.
        /// </summary>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">The remote exception that caused the failure.</param>
        public void NotifyFileStreamFailure(Guid id, Exception exception)
        {
            var key = new WaitKey(nameof(GetFileStream), id);
            Waiter.Throw(key, exception);
        }

        /// <summary>
        ///     Safely attempts to close a stream obtained with <see cref="GetFileStream"/>.
        /// </summary>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">If the transfer associated with the stream failed, the exception that caused the failure.</param>
        public void TryCloseFileStream(Guid id, Exception exception = default)
        {
            var key = new WaitKey(nameof(HandleGetFileStreamResponse), id);

            if (exception != default)
            {
                Waiter.Throw(key, exception);
                return;
            }

            Waiter.Complete(key);
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
            if (!RegisteredAgentDictionary.TryGetValue(agentName, out var record))
            {
                throw new NotFoundException($"Agent {agentName} is not registered");
            }

            var id = Guid.NewGuid();
            var key = new WaitKey(nameof(GetFileInfo), agentName, id);
            var wait = Waiter.Wait<(bool Exists, long Length)>(key, timeout);

            try
            {
                await NetworkHub.Clients.Client(record.ConnectionId).RequestFileInfo(filename, id);
                Log.Information("Requested file information for {Filename} from Agent {Agent} with ID {Id}. Waiting for response.", filename, agentName, id);

                return await wait;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to fetch file information for {Filename} from Agent {Agent}: {Message}", filename, agentName, ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileInfo"/> request.
        /// </summary>
        /// <param name="agentName">The agent that provided the response.</param>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        public void HandleGetFileInfoResponse(string agentName, Guid id, (bool Exists, long Length) response)
        {
            var key = new WaitKey(nameof(GetFileInfo), agentName, id);

            if (!Waiter.IsWaitingFor(key))
            {
                Log.Warning("Agent {Agent} responded to a file info request with Id {Id}, but a response was not expected", agentName, id);
                return;
            }

            Waiter.Complete(key, response);
            Log.Information("Agent {Agent} responded to a file info request with Id {Id}: Exists: {Exists}, Length: {Length}", agentName, id, response.Exists, response.Length);
        }

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileStream"/> request, returning when the corresponding file upload is complete.
        /// </summary>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        /// <returns>The operation context.</returns>
        public async Task HandleGetFileStreamResponse(Guid id, Stream response)
        {
            var streamKey = new WaitKey(nameof(GetFileStream), id);

            if (!Waiter.IsWaitingFor(streamKey))
            {
                Log.Warning("A file stream response matching Id {Id} is not expected", id);
                return;
            }

            // create a wait (but do not await it) for the completion of the upload to the remote client. we need to await this
            // to keep execution in the body of the API controller that provided the stream, in order to keep
            // the stream open for the duration of the remote transfer. omit the id from the key, the caller doesn't know it.
            var completionKey = new WaitKey(nameof(HandleGetFileStreamResponse), id);
            var completion = Waiter.WaitIndefinitely(completionKey);

            // complete the wait that's waiting for the stream, so we can send the stream back to the caller of GetFileStream
            // this sends execution back to the body of GetFileStream and the upload will begin transferring data
            Waiter.Complete(streamKey, response);

            // wait for the caller of GetFileStream to report that the upload/stream is complete
            // this is the wait that keeps the HTTP handler running
            await completion;
        }

        public void HandleGetFileStreamCompletion(string agentName, string filename)
        {
            // complete the indefinite wait for this upload. this sends execution back to the body of the file
            // stream response handler, and ultimately back to the API controller. the agent's original HTTP request
            // will complete.
            var key = new WaitKey(nameof(GetFileStream), "completion", agentName, filename);
            Waiter.Complete(key);
            Log.Information("Upload of {File} from agent {Agent} reported as completed", filename, agentName);
        }

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent.</param>
        public void RegisterAgent(string connectionId, Agent agent)
            => RegisteredAgentDictionary.AddOrUpdate(agent.Name, addValue: (connectionId, agent), updateValueFactory: (k, v) => (connectionId, agent));

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="record">The registration record, if one was removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        public bool TryDeregisterAgent(string connectionId, out (string ConnectionId, Agent Agent) record)
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
        public bool TryGetAgentRegistration(string connectionId, out (string ConnectionId, Agent Agent) record)
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
        public bool TryValidateAuthenticationChallengeResponse(string connectionId, string agentName, string challengeResponse)
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
        ///     Attempts to validate the file upload response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="filename">The name of the file being uploaded.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        public bool TryValidateFileUploadCredential(Guid token, string agentName, string filename, string credential)
        {
            return TryValidateCredential(token.ToString(), agentName, credential, GetFileTokenCacheKey(filename, token));
        }

        /// <summary>
        ///     Attempts to validate the share upload response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        public bool TryValidateShareUploadCredential(Guid token, string agentName, string credential)
        {
            return TryValidateCredential(token.ToString(), agentName, credential, GetShareTokenCacheKey(token));
        }

        private string GetAuthTokenCacheKey(string connectionId) => $"{connectionId}.auth";

        private string GetFileTokenCacheKey(string filename, Guid token) => $"{filename}.{token}.file";

        private string GetShareTokenCacheKey(Guid token) => $"{token}.share";

        private bool TryValidateCredential(string token, string agentName, string credential, string cacheKey)
        {
            try
            {
                if (!OptionsMonitor.CurrentValue.Network.Agents.TryGetValue(agentName, out var agentOptions))
                {
                    Log.Debug("Validation failed: Agent {Agent} not configured", agentName);
                    return false;
                }

                if (!RegisteredAgentDictionary.TryGetValue(agentName, out _))
                {
                    Log.Debug("Validation failed: Agent {Agent} not registered", agentName);
                    return false;
                }

                if (!MemoryCache.TryGetValue(cacheKey, out var cachedAgentName))
                {
                    Log.Debug("Validation failed: Cache key {Key} not cached", cacheKey);
                    return false;
                }

                if ((string)cachedAgentName != agentName)
                {
                    Log.Debug("Validation failed: Cached agent {Cached} does not match supplied agent {Agent}", cachedAgentName, agentName);
                    return false;
                }

                var key = agentOptions.Secret.FromBase62();
                var tokenBytes = token.ToString().FromBase62();
                var expectedCredential = Aes.Encrypt(tokenBytes, key).ToBase62();

                if (expectedCredential != credential)
                {
                    Log.Debug("Validation failed: Supplied credential {Credential} does not match expected credential {Expected}", credential, expectedCredential);
                    return false;
                }

                return true;
            }
            finally
            {
                // tokens can be used exactly once, pass or fail
                MemoryCache.Remove(cacheKey);
            }
        }
    }
}