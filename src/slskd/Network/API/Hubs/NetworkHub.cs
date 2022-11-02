// <copyright file="NetworkHub.cs" company="slskd Team">
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

namespace slskd.Network
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.SignalR;
    using Serilog;

    /// <summary>
    ///     Methods for the <see cref="NetworkHub"/>.
    /// </summary>
    public interface INetworkHub
    {
        /// <summary>
        ///     Sends an authentication challenge token to the newly connected agent.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns>The operation context.</returns>
        Task Challenge(string token);

        /// <summary>
        ///     Requests information about the specified <paramref name="filename"/> from the agent.
        /// </summary>
        /// <param name="filename">The name of the file.</param>
        /// <param name="id">The unique identifier for the request.</param>
        /// <returns>The operation context.</returns>
        Task RequestFileInfo(string filename, Guid id);

        /// <summary>
        ///     Requests the specified <paramref name="filename"/> from the agent.
        /// </summary>
        /// <param name="filename">The name of the file.</param>
        /// <param name="id">The unique identifier for the request.</param>
        /// <returns>The operation context.</returns>
        Task RequestFileUpload(string filename, Guid id);
    }

    /// <summary>
    ///     The network SignalR hub.
    /// </summary>
    [Authorize]
    public class NetworkHub : Hub<INetworkHub>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="NetworkHub"/> class.
        /// </summary>
        /// <param name="networkService"></param>
        public NetworkHub(
            INetworkService networkService)
        {
            Network = networkService;
        }

        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();
        private INetworkService Network { get; }

        /// <summary>
        ///     Executed when a new connection is established.
        /// </summary>
        /// <returns></returns>
        public override async Task OnConnectedAsync()
        {
            var token = Network.GenerateAuthenticationChallengeToken(Context.ConnectionId);

            Log.Information("Agent connection {Id} established. Sending authentication challenge {Token}...", Context.ConnectionId, token);
            await Clients.Caller.Challenge(token);
        }

        /// <summary>
        ///     Executed when a connection is disconnected.
        /// </summary>
        /// <param name="exception">The Exception that caused the disconnect.</param>
        /// <returns></returns>
        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (Network.TryDeregisterAgent(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent {Agent} (connection {Id}) disconnected", record.Agent.Name, Context.ConnectionId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Executed by the agent after receipt of an authentication challenge token, shortly after the connection is established.
        /// </summary>
        /// <param name="agent">The agent's name.</param>
        /// <param name="challengeResponse">The response to the challenge token.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the challenge response is invalid.</exception>
        public void Login(string agent, string challengeResponse)
        {
            if (!Network.TryValidateAuthenticationCredential(Context.ConnectionId, agent, challengeResponse))
            {
                Log.Information("Agent connection {Id} authentication failed", Context.ConnectionId);
                Network.TryDeregisterAgent(Context.ConnectionId, out var _); // just in case!
                throw new UnauthorizedAccessException();
            }

            var remoteIp = Context.Features.Get<IHttpConnectionFeature>().RemoteIpAddress.ToString();
            var record = new Agent { Name = agent, ConnectedAt = DateTime.UtcNow, IPAddress = remoteIp };

            Log.Information("Agent connection {Id} ({IP}) authenticated as agent {Agent}", Context.ConnectionId, remoteIp, agent);
            Network.RegisterAgent(Context.ConnectionId, record);
        }

        /// <summary>
        ///     Initiates the share upload workflow by generating and retrieving a request token.
        /// </summary>
        /// <returns>The generated token.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when the agent is not fully authenticated.</exception>
        public Guid BeginShareUpload()
        {
            if (Network.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                // this can happen if the agent attempts to upload before logging in
                Log.Information("Agent connection {Id} requested a share upload token, but is not registered.", Context.ConnectionId);
                throw new UnauthorizedAccessException();
            }

            var token = Network.GenerateShareUploadToken(record.Agent.Name);
            Log.Information("Agent {Agent} (connection {Id}) requested share upload token {Token}", record.Agent.Name, record.ConnectionId, token);
            return token;
        }

        /// <summary>
        ///     Notifies the controller that the agent was unable to upload the file requested by a call to <see cref="INetworkHub.RequestFileUpload"/>.
        /// </summary>
        /// <param name="id">The unique identifier of the request.</param>
        /// <param name="exception">The Exception that caused the failure.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the agent is not fully authenticated.</exception>
        public void NotifyFileUploadFailed(Guid id, Exception exception)
        {
            if (Network.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent connection {Id} attempted to report a failed upload, but is not registered.", Context.ConnectionId);
                throw new UnauthorizedAccessException();
            }

            Log.Warning("Agent {Agent} (connection {ConnectionId}) reported upload failure for {Id}: {Message}", id, exception.Message);

            Network.NotifyFileStreamException(id, exception);
        }

        /// <summary>
        ///     Returns the response to a call to <see cref="INetworkHub.RequestFileInfo"/>.
        /// </summary>
        /// <param name="id">The unique identifier for the request.</param>
        /// <param name="exists">A value indicating whether the requested file exists on the agent's filesystem.</param>
        /// <param name="length">The length of the file, or 0 if the file does not exist.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the agent is not fully authenticated.</exception>
        public void ReturnFileInfo(Guid id, bool exists, long length)
        {
            if (!Network.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent connection {Id} attempted to return file information, but is not registered.", Context.ConnectionId);
                throw new UnauthorizedAccessException();
            }

            Log.Information("Agent {Agent} (connection {ConnectionId}) returned file info for {Id}; exists: {Exists}, length: {Length}", record.Agent.Name, Context.ConnectionId, id, exists, length);

            Network.HandleFileInfoResponse(record.Agent.Name, id, (exists, length));
        }
    }
}