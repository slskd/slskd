// <copyright file="IRelayClient.cs" company="slskd Team">
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

namespace slskd.Relay
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Relay client (agent).
    /// </summary>
    public interface IRelayClient : IDisposable
    {
        /// <summary>
        ///     Gets the client state.
        /// </summary>
        IStateMonitor<RelayClientState> StateMonitor { get; }

        /// <summary>
        ///     Starts the client and connects to the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stops the client and disconnects from the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Synchronizes state with the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        Task SynchronizeAsync(CancellationToken cancellationToken = default);
    }
}
