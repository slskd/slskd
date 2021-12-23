// <copyright file="SearchHub.cs" company="slskd Team">
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

namespace slskd.Search.API
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;

    public static class SearchHubMethods
    {
        public static readonly string Response = "RESPONSE";
        public static readonly string Update = "UPDATE";
    }

    /// <summary>
    ///     Extension methods for the search SignalR hub.
    /// </summary>
    public static class SearchHubExtensions
    {
        /// <summary>
        ///     Broadcast an update for a search.
        /// </summary>
        /// <param name="hub">The hub.</param>
        /// <param name="search">The search to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastUpdateAsync(this IHubContext<SearchHub> hub, Search search)
        {
            return hub.Clients.All.SendAsync(SearchHubMethods.Update, search);
        }

        /// <summary>
        ///     Broadcast the present application options.
        /// </summary>
        /// <param name="hub">The hub.</param>
        /// <param name="searchId">The ID of the search associated with the response.</param>
        /// <param name="response">The response to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task BroadcastResponseAsync(this IHubContext<SearchHub> hub, Guid searchId, Soulseek.SearchResponse response)
        {
            return hub.Clients.All.SendAsync(SearchHubMethods.Response, new { searchId, response });
        }
    }

    /// <summary>
    ///     The search SignalR hub.
    /// </summary>
    [Authorize]
    public class SearchHub : Hub
    {
        public SearchHub(
            IStateMonitor<State> stateMonitor,
            IOptionsMonitor<Options> optionsMonitor)
        {
            StateMonitor = stateMonitor;
            OptionsMonitor = optionsMonitor;
        }

        private IStateMonitor<State> StateMonitor { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
    }
}