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
    /// <remarks>
    ///     <para>
    ///         This class maintains a UserDictionary that acts as a non-expiring cache of information
    ///         collected about a user.  This includes their statistics (share counts, speed, etc), their status (privileged, etc)
    ///         and, if they are a member of a user-defined group, their group.
    ///     </para>
    ///     <para>
    ///         This class also maintains a WatchedUsernamesDictionary to keep track of which usernames have been
    ///         "watched" server side and for which we will therefore receive events when their status changes.
    ///     </para>
    ///     <para>
    ///         If a user's information is in the UserDictionary, it's because we requested it at some point.  If that user
    ///         is also "watched", we can assume that the data in the dictionary is up to date and will be kept so.
    ///     </para>
    ///     <para>
    ///         The data in the UserDictionary can continue to grow until -- unlikely -- it contains a record for every
    ///         user on or that was on the network at any point since the last client connect.  This is a calculated risk, roughly
    ///         knowing the size of the network, the size of the data being stored, and balanced against the consequences of not having
    ///         a user's data when it is needed (for queue positioning, speed limits, etc).
    ///     </para>
    ///     <para>
    ///         The <see cref="GetGroup(string)"/> method acts on cached data _only_.  This method should be called within hot paths,
    ///         such as a transfer governor or from the upload queue.  We care more that it is fast than if it is stale.  If no data for the
    ///         requested user is cached, that user is assumed to be in the default group.
    ///     </para>
    ///     <para>
    ///         The <see cref="GetOrFetchGroupAsync(string, bool)"/> method is similar to <see cref="GetGroup(string)"/>, except that if
    ///         the requested user is not cached, it will fetch the user's data and cache it before returning.  This method accepts an optional
    ///         parameter that can be used to force a "refresh" of the requested user's data, useful for times when we want the latest data,
    ///         and can afford to wait for it.
    ///     </para>
    /// </remarks>
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
        /// <remarks>The group name is fetched from cached data, and lookups should always be fast.</remarks>
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
        ///     Gets the name of the group for the specified <paramref name="username"/>, or, if the user's information isn't
        ///     cached, fetches and caches the user's information from the server, then returns the group.
        /// </summary>
        /// <remarks>The fetch of fresh data can be forced by specifying <paramref name="forceFetch"/> = true.</remarks>
        /// <param name="username">The username of the peer.</param>
        /// <param name="forceFetch">
        ///     A value determining whether the user's information should be fetched from the server, regardless of local cache.
        /// </param>
        /// <returns>The group for the specified username.</returns>
        Task<string> GetOrFetchGroupAsync(string username, bool forceFetch = false);

        /// <summary>
        ///     Retrieves the current <see cref="Statistics"/> of a peer, and caches the result.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved statistics.</returns>
        Task<Statistics> GetStatisticsAsync(string username);

        /// <summary>
        ///     Retrieves the current <see cref="Status"/> of a peer, and caches the result.
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
        ///     Gets a value indicating whether the specified <paramref name="username"/> and/or <paramref name="ipAddress"/> are blacklisted.
        /// </summary>
        /// <param name="username">The username to check.</param>
        /// <param name="ipAddress">The IPAddress to check, if available.</param>
        /// <returns>A value indicating whether the specified user and/or IP are blacklisted.</returns>
        bool IsBlacklisted(string username, IPAddress ipAddress = null);

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

        /// <summary>
        ///     Gets the profile picture as a byte array if it exists.
        /// </summary>
        /// <param name="profilePicture">The profile picture path to resolve.</param>
        /// <returns>The profile picture as a byte array if it exists and can be read, null otherwise.</returns>
        byte[] GetProfilePicture(string profilePicture);
    }
}