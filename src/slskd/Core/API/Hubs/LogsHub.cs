// <copyright file="LogsHub.cs" company="slskd Team">
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

namespace slskd.Core.API
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;

    public static class LogHubMethods
    {
        public static readonly string Buffer = "BUFFER";
        public static readonly string Log = "LOG";
    }

    /// <summary>
    ///     Extension methods for the logs SignalR hub.
    /// </summary>
    public static class LogHubExtensions
    {
        /// <summary>
        ///     Broadcast a log record.
        /// </summary>
        /// <param name="hub">The hub.</param>
        /// <param name="record">The log record to broadcast.</param>
        /// <returns>The operation context.</returns>
        public static Task EmitLogAsync(this IHubContext<LogsHub> hub, LogRecord record)
        {
            return hub.Clients.All.SendAsync(LogHubMethods.Log, record);
        }
    }

    /// <summary>
    ///     The logs SignalR hub.
    /// </summary>
    [Authorize(Policy = AuthPolicy.Any)]
    public class LogsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync(LogHubMethods.Buffer, Program.LogBuffer);
        }
    }
}