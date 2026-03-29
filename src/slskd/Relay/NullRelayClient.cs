// <copyright file="NullRelayClient.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Relay
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     A non-operable Relay client.
    /// </summary>
    public class NullRelayClient : IRelayClient
    {
        /// <summary>
        ///     Gets the client state.
        /// </summary>
        public IStateMonitor<RelayClientState> StateMonitor { get; set; } = new ManagedState<RelayClientState>();

        private bool Disposed { get; set; }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Starts the client and connects to the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        ///     Stops the client and disconnects from the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        ///     Synchronizes state with the controller.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        public Task SynchronizeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                Disposed = true;
            }
        }
    }
}