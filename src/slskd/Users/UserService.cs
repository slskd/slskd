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

using Microsoft.Extensions.Options;

namespace slskd.Users
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
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
        /// <param name="optionsMonitor"></param>
        public UserService(
            ISoulseekClient soulseekClient,
            IDbContextFactory<UserDbContext> contextFactory,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Client = soulseekClient;
            ContextFactory = contextFactory;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private ISoulseekClient Client { get; }
        private IDbContextFactory<UserDbContext> ContextFactory { get; }
        private string LastOptionsHash { get; set; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<UserService>();
        private ConcurrentDictionary<string, string> Map { get; set; } = new ConcurrentDictionary<string, string>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }

        /// <summary>
        ///     Gets the name of the group for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The group for the specified username.</returns>
        public string GetGroup(string username)
        {
            return Map.GetValueOrDefault(username ?? string.Empty, Application.DefaultGroup);
        }

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

        private void Configure(Options options)
        {
            var optionsHash = Compute.Sha1Hash(options.Groups.UserDefined.ToJson());

            if (optionsHash == LastOptionsHash)
            {
                return;
            }

            var map = new ConcurrentDictionary<string, string>();

            // sort by priority, ascending
            foreach (var group in options.Groups.UserDefined.OrderBy(kvp => kvp.Value.Upload.Priority))
            {
                foreach (var user in group.Value.Members)
                {
                    // if the key already exists, leave the existing entry. if a user appears in more than one group, the higher
                    // priority (lower numbered) group is their effective group.
                    map.TryAdd(user, group.Key);
                }
            }

            Map = map;
            LastOptionsHash = optionsHash;
        }
    }
}