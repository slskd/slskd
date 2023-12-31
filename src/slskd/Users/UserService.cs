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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Provides information and operations for network peers.
    /// </summary>
    public class UserService : IUserService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="stateMutator"></param>
        /// <param name="optionsMonitor"></param>
        public UserService(
            ISoulseekClient soulseekClient,
            IStateMutator<State> stateMutator,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Client = soulseekClient;

            StateMutator = stateMutator;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            // updates may be sent unsolicited from the server, so update when we get them. binding these events will cause
            // multiple redundant round trips when initially watching a user or when explicitly requesting via
            // GetStatus/GetStatistics. this is wasteful, but there's no functional side effect.
            Client.UserStatisticsChanged += (_, userStatistics) => UpdateStatistics(userStatistics.Username, userStatistics.ToStatistics());
            Client.UserStatusChanged += (sender, userStatus) =>
            {
                UpdateStatus(userStatus.Username, userStatus.ToStatus());

                // the server doesn't send statistics events by itself, so when a user status changes, fetch stats at the same time.
                _ = GetStatisticsAsync(userStatus.Username);
            };

            Client.Connected += (_, _) => Reset();
            Client.LoggedIn += (_, _) => Configure(OptionsMonitor.CurrentValue, force: true);

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the list of tracked users.
        /// </summary>
        public IReadOnlyList<User> Users => UserDictionary.Values.ToList().AsReadOnly();

        /// <summary>
        ///     Gets the list of watched usernames.
        /// </summary>
        public IReadOnlyList<string> WatchedUsernames => WatchedUsernamesDictionary.Keys.ToList().AsReadOnly();

        private ISoulseekClient Client { get; }
        private string LastOptionsHash { get; set; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<UserService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IStateMutator<State> StateMutator { get; }
        private ConcurrentDictionary<string, User> UserDictionary { get; set; } = new ConcurrentDictionary<string, User>();
        private ConcurrentDictionary<string, bool> WatchedUsernamesDictionary { get; set; } = new ConcurrentDictionary<string, bool>();

        /// <summary>
        ///     Retrieves peer <see cref="Info"/>.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved info.</returns>
        public async Task<Info> GetInfoAsync(string username)
        {
            var soulseekUserInfo = await Client.GetUserInfoAsync(username);
            return soulseekUserInfo.ToInfo();
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
        ///     Retrieves the current <see cref="Statistics"/> of a peer.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved statistics.</returns>
        public async Task<Statistics> GetStatisticsAsync(string username)
        {
            var soulseekStatistics = await Client.GetUserStatisticsAsync(username);
            var statistics = soulseekStatistics.ToStatistics();

            UpdateStatistics(username, statistics);

            return statistics;
        }

        /// <summary>
        ///     Retrieves the current <see cref="Status"/> of a peer.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The retrieved status.</returns>
        public async Task<Status> GetStatusAsync(string username)
        {
            var soulseekStatus = await Client.GetUserStatusAsync(username);
            var status = soulseekStatus.ToStatus();

            UpdateStatus(username, status);

            return status;
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
        public async Task<bool> IsPrivilegedAsync(string username)
        {
            var status = await GetStatusAsync(username);
            return status.IsPrivileged;
        }

        /// <summary>
        ///     Gets a value indicating whether the specified <paramref name="username"/> is watched.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>A value indicating whether the username is watched.</returns>
        public bool IsWatched(string username) => WatchedUsernamesDictionary.ContainsKey(username);

        /// <summary>
        ///     Resolves the name of the group for the specified <paramref name="username"/>.
        /// </summary>
        /// <remarks>
        ///     If the user is watched, and therefore the application is tracking their status and statistics, leech and privilege
        ///     detection works as expected. If the user is not watched, their status and statistics can be specified. If the user
        ///     is neither watched nor status and statistics are supplied, leech and privilege detection will not work.
        /// </remarks>
        /// <param name="username">The username of the peer.</param>
        /// <param name="status">The optional status for the user.</param>
        /// <param name="statistics">The optional statistics for the user.</param>
        /// <returns>The group for the specified username.</returns>
        public string ResolveGroup(string username, Status status = null, Statistics statistics = null)
        {
            /*
             * note: this is an extremely hot path; keep the work done to an absolute minimum.
             */

            // we shouldn't be asking, but this could happen in some scenarios.
            if (OptionsMonitor.CurrentValue.Groups.Blacklisted.Members.Contains(username))
            {
                return Application.BlacklistedGroup;
            }

            var thresholds = OptionsMonitor.CurrentValue.Groups.Leechers.Thresholds;

            // any user that's been explicitly added to a group is "watched" and will be in this dictionary,
            // along with their latest status and statistics.  we *need* to do this lookup to expedite the
            // resolution of their group from configuration quickly.
            if (UserDictionary.TryGetValue(username ?? string.Empty, out var user))
            {
                if (user.Status?.IsPrivileged ?? false)
                {
                    return Application.PrivilegedGroup;
                }

                if (user.Group != null)
                {
                    return user.Group;
                }

                if (user.Statistics?.FileCount < thresholds.Files || user.Statistics?.DirectoryCount < thresholds.Directories)
                {
                    return Application.LeecherGroup;
                }
            }

            // the user hasn't been added to the dictionary, so we know they aren't part of a user defined group
            // the caller may have specified status and statistics. check and see.
            if (status?.IsPrivileged ?? false)
            {
                return Application.PrivilegedGroup;
            }

            if (statistics?.FileCount < thresholds.Files || statistics?.DirectoryCount < thresholds.Directories)
            {
                return Application.LeecherGroup;
            }

            return Application.DefaultGroup;
        }

        /// <summary>
        ///     Adds the specified username to the server-side user list.
        /// </summary>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The operation context.</returns>
        public async Task WatchAsync(string username)
        {
            await Client.AddUserAsync(username);
            WatchedUsernamesDictionary.TryAdd(username, true);

            Log.Information("Added user {Username} to watch list", username);

            // the server does not automatically send status and statistics information when watching a user initially. explicitly
            // fetch both, so that the user has been watched and all information populated when this method returns.
            var status = await GetStatusAsync(username);
            UpdateStatus(username, status);

            var statistics = await GetStatisticsAsync(username);
            UpdateStatistics(username, statistics);
        }

        private void Configure(Options options, bool force = false)
        {
            var optionsHash = Compute.Sha1Hash(options.Groups.UserDefined.ToJson());

            if (optionsHash == LastOptionsHash && !force)
            {
                return;
            }

            // get a list of tracked names that haven't been explicitly added to any group, including those that were previlously
            // configured but have now been removed
            var usernamesBeforeUpdate = UserDictionary.Keys.ToList();
            var usernamesAfterUpdate = options.Groups.UserDefined.SelectMany(g => g.Value.Members);
            var usernamesRemoved = usernamesBeforeUpdate.Except(usernamesAfterUpdate);

            // clear the configured group for anyone that was removed from config, or that was added transiently
            foreach (var username in usernamesRemoved)
            {
                UserDictionary.AddOrUpdate(
                    key: username,
                    addValue: new User() { Username = username },
                    updateValueFactory: (key, user) => user with { Username = username, Group = null });
            }

            // sort by priority, descending. this will cause the highest priority group for the user to be persisted when the
            // operation is complete.
            foreach (var group in options.Groups.UserDefined.OrderByDescending(kvp => kvp.Value.Upload.Priority))
            {
                foreach (var username in group.Value.Members)
                {
                    UserDictionary.AddOrUpdate(
                        key: username,
                        addValue: new User() { Username = username, Group = group.Key },
                        updateValueFactory: (key, user) => user with { Username = username, Group = group.Key });
                }
            }

            StateMutator.SetValue(state => state with { Users = Users.ToArray() });

            WatchAllUsers();

            LastOptionsHash = optionsHash;
        }

        private void Reset()
        {
            WatchedUsernamesDictionary.Clear();
            UserDictionary.Clear();

            StateMutator.SetValue(state => state with { Users = Users.ToArray() });
        }

        private void UpdateStatistics(string username, Statistics statistics)
        {
            UserDictionary.AddOrUpdate(
                key: username,
                addValue: new User() { Statistics = statistics },
                updateValueFactory: (key, user) => user with { Username = username, Statistics = statistics });

            StateMutator.SetValue(state => state with { Users = Users.ToArray() });
        }

        private void UpdateStatus(string username, Status status)
        {
            UserDictionary.AddOrUpdate(
                key: username,
                addValue: new User() { Status = status },
                updateValueFactory: (key, user) => user with { Username = username, Status = status });

            StateMutator.SetValue(state => state with { Users = Users.ToArray() });
        }

        private void WatchAllUsers()
        {
            if (Client.State.HasFlag(SoulseekClientStates.Connected) && Client.State.HasFlag(SoulseekClientStates.LoggedIn))
            {
                foreach (var username in UserDictionary.Keys.ToList())
                {
                    _ = WatchAsync(username);
                }
            }
        }
    }
}