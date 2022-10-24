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

    public interface INetworkHub
    {
        Task Challenge(string token);
        Task RequestFile(string filename, Guid id);
        Task RequestFileInfo(string filename, Guid id);
    }

    /// <summary>
    ///     The network SignalR hub.
    /// </summary>
    [Authorize]
    public class NetworkHub : Hub<INetworkHub>
    {
        public NetworkHub(
            INetworkService networkService)
        {
            Network = networkService;
        }

        private INetworkService Network { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();

        public override async Task OnConnectedAsync()
        {
            var token = Network.GenerateAuthenticationChallengeToken(Context.ConnectionId);

            Log.Information("Agent connection {Id} established. Sending authentication challenge {Token}...", Context.ConnectionId, token);
            await Clients.Caller.Challenge(token);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (Network.TryDeregisterAgent(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent {Agent} (connection {Id}) disconnected", record.Agent.Name, Context.ConnectionId);
            }

            return Task.CompletedTask;
        }

        public bool Login(string agent, string challengeResponse)
        {
            if (Network.TryValidateAuthenticationChallengeResponse(Context.ConnectionId, agent, challengeResponse))
            {
                var remoteIp = Context.Features.Get<IHttpConnectionFeature>().RemoteIpAddress.ToString();
                var record = new Agent { Name = agent, ConnectedAt = DateTime.UtcNow, IPAddress = remoteIp };

                Log.Information("Agent connection {Id} ({IP}) authenticated as agent {Agent}", Context.ConnectionId, remoteIp, agent);
                Network.RegisterAgent(Context.ConnectionId, record);
                return true;
            }

            Log.Information("Agent connection {Id} authentication failed", Context.ConnectionId);
            Network.TryDeregisterAgent(Context.ConnectionId, out var _); // just in case!
            return false;
        }

        public Guid GetShareUploadToken()
        {
            if (Network.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                var token = Network.GenerateShareUploadToken(record.Agent.Name);
                Log.Information("Agent {Agent} (connection {Id}) requested share upload token {Token}", record.Agent.Name, record.ConnectionId, token);
                return token;
            }

            // this can happen if the agent attempts to upload before logging in
            Log.Information("Agent connection {Id} requested a share upload token, but is not registered.", Context.ConnectionId);
            throw new UnauthorizedAccessException();
        }

        public void NotifyUploadFailed(Guid id, Exception exception)
        {
            Network.NotifyFileStreamException(id, exception);
        }

        public void ReturnFileInfo(Guid id, bool exists, long length)
        {
            if (!Network.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent connection {Id} responded to a file info request with Id {Id}, but is not registered.", Context.ConnectionId, id);
                return;
            }

            Network.HandleFileInfoResponse(record.Agent.Name, id, (exists, length));
        }
    }
}