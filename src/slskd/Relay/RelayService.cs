// <copyright file="RelayService.cs" company="slskd Team">
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

namespace slskd.Relay
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Caching.Memory;
    using Serilog;
    using slskd.Cryptography;
    using slskd.Shares;

    /// <summary>
    ///     Handles relay (controller/agent) interactions.
    /// </summary>
    public interface IRelayService
    {
        /// <summary>
        ///     Gets the relay client (agent).
        /// </summary>
        IRelayClient Client { get; }

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        ReadOnlyCollection<Agent> RegisteredAgents { get; }

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        IStateMonitor<RelayState> StateMonitor { get; }

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
        /// <remarks>
        ///     <para>This is the first step in a multi-step workflow. The entire sequence is:</para>
        ///     <list type="number">
        ///         <item>
        ///             A remote agent makes a request to the SignalR hub to retrieve a share upload token, which in turn calls
        ///             <see cref="GenerateShareUploadToken"/>. The token is generated and cached, and is only valid while it is
        ///             in the cache.
        ///         </item>
        ///         <item>
        ///             The remote agent makes an HTTP POST request containing a multipart upload including a backup of its shared
        ///             database and a serialized list of locally configured shares, and the controller invokes <see cref="HandleShareUploadAsync"/>.
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <param name="agentName">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        Guid GenerateShareUploadToken(string agentName);

        /// <summary>
        ///     Retrieves information about the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>
        ///     <para>This is the first step in a mult-step workflow. The entire sequence is:</para>
        ///     <list type="number">
        ///         <item>
        ///             Upload service calls and awaits <see cref="GetFileInfoAsync"/>, which requests the file info from the
        ///             remote agent, and waits for the response before returning it to the caller.
        ///         </item>
        ///         <item>
        ///             The remote agent sends the response via the SignalR hub, and the hub invokes
        ///             <see cref="HandleFileInfoResponse"/>. The response is passed back to <see cref="GetFileInfoAsync"/> and
        ///             returned to the caller.
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <param name="agentName">The agent from which to retrieve the file information.</param>
        /// <param name="filename">The file for which to retrieve information.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>A value indicating whether the file exists, and the length in bytes.</returns>
        Task<(bool Exists, long Length)> GetFileInfoAsync(string agentName, string filename, int timeout = 3000);

        /// <summary>
        ///     Retrieves a stream of the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is the first step in a multi-step workflow that enables proxying of file uploads through agents. The
        ///         entire sequence is:
        ///     </para>
        ///     <list type="number">
        ///         <item>
        ///             Upload service calls and awaits <see cref="GetFileStreamAsync"/>, which requests the file from the remote
        ///             agent, and waits for the stream to be returned before returning it to the caller.
        ///         </item>
        ///         <item>
        ///             <para>
        ///                 The remote agent makes an HTTP POST request containing a multipart upload including the file, and the
        ///                 API controller invokes and awaits <see cref="HandleFileStreamResponseAsync"/>. The stream is passed from
        ///                 this method back to the awaited <see cref="GetFileStreamAsync"/>, and the Upload service passes the
        ///                 stream to Soulseek.NET, streaming the data from the still-open HTTP request through to the remote
        ///                 Soulseek user.
        ///             </para>
        ///             <para>
        ///                 If the remote agent can't find or open the requested file, it invokes
        ///                 <see cref="NotifyFileStreamException"/> through the open SignalR connection, and
        ///                 <see cref="GetFileStreamAsync"/> throws with the given exception. If the remote agent fails to respond
        ///                 within the timeout period, <see cref="GetFileStreamAsync"/> throws a <see cref="TimeoutException"/>.
        ///             </para>
        ///         </item>
        ///         <item>
        ///             When the Upload is complete (successfully or otherwise), the Upload service invokes
        ///             <see cref="TryCloseFileStream"/>, passing an optional <see cref="Exception"/> if the transfer was not
        ///             successful. This call signals the waiting <see cref="HandleFileStreamResponseAsync"/> to complete, passing
        ///             control back to the API controller and completing the HTTP POST request from the agent.
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <param name="agentName">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="startOffset">The starting offset for the transfer.</param>
        /// <param name="id">A unique ID for the stream.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <param name="cancellationToken">An optional token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including a stream containing the requested file.</returns>
        Task<Stream> GetFileStreamAsync(string agentName, string filename, long startOffset, Guid id, int timeout = 3000, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileInfoAsync"/> request.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        void HandleFileInfoResponse(string agentName, Guid id, (bool Exists, long Length) response);

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileStreamAsync"/> request, returning when the corresponding file
        ///     upload is complete.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        /// <returns>The operation context.</returns>
        Task HandleFileStreamResponseAsync(string agentName, Guid id, Stream response);

        /// <summary>
        ///     Handles incoming share uploads.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The ID obtained by the caller prior to uploading.</param>
        /// <param name="shares">The list of shares provided.</param>
        /// <param name="filename">The filename of the temporary file containing the upload.</param>
        /// <returns>The operation context.</returns>
        Task HandleShareUploadAsync(string agentName, Guid id, IEnumerable<Share> shares, string filename);

        /// <summary>
        ///     Notifies connected agents that a file download has completed.
        /// </summary>
        /// <param name="filename">The filename of the completed file, relative to the downloads directory.</param>
        /// <returns>The operation context.</returns>
        Task NotifyFileDownloadCompleteAsync(string filename);

        /// <summary>
        ///     Notifies the caller of <see cref="GetFileStreamAsync"/> of a failure to obtain a file stream from the requested agent.
        /// </summary>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">The remote exception that caused the failure.</param>
        void NotifyFileStreamException(Guid id, Exception exception);

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent.</param>
        void RegisterAgent(string connectionId, Agent agent);

        /// <summary>
        ///     Safely attempts to close a stream obtained with <see cref="GetFileStreamAsync"/>.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">If the transfer associated with the stream failed, the exception that caused the failure.</param>
        void TryCloseFileStream(string agentName, Guid id, Exception exception = null);

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
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        bool TryValidateAuthenticationCredential(string connectionId, string agentName, string credential);

        /// <summary>
        ///     Attempts to validate the file download response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="filename">The name of the file being downloaded.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        bool TryValidateFileDownloadCredential(Guid token, string agentName, string filename, string credential);

        /// <summary>
        ///     Attempts to validate the file stream response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="filename">The name of the file being uploaded.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        bool TryValidateFileStreamResponseCredential(Guid token, string agentName, string filename, string credential);

        /// <summary>
        ///     Attempts to validate the share upload response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        bool TryValidateShareUploadCredential(Guid token, string agentName, string credential);
    }

    /// <summary>
    ///     Handles relay (controller/agent) interactions.
    /// </summary>
    public class RelayService : IRelayService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RelayService"/> class.
        /// </summary>
        /// <param name="waiter"></param>
        /// <param name="shareService"></param>
        /// <param name="shareRepositoryFactory"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="relayHub"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="relayClient"></param>
        public RelayService(
            IWaiter waiter,
            IShareService shareService,
            IShareRepositoryFactory shareRepositoryFactory,
            IOptionsMonitor<Options> optionsMonitor,
            IHubContext<RelayHub, IRelayHub> relayHub,
            IHttpClientFactory httpClientFactory,
            IRelayClient relayClient = null)
        {
            Shares = shareService;
            ShareRepositoryFactory = shareRepositoryFactory;
            Waiter = waiter;
            RelayHub = relayHub;

            HttpClientFactory = httpClientFactory;

            // wire up a dummy client so callers don't need to handle nulls
            Client = relayClient ?? new NullRelayClient();

            StateMonitor = State;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));
            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the relay client (agent).
        /// </summary>
        public IRelayClient Client { get; private set; }

        /// <summary>
        ///     Gets the collection of registered Agents.
        /// </summary>
        public ReadOnlyCollection<Agent> RegisteredAgents => RegisteredAgentDictionary.Values.Select(v => v.Agent).ToList().AsReadOnly();

        /// <summary>
        ///     Gets the state monitor for the service.
        /// </summary>
        public IStateMonitor<RelayState> StateMonitor { get; }

        private IHttpClientFactory HttpClientFactory { get; }
        private string LastControllerOptionsHash { get; set; }
        private string LastOptionsHash { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<RelayService>();
        private MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions());
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ConcurrentDictionary<Guid, TaskCompletionSource<(bool Exists, long Length)>> PendingFileInquiryDictionary { get; } = new();
        private ConcurrentDictionary<string, (string ConnectionId, Agent Agent)> RegisteredAgentDictionary { get; } = new();
        private IHubContext<RelayHub, IRelayHub> RelayHub { get; set; }
        private IShareRepositoryFactory ShareRepositoryFactory { get; }
        private IShareService Shares { get; }
        private IManagedState<RelayState> State { get; } = new ManagedState<RelayState>();
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private IWaiter Waiter { get; }
        private ConcurrentDictionary<WaitKey, Guid> WaitIdDictionary { get; } = new();

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
            MemoryCache.Set(GetAuthTokenCacheKey(connectionId), token, TimeSpan.FromSeconds(10));
            Log.Debug("Cached auth token {Token} for ID {Id}", token, connectionId);
            return token;
        }

        /// <summary>
        ///     Retrieves a new share upload token for the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>The token is cached internally, and is only valid while it remains in the cache.</remarks>
        /// <remarks>
        ///     <para>This is the first step in a multi-step workflow. The entire sequence is:</para>
        ///     <list type="number">
        ///         <item>
        ///             A remote agent makes a request to the SignalR hub to retrieve a share upload token, which in turn calls
        ///             <see cref="GenerateShareUploadToken"/>. The token is generated and cached.
        ///         </item>
        ///         <item>
        ///             The remote agent makes an HTTP POST request containing a multipart upload including a backup of its shared
        ///             database and a serialized list of locally configured shares.
        ///         </item>
        ///         <item>
        ///             The HTTP controller saves the database backup to a temporary file, validates it, and then adds (or
        ///             updates) a share host for the agent.
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <param name="agentName">The name of the agent.</param>
        /// <returns>The generated token.</returns>
        public Guid GenerateShareUploadToken(string agentName)
        {
            var token = Guid.NewGuid();

            // allow a generous amount of time, in case it takes a while to upload the response
            MemoryCache.Set(GetShareTokenCacheKey(token), agentName, TimeSpan.FromMinutes(5));
            Log.Debug("Cached share upload token {Token} for agent {Agent}", token, agentName);

            return token;
        }

        /// <summary>
        ///     Retrieves information about the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>
        ///     <para>This is the first step in a mult-step workflow. The entire sequence is:</para>
        ///     <list type="number">
        ///         <item>
        ///             Upload service calls and awaits <see cref="GetFileInfoAsync"/>, which requests the file info from the
        ///             remote agent, and waits for the response before returning it to the caller.
        ///         </item>
        ///         <item>
        ///             The remote agent sends the response via the SignalR hub, and the hub invokes
        ///             <see cref="HandleFileInfoResponse"/>. The response is passed back to <see cref="GetFileInfoAsync"/> and
        ///             returned to the caller.
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <param name="agentName">The agent from which to retrieve the file information.</param>
        /// <param name="filename">The file for which to retrieve information.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>A value indicating whether the file exists, and the length in bytes.</returns>
        public async Task<(bool Exists, long Length)> GetFileInfoAsync(string agentName, string filename, int timeout = 3000)
        {
            if (!RegisteredAgentDictionary.TryGetValue(agentName, out var record))
            {
                throw new NotFoundException($"Agent {agentName} is not registered");
            }

            // prepare a wait for the response. we don't cache the token along with the agent here, as all data is exchanged over
            // an authenticated SignalR connection
            var id = Guid.NewGuid();
            var key = new WaitKey(nameof(GetFileInfoAsync), agentName, id);
            var wait = Waiter.Wait<(bool Exists, long Length)>(key, timeout);

            Log.Debug("Created wait {Key}", key);

            try
            {
                await RelayHub.Clients.Client(record.ConnectionId).RequestFileInfo(filename, id);
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
        ///     Retrieves a stream of the specified <paramref name="filename"/> from the specified <paramref name="agentName"/>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is the first step in a multi-step workflow that enables proxying of file uploads through agents. The
        ///         entire sequence is:
        ///     </para>
        ///     <list type="number">
        ///         <item>
        ///             Upload service calls and awaits <see cref="GetFileStreamAsync"/>, which requests the file from the remote
        ///             agent, and waits for the stream to be returned before returning it to the caller.
        ///         </item>
        ///         <item>
        ///             <para>
        ///                 The remote agent makes an HTTP POST request containing a multipart upload including the file, and the
        ///                 API controller invokes and awaits <see cref="HandleFileStreamResponseAsync"/>. The stream is passed from
        ///                 this method back to the awaited <see cref="GetFileStreamAsync"/>, and the Upload service passes the
        ///                 stream to Soulseek.NET, streaming the data from the still-open HTTP request through to the remote
        ///                 Soulseek user.
        ///             </para>
        ///             <para>
        ///                 If the remote agent can't find or open the requested file, it invokes
        ///                 <see cref="NotifyFileStreamException"/> through the open SignalR connection, and
        ///                 <see cref="GetFileStreamAsync"/> throws with the given exception. If the remote agent fails to respond
        ///                 within the timeout period, <see cref="GetFileStreamAsync"/> throws a <see cref="TimeoutException"/>.
        ///             </para>
        ///         </item>
        ///         <item>
        ///             When the Upload is complete (successfully or otherwise), the Upload service invokes
        ///             <see cref="TryCloseFileStream"/>, passing an optional <see cref="Exception"/> if the transfer was not
        ///             successful. This call signals the waiting <see cref="HandleFileStreamResponseAsync"/> to complete, passing
        ///             control back to the API controller and completing the HTTP POST request from the agent.
        ///         </item>
        ///     </list>
        /// </remarks>
        /// <param name="agentName">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="startOffset">The starting offset for the transfer.</param>
        /// <param name="id">A unique ID for the stream.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <param name="cancellationToken">An optional token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including a stream containing the requested file.</returns>
        public async Task<Stream> GetFileStreamAsync(string agentName, string filename, long startOffset, Guid id, int timeout = 3000, CancellationToken cancellationToken = default)
        {
            if (!RegisteredAgentDictionary.TryGetValue(agentName, out var record))
            {
                throw new NotFoundException($"Agent {agentName} is not registered");
            }

            // cache the id to prevent replay attacks; the agent should respond within the timeout. this is somewhat redundant due
            // to the wait using only the id, however caching the agent name along with the other elements of the request allows
            // us to ensure that tokens are used only by the agent they were intended for.
            MemoryCache.Set(GetFileTokenCacheKey(filename, id), agentName, TimeSpan.FromMilliseconds(timeout));
            Log.Debug("Cached file upload token {Token} for agent {Agent}", id, agentName);

            // create a wait for the agent response. this wait will be completed in the response handler, ultimately called from
            // the API controller when the agent makes an HTTP request to return the file
            var key = new WaitKey(nameof(GetFileStreamAsync), agentName, id);
            var wait = Waiter.Wait<Stream>(key, timeout, cancellationToken);

            await RelayHub.Clients.Client(record.ConnectionId).RequestFileUpload(filename, startOffset, id);
            Log.Information("Requested file {Filename} from Agent {Agent} with ID {Id}. Waiting for incoming connection.", filename, agentName, id);

            var task = await Task.WhenAny(wait, Task.Delay(timeout, cancellationToken));

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
        ///     Handles the client response for a <see cref="GetFileInfoAsync"/> request.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        public void HandleFileInfoResponse(string agentName, Guid id, (bool Exists, long Length) response)
        {
            var key = new WaitKey(nameof(GetFileInfoAsync), agentName, id);

            if (!Waiter.IsWaitingFor(key))
            {
                var msg = $"A file info response from Agent {agentName} matching Id {id} was not expected";
                Log.Warning(msg);
                throw new NotFoundException(msg);
            }

            Waiter.Complete(key, response);
        }

        /// <summary>
        ///     Handles the client response for a <see cref="GetFileStreamAsync"/> request, returning when the corresponding file
        ///     upload is complete.
        /// </summary>
        /// <remarks>
        ///     Assumes <see cref="TryValidateFileStreamResponseCredential"/> has previously been used to ensure the Id and agent match.
        /// </remarks>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The ID of the request.</param>
        /// <param name="response">The client response to the request.</param>
        /// <returns>The operation context.</returns>
        public async Task HandleFileStreamResponseAsync(string agentName, Guid id, Stream response)
        {
            var streamKey = new WaitKey(nameof(GetFileStreamAsync), agentName, id);

            if (!Waiter.IsWaitingFor(streamKey))
            {
                var msg = $"A file stream response matching Id {id} is not expected";
                Log.Warning(msg);
                throw new NotFoundException(msg);
            }

            // create a wait (but do not await it) for the completion of the upload to the remote client. we need to await this to
            // keep execution in the body of the API controller that provided the stream, in order to keep the stream open for the
            // duration of the remote transfer. omit the id from the key, the caller doesn't know it.
            var completionKey = new WaitKey(nameof(HandleFileStreamResponseAsync), agentName, id);
            var completion = Waiter.WaitIndefinitely(completionKey);

            // complete the wait that's waiting for the stream, so we can send the stream back to the caller of GetFileStream this
            // sends execution back to the body of GetFileStream and the upload will begin transferring data
            Waiter.Complete(streamKey, response);

            // wait for the caller of GetFileStream to report that the upload/stream is complete this is the wait that keeps the
            // HTTP handler running
            await completion;
        }

        /// <summary>
        ///     Handles incoming share uploads.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The ID obtained by the caller prior to uploading.</param>
        /// <param name="shares">The list of shares provided.</param>
        /// <param name="filename">The filename of the temporary file containing the upload.</param>
        /// <returns>The operation context.</returns>
        public Task HandleShareUploadAsync(string agentName, Guid id, IEnumerable<Share> shares, string filename)
        {
            Log.Information("Loading shares from agent {Agent}", agentName);

            using var repository = ShareRepositoryFactory.CreateFromFile(filename);

            if (!repository.TryValidate(out var problems))
            {
                throw new ShareValidationException("Invalid database: " + string.Join(", ", problems));
            }

            var destinationRepository = ShareRepositoryFactory.CreateFromHost(agentName);

            destinationRepository.RestoreFrom(repository);

            Shares.AddOrUpdateHost(new Host(agentName, shares));

            Log.Information("Shares from agent {Agent} ready.", agentName);

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Notifies connected agents that a file download has completed.
        /// </summary>
        /// <param name="filename">The filename of the completed file, relative to the downloads directory.</param>
        /// <returns>The operation context.</returns>
        public Task NotifyFileDownloadCompleteAsync(string filename)
        {
            if (!OptionsMonitor.CurrentValue.Relay.Enabled ||
                !new[] { RelayMode.Controller, RelayMode.Debug }.Contains(OptionsMonitor.CurrentValue.Relay.Mode.ToEnum<RelayMode>()))
            {
                return Task.CompletedTask;
            }

            var agents = RegisteredAgentDictionary.Values;

            if (!agents.Any())
            {
                return Task.CompletedTask;
            }

            // ensure filename is relative to the local download directory and make sure it doesn't start with a slash
            filename = filename
                .ReplaceFirst(OptionsMonitor.CurrentValue.Directories.Downloads, string.Empty)
                .TrimStart(new[] { '/', '\\' });

            async Task Notify((string ConnectionId, Agent Agent) record)
            {
                try
                {
                    var id = Guid.NewGuid();

                    MemoryCache.Set(GetDownloadTokenCacheKey(filename, id), record.Agent.Name, TimeSpan.FromMinutes(10));
                    Log.Debug("Cached file download token {Token} for agent {Agent}", id, record.Agent.Name);

                    await RelayHub.Clients.Client(record.ConnectionId).NotifyFileDownloadCompleted(filename, id);
                }
                catch
                {
                    Log.Warning("Failed to notify agent {Agent} of download completion for {Filename}", record.Agent.Name, filename);
                }
            }

            Log.Information("Notifying agents of download completion for {Filename}", filename);

            return Task.WhenAll(RegisteredAgentDictionary.Values.Select(Notify));
        }

        /// <summary>
        ///     Notifies the caller of <see cref="GetFileStreamAsync"/> of a failure to obtain a file stream from the requested agent.
        /// </summary>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">The remote exception that caused the failure.</param>
        public void NotifyFileStreamException(Guid id, Exception exception)
        {
            var key = new WaitKey(nameof(GetFileStreamAsync), id);
            Waiter.Throw(key, exception);
        }

        /// <summary>
        ///     Registers the specified <paramref name="agent"/> name with the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="agent">The agent.</param>
        public void RegisterAgent(string connectionId, Agent agent)
        {
            RegisteredAgentDictionary.AddOrUpdate(agent.Name, addValue: (connectionId, agent), updateValueFactory: (k, v) => (connectionId, agent));
            State.SetValue(state => state with { Agents = RegisteredAgents });
        }

        /// <summary>
        ///     Safely attempts to close a stream obtained with <see cref="GetFileStreamAsync"/>.
        /// </summary>
        /// <param name="agentName">The name of the agent.</param>
        /// <param name="id">The unique ID for the stream.</param>
        /// <param name="exception">If the transfer associated with the stream failed, the exception that caused the failure.</param>
        public void TryCloseFileStream(string agentName, Guid id, Exception exception = default)
        {
            var key = new WaitKey(nameof(HandleFileStreamResponseAsync), agentName, id);

            if (exception != default)
            {
                Waiter.Throw(key, exception);
                return;
            }

            Waiter.Complete(key);
        }

        /// <summary>
        ///     Attempts to remove the registration for the specified <paramref name="connectionId"/>.
        /// </summary>
        /// <param name="connectionId">The ID of the connection.</param>
        /// <param name="record">The registration record, if one was removed.</param>
        /// <returns>A value indicating whether a registration was removed.</returns>
        public bool TryDeregisterAgent(string connectionId, out (string ConnectionId, Agent Agent) record)
        {
            record = default;
            var removed = false;

            if (TryGetAgentRegistration(connectionId, out var found))
            {
                Log.Information("Unloading shares for agent {Agent}", found.Agent.Name);

                Shares.TryRemoveHost(found.Agent.Name);
                removed = RegisteredAgentDictionary.TryRemove(found.Agent.Name, out record);

                Log.Information("Shares for agent {Agent} unloaded", found.Agent.Name);
            }

            State.SetValue(state => state with { Agents = RegisteredAgents });
            return removed;
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
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the response is valid.</returns>
        public bool TryValidateAuthenticationCredential(string connectionId, string agentName, string credential)
        {
            if (!MemoryCache.TryGetValue(GetAuthTokenCacheKey(connectionId), out var challengeToken))
            {
                Log.Debug("Auth challenge for {Id} failed: no challenge token cached for ID", connectionId);
                return false;
            }

            var agentOptions = OptionsMonitor.CurrentValue.Relay.Agents.Values.SingleOrDefault(a => a.InstanceName == agentName);

            if (agentOptions == default)
            {
                Log.Debug("Auth challenge for {Id} failed: no configuration for agent '{Agent}'", connectionId, agentName);
                return false;
            }

            var key = Pbkdf2.GetKey(password: agentOptions.Secret, salt: agentName, length: 48);
            var tokenBytes = System.Text.Encoding.UTF8.GetBytes((string)challengeToken);
            var expectedCredential = Aes.Encrypt(tokenBytes, key).ToBase62();

            if (expectedCredential != credential)
            {
                Log.Debug("Validation failed: Supplied credential {Credential} does not match expected credential {Expected}", credential, expectedCredential);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Attempts to validate the file download response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="filename">The name of the file being downloaded.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        public bool TryValidateFileDownloadCredential(Guid token, string agentName, string filename, string credential)
        {
            return TryValidateCredential(token.ToString(), agentName, credential, GetDownloadTokenCacheKey(filename, token), suppressRemoval: true);
        }

        /// <summary>
        ///     Attempts to validate the file stream response credential associated with the specified <paramref name="token"/>.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="agentName">The name of the responding agent.</param>
        /// <param name="filename">The name of the file being uploaded.</param>
        /// <param name="credential">The response credential.</param>
        /// <returns>A value indicating whether the credential is valid.</returns>
        public bool TryValidateFileStreamResponseCredential(Guid token, string agentName, string filename, string credential)
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

        private void Configure(Options options)
        {
            SyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(options.Relay.ToJson());
                var controllerOptionsHash = Compute.Sha1Hash(options.Relay.Controller.ToJson());

                if (optionsHash == LastOptionsHash || controllerOptionsHash == LastControllerOptionsHash)
                {
                    return;
                }

                if (options.Relay.Enabled)
                {
                    var mode = options.Relay.Mode.ToEnum<RelayMode>();

                    if (mode == RelayMode.Controller)
                    {
                        State.SetValue(state => state with
                        {
                            Mode = mode,
                            Agents = RegisteredAgents,
                        });
                    }
                    else
                    {
                        // the controller changed. disconnect and throw away the client and create a new one
                        Client = new RelayClient(Shares, OptionsMonitor, HttpClientFactory);
                        Client.StateMonitor.OnChange(clientState
                            => State.SetValue(state => state with { Controller = state.Controller with { State = clientState.Current } }));

                        State.SetValue(state => state with
                        {
                            Mode = mode,
                            Controller = new RelayControllerState()
                            {
                                Address = options.Relay.Controller.Address,
                                State = Client.StateMonitor.CurrentValue,
                            },
                        });
                    }
                }

                LastOptionsHash = optionsHash;
                LastControllerOptionsHash = controllerOptionsHash;
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private string GetAuthTokenCacheKey(string connectionId) => $"{connectionId}.auth";

        private string GetDownloadTokenCacheKey(string filename, Guid token) => $"{filename}.{token}.download";

        private string GetFileTokenCacheKey(string filename, Guid token) => $"{filename}.{token}.file";

        private string GetShareTokenCacheKey(Guid token) => $"{token}.share";

        private bool TryValidateCredential(string token, string agentName, string credential, string cacheKey, bool suppressRemoval = false)
        {
            try
            {
                var agentOptions = OptionsMonitor.CurrentValue.Relay.Agents.Values.SingleOrDefault(a => a.InstanceName == agentName);

                if (agentOptions == default)
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

                var key = Pbkdf2.GetKey(password: agentOptions.Secret, salt: agentName, length: 48);
                var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
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
                if (!suppressRemoval)
                {
                    MemoryCache.Remove(cacheKey);
                }
            }
        }
    }
}