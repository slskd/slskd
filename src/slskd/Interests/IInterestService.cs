// <copyright file="IInterestService.cs" company="JP Dillingham">
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
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Shares the configured interests with the server and finds users and interests through them.
    /// </summary>
    public interface IInterestService
    {
        /// <summary>
        ///     Retrieves the interests of the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the user.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the user's interests.</returns>
        Task<UserInterests> GetUserInterestsAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves users who like the specified <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The interest.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the usernames of the users.</returns>
        Task<IReadOnlyList<string>> GetItemSimilarUsersAsync(string item, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves users with interests similar to the current user's.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the users.</returns>
        Task<IReadOnlyList<SimilarUser>> GetSimilarUsersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves interests related to the specified <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The interest.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the recommendations.</returns>
        Task<Recommendations> GetItemRecommendationsAsync(string item, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves interests recommended from the current user's interests.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the recommendations.</returns>
        Task<Recommendations> GetRecommendationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Retrieves the most popular interests on the network.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The operation context, including the recommendations.</returns>
        Task<Recommendations> GetGlobalRecommendationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Sends any changes to the configured interests to the server.
        /// </summary>
        /// <param name="resend">A value indicating whether to send every interest, as after logging in.</param>
        /// <returns>The operation context.</returns>
        Task SyncAsync(bool resend = false);
    }
}
