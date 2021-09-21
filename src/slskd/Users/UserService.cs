// <copyright file="UserService.cs" company="slskd Team">
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
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Soulseek;

    /// <summary>
    ///     Provides information and operations for network peers.
    /// </summary>
    public class UserService : IUserService
    {
        private const int CacheTTLSeconds = 300;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="contextFactory">The database context to use.</param>
        /// <param name="log">The logger.</param>
        public UserService(
            ISoulseekClient soulseekClient,
            IDbContextFactory<UserDbContext> contextFactory,
            ILogger<UserService> log)
        {
            Client = soulseekClient;
            ContextFactory = contextFactory;
            Log = log;
        }

        private ISoulseekClient Client { get; }
        private IDbContextFactory<UserDbContext> ContextFactory { get; }
        private ILogger<UserService> Log { get; set; }

        /// <summary>
        ///     Retrieves peer <see cref="Info"/>.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <param name="bypassCache">A value indicating whether the local cache should be bypassed.</param>
        /// <returns>The cached or retrieved info.</returns>
        public async Task<Info> GetInfoAsync(string username, bool bypassCache = false)
        {
            using var context = ContextFactory.CreateDbContext();

            var info = await context.Info.AsNoTracking().FirstOrDefaultAsync(peer => peer.Username == username);
            var peerExists = info != default;

            if (!bypassCache && peerExists && info.UpdatedAt < DateTime.UtcNow.AddSeconds(CacheTTLSeconds))
            {
                return info;
            }

            var soulseekUserInfo = await Client.GetUserInfoAsync(username);
            info = Info.FromSoulseekUserInfo(username, soulseekUserInfo);

            if (peerExists)
            {
                context.Update(info);
            }
            else
            {
                context.Add(info);
            }

            await context.SaveChangesAsync();

            return info;
        }

        /// <summary>
        ///     Retrieves a peer's IP endpoint, including their IP address and listen port.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved endpoint.</returns>
        public Task<IPEndPoint> GetIPEndPointAsync(string username)
        {
            return Client.GetUserEndPointAsync(username);
        }

        /// <summary>
        ///     Retrieves the current <see cref="Status"/> of a peer.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved status.</returns>
        public async Task<Status> GetStatusAsync(string username)
        {
            var soulseekStatus = await Client.GetUserStatusAsync(username);

            return Status.FromSoulseekUserStatus(soulseekStatus);
        }

        /// <summary>
        ///     Grants the specified peer the specified number of privilege days.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <param name="days">The number of days to grant.</param>
        /// <returns>The operation context.</returns>
        public Task GrantPrivilegesAsync(string username, int days)
            => Client.GrantUserPrivilegesAsync(username, days);

        /// <summary>
        ///     Retrieves a value indicating whether the specified peer is privileged.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>A value indicating whether the specified peer is privileged.</returns>
        public Task<bool> IsPrivilegedAsync(string username)
            => Client.GetUserPrivilegedAsync(username);

        /// <summary>
        ///     Adds the specified username to the server-side user list.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The operation context.</returns>
        public async Task WatchAsync(string username)
        {
            await Client.AddUserAsync(username);
        }
    }
}