// <copyright file="ApplicationHub.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ https://slskd.org
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, either version 3 of the License, or
//   │ (at your option) any later version.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to
//   │ Section 7 of the GNU Affero General Public License.  See the
//   │ LICENSE file in the root directory of this project for the
//   │ complete terms and conditions.
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Core.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;

    public static class ApplicationHubMethods
    {
        public static readonly string State = "STATE";
        public static readonly string Options = "OPTIONS";
    }

    /// <summary>
    ///     Extension methods for the application SignalR hub.
    /// </summary>
    public static class ApplicationHubExtensions
    {
        /// <summary>
        ///     Broadcast the present application state.
        /// </summary>
        /// <param name="hub">The hub.</param>
        /// <param name="state">The state to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastStateAsync(this IHubContext<ApplicationHub> hub, State state)
        {
            return hub.Clients.All.SendAsync(ApplicationHubMethods.State, state);
        }

        /// <summary>
        ///     Broadcast the present application options.
        /// </summary>
        /// <param name="hub">The hub.</param>
        /// <param name="options">The options to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastOptionsAsync(this IHubContext<ApplicationHub> hub, Options options)
        {
            return hub.Clients.All.SendAsync(ApplicationHubMethods.Options, options.Redact());
        }
    }

    /// <summary>
    ///     The application SignalR hub.
    /// </summary>
    [Authorize(Policy = AuthPolicy.Any)]
    public class ApplicationHub : Hub
    {
        public ApplicationHub(
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
            await Clients.Caller.SendAsync(ApplicationHubMethods.State, StateMonitor.CurrentValue);
            await Clients.Caller.SendAsync(ApplicationHubMethods.Options, OptionsMonitor.CurrentValue.Redact());
        }
    }
}