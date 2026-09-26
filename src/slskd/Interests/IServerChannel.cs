// <copyright file="IServerChannel.cs" company="JP Dillingham">
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

namespace slskd.Interests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Sends and receives Soulseek server messages that the client library doesn't implement.
    /// </summary>
    public interface IServerChannel
    {
        /// <summary>
        ///     Sends the specified <paramref name="message"/> to the server.
        /// </summary>
        /// <param name="message">The message to send, including the length prefix.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context.</returns>
        Task SendAsync(byte[] message, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Sends the specified <paramref name="message"/> to the server and waits for the response with the specified
        ///     <paramref name="responseCode"/>.
        /// </summary>
        /// <remarks>
        ///     The server doesn't correlate responses with requests, so <paramref name="key"/> picks this request's response
        ///     out of those with the same code. It's compared with the key the <paramref name="keySelector"/> reads from each
        ///     response, and if no pending request's key matches, the response goes to the oldest pending request with
        ///     the same code.
        /// </remarks>
        /// <param name="message">The message to send, including the length prefix.</param>
        /// <param name="responseCode">The code of the expected response.</param>
        /// <param name="key">The value identifying the expected response, or null to accept any response with the code.</param>
        /// <param name="keySelector">A function reading the identifying value from a response payload.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the payload of the response, excluding the length and code.</returns>
        /// <exception cref="TimeoutException">Thrown when the server doesn't respond in time.</exception>
        Task<byte[]> RequestAsync(
            byte[] message,
            int responseCode,
            string key = null,
            Func<byte[], string> keySelector = null,
            CancellationToken cancellationToken = default);
    }
}
