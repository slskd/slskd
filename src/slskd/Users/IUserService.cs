// <copyright file="IUserService.cs" company="slskd Team">
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

namespace slskd.Users
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    /// <summary>
    ///     Provides information and operations for network peers.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        ///     Gets the list of tracked users.
        /// </summary>
        IReadOnlyList<User> Users { get; }

        /// <summary>
        ///     Gets the list of watched usernames.
        /// </summary>
        IReadOnlyList<string> WatchedUsernames { get; }

        /// <summary>
        ///     Gets the name of the group for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The group for the specified username.</returns>
        string GetGroup(string username);

        /// <summary>
        ///     Retrieves peer <see cref="Info"/>.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved info.</returns>
        Task<Info> GetInfoAsync(string username);

        /// <summary>
        ///     Retrieves a peer's IP endpoint, including their IP address and listen port.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved endpoint.</returns>
        Task<IPEndPoint> GetIPEndPointAsync(string username);

        /// <summary>
        ///     Retrieves the current <see cref="Status"/> of a peer.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved status.</returns>
        Task<Status> GetStatusAsync(string username);

        /// <summary>
        ///     Grants the specified peer the specified number of privilege days.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <param name="days">The number of days to grant.</param>
        /// <returns>The operation context.</returns>
        Task GrantPrivilegesAsync(string username, int days);

        /// <summary>
        ///     Retrieves a value indicating whether the specified peer is privileged.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>A value indicating whether the specified peer is privileged.</returns>
        Task<bool> IsPrivilegedAsync(string username);

        /// <summary>
        ///     Gets a value indicating whether the specified <paramref name="username"/> is watched.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>A value indicating whether the username is watched.</returns>
        bool IsWatched(string username);

        /// <summary>
        ///     Adds the specified username to the server-side user list.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The operation context.</returns>
        Task WatchAsync(string username);
    }
}