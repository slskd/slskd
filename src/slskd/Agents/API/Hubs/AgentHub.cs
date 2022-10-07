// <copyright file="AgentHub.cs" company="slskd Team">
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

namespace slskd.Agents
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;
    using slskd;

    public static class AgentHubMethods
    {
        public static readonly string RequestFile = "REQUEST_FILE";
    }

    /// <summary>
    ///     Extension methods for the agent SignalR hub.
    /// </summary>
    public static class AgentHubExtensions
    {
        public static Task RequestFileAsync(this IHubContext<AgentHub> hub, string agent, string filename, Guid id)
        {
            // todo: how to send this to the specified agent?
            return hub.Clients.All.SendAsync(AgentHubMethods.RequestFile, filename, id);
        }
    }

    /// <summary>
    ///     The agent SignalR hub.
    /// </summary>
    [Authorize]
    public class AgentHub : Hub
    {
        public AgentHub(
            IStateMonitor<State> stateMonitor,
            IOptionsMonitor<Options> optionsMonitor)
        {
            StateMonitor = stateMonitor;
            OptionsMonitor = optionsMonitor;
        }

        private IStateMonitor<State> StateMonitor { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }

        public override async Task OnConnectedAsync()
        {
            // upon connection we need to do a little handshaking to establish
            // that this is a registered agent, and which agent it is. whoever is calling
            // has an api key or jwt, but users might disable auth, so literally anyone could
            // connect. they should at least say who they are, and we'd allow or disallow
            // todo: how to authenticate?
            // todo: how to identify?
        }
    }
}