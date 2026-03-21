// <copyright file="RelayHub.cs" company="slskd Team">
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
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.AspNetCore.SignalR;
    using NetTools;
    using Serilog;

    /// <summary>
    ///     Methods for the <see cref="RelayHub"/>.
    /// </summary>
    public interface IRelayHub
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
        /// <param name="startOffset">The starting offset for the transfer.</param>
        /// <param name="id">The unique identifier for the request.</param>
        /// <returns>The operation context.</returns>
        Task RequestFileUpload(string filename, long startOffset, Guid id);

        /// <summary>
        ///     Notifies the agent that the download of the specified <paramref name="filename"/> is complete and that the file is ready for downloading.
        /// </summary>
        /// <param name="filename">The name of the newly downloaded file.</param>
        /// <param name="id">The unique identifier for the request.</param>
        /// <returns>The operation context.</returns>
        Task NotifyFileDownloadCompleted(string filename, Guid id);
    }

    /// <summary>
    ///     The relay SignalR hub.
    /// </summary>
    [Authorize]
    public class RelayHub : Hub<IRelayHub>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RelayHub"/> class.
        /// </summary>
        /// <param name="relayService"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="optionsAtStartup"></param>
        public RelayHub(
            IRelayService relayService,
            IOptionsMonitor<Options> optionsMonitor,
            OptionsAtStartup optionsAtStartup)
        {
            Relay = relayService;
            OptionsMonitor = optionsMonitor;
            OptionsAtStartup = optionsAtStartup;
        }

        private ILogger Log { get; } = Serilog.Log.ForContext<RelayHub>();
        private IRelayService Relay { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private RelayMode OperationMode => OptionsAtStartup.Relay.Mode.ToEnum<RelayMode>();
        private IPAddress RemoteIpAddress => Context.Features.Get<IHttpConnectionFeature>().RemoteIpAddress;

        /// <summary>
        ///     Executed when a new connection is established.
        /// </summary>
        /// <returns></returns>
        public override async Task OnConnectedAsync()
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Controller, RelayMode.Debug }.Contains(OperationMode))
            {
                Log.Debug("Agent connection {Id} from {IP} aborted; Relay is not enabled, or is not in Controller mode", Context.ConnectionId, RemoteIpAddress);
                Context.Abort();
            }

            var token = Relay.GenerateAuthenticationChallengeToken(Context.ConnectionId);

            Log.Information("Agent connection {Id} from {IP} established. Sending authentication challenge {Token}...", Context.ConnectionId, RemoteIpAddress, token);
            await Clients.Caller.Challenge(token);
        }

        /// <summary>
        ///     Executed when a connection is disconnected.
        /// </summary>
        /// <param name="exception">The Exception that caused the disconnect.</param>
        /// <returns></returns>
        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (Relay.TryDeregisterAgent(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent {Agent} (connection {Id}) from {IP} disconnected", record.Agent.Name, Context.ConnectionId, RemoteIpAddress);
                Relay.TryDeregisterAgent(record.Agent.Name, out _);
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
            bool TryGetAgentConfig(string agent, out Options.RelayOptions.RelayAgentConfigurationOptions options)
            {
                var allAgents = OptionsMonitor.CurrentValue.Relay.Agents;
                var found = allAgents.Values.SingleOrDefault(a => a.InstanceName == agent);

                if (found != default)
                {
                    options = found;
                    return true;
                }

                Log.Warning("Unable to locate Agent config for '{Agent}' (configured Agents: {Agents})", agent, string.Join(", ", allAgents.Values.Select(a => a.InstanceName)));

                options = null;
                return false;
            }

            if (!TryGetAgentConfig(agent, out var agentOptions))
            {
                Log.Warning("Unauthorized login attempt from unknown Agent {Agent} (connection {Id}) from {IP}", agent, Context.ConnectionId, RemoteIpAddress);
                throw new UnauthorizedAccessException();
            }

            if (!agentOptions.Cidr.Split(',')
                .Select(cidr => IPAddressRange.Parse(cidr))
                .Any(range => range.Contains(RemoteIpAddress)))
            {
                Log.Warning("Unauthorized login attempt by Agent {Agent} (connection {Id}); remote IP address {IP} is not within the configured range {CIDR}", agent, Context.ConnectionId, RemoteIpAddress, agentOptions.Cidr);
                throw new UnauthorizedAccessException();
            }

            if (!Relay.TryValidateAuthenticationCredential(Context.ConnectionId, agent, challengeResponse))
            {
                Log.Warning("Unauthorized login attempt by Agent {Agent} (connection {Id}) from {IP}; authentication failed", agent, Context.ConnectionId, RemoteIpAddress);
                Relay.TryDeregisterAgent(Context.ConnectionId, out var _); // just in case!
                throw new UnauthorizedAccessException();
            }

            var remoteIp = Context.Features.Get<IHttpConnectionFeature>().RemoteIpAddress.ToString();
            var record = new Agent { Name = agent, IPAddress = remoteIp };

            Log.Information("Agent connection {Id} from {IP} authenticated as agent {Agent}", Context.ConnectionId, remoteIp, agent);
            Relay.RegisterAgent(Context.ConnectionId, record);
        }

        /// <summary>
        ///     Executed by the agent to initiate the share upload workflow by generating and retrieving a request token.
        /// </summary>
        /// <returns>The generated token.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when the agent is not fully authenticated.</exception>
        public Guid BeginShareUpload()
        {
            if (!Relay.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                // this can happen if the agent attempts to upload before logging in
                Log.Information("Agent connection {Id} from {IP} requested a share upload token, but is not registered.", Context.ConnectionId, RemoteIpAddress);
                throw new UnauthorizedAccessException();
            }

            var token = Relay.GenerateShareUploadToken(record.Agent.Name);
            Log.Information("Agent {Agent} (connection {Id}) from {IP} requested share upload token {Token}", record.Agent.Name, record.ConnectionId, RemoteIpAddress, token);
            return token;
        }

        /// <summary>
        ///     Executed by the agent to notify the controller that the agent was unable to upload the file requested by a call to <see cref="IRelayHub.RequestFileUpload"/>.
        /// </summary>
        /// <param name="id">The unique identifier of the request.</param>
        /// <param name="exception">The Exception that caused the failure.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the agent is not fully authenticated.</exception>
        public void NotifyFileUploadFailed(Guid id, Exception exception)
        {
            if (!Relay.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent connection {Id} from {IP} attempted to report a failed upload, but is not registered.", Context.ConnectionId, RemoteIpAddress);
                throw new UnauthorizedAccessException();
            }

            Log.Warning("Agent {Agent} (connection {ConnectionId}) from {IP} reported upload failure for {Id}: {Message}", record.Agent, Context.ConnectionId, RemoteIpAddress, id, exception.Message);

            Relay.NotifyFileStreamException(record.Agent.Name, id, exception);
        }

        /// <summary>
        ///     Executed by the agent to return the response to a call to <see cref="IRelayHub.RequestFileInfo"/>.
        /// </summary>
        /// <param name="id">The unique identifier for the request.</param>
        /// <param name="exists">A value indicating whether the requested file exists on the agent's filesystem.</param>
        /// <param name="length">The length of the file, or 0 if the file does not exist.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the agent is not fully authenticated.</exception>
        public void ReturnFileInfo(Guid id, bool exists, long length)
        {
            if (!Relay.TryGetAgentRegistration(Context.ConnectionId, out var record))
            {
                Log.Warning("Agent connection {Id} from {IP} attempted to return file information, but is not registered.", Context.ConnectionId, RemoteIpAddress);
                throw new UnauthorizedAccessException();
            }

            Log.Information("Agent {Agent} (connection {ConnectionId}) from {IP} returned file info for {Id}; exists: {Exists}, length: {Length}", record.Agent.Name, Context.ConnectionId, RemoteIpAddress, id, exists, length);

            Relay.HandleFileInfoResponse(record.Agent.Name, id, (exists, length));
        }
    }
}