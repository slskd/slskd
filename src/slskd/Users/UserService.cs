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
    using NetTools;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Provides information and operations for network peers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This class maintains a <see cref="UserDictionary"/> that acts as a non-expiring cache of information
    ///         collected about a user.  This includes their statistics (share counts, speed, etc), their status (privileged, etc)
    ///         and, if they are a member of a user-defined group, their group.
    ///     </para>
    ///     <para>
    ///         This class also maintains a <see cref="WatchedUsernamesDictionary"/> to keep track of which usernames have been
    ///         "watched" server side and for which we will therefore receive events when their status changes.
    ///     </para>
    ///     <para>
    ///         If a user's information is in the <see cref="UserDictionary"/>, it's because we requested it at some point.  If that user
    ///         is also "watched", we can assume that the data in the dictionary is up to date and will be kept so.
    ///     </para>
    ///     <para>
    ///         The data in the <see cref="UserDictionary"/> can continue to grow until -- unlikely -- it contains a record for every
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
    public class UserService : IUserService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UserService"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="optionsMonitor"></param>
        public UserService(
            ISoulseekClient soulseekClient,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Client = soulseekClient;
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

            // it's important for us to force a reconfig at login to discard any users that were previously tracked
            // specific scenario being; up and running for some time, offline for some time (days?), reconnect, are users still online? have stats changed?
            // to avoid needing to go through and exhaustively check each user in the tracking dictionary, just delete it and let it be built back up
            // any user we are downloading from will be tracked again when pending downloads are re-requested
            Client.LoggedIn += (_, _) => Configure(OptionsMonitor.CurrentValue, force: true);

            // working hand-in-hand with the forced reconfig on login, reset clears everything upon connect; clearing the way
            // for the reconfig at login to rebuild it. i think. yeah, that sounds right. we don't ever want Configure() to reset anything
            // so the connect is distinctly different from reconfig at login.
            Client.Connected += (_, _) => Reset();
            Client.PrivilegedUserListReceived += (_, list) => Client_PrivilegedUserListReceived(list);

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
        private string LastBlacklistOptionsHash { get; set; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<UserService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private Blacklist Blacklist { get; } = new Blacklist();

        /// <summary>
        ///     Gets or sets the internal cache of User data.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method retrieves the requested user's information from a dictionary, and is therefore a 'get'. This
        ///         information is needed to control who can do what, who is subject to what limits, how to control the upload
        ///         queue, and how to govern speeds. The logic in this method is the 'hottest' in the application and *must*
        ///         remain very simple and _fast_.
        ///     </para>
        ///     <para>
        ///         If this method is called for a user who has not previously been cached, it will return the default group,
        ///         meaning leech, privilege, and user defined group discrimination will not work. For this reason it is critical
        ///         that user information is retrieved and cached upon first interaction with that user.
        ///     </para>
        ///     <para>
        ///         Uploads can be cached indefinitely, and for that reason the user data cache must be filled indefinitely;
        ///         meaning there is no invalidation and that user data will accrue for the lifetime of the application (instance).
        ///     </para>
        /// </remarks>
        private ConcurrentDictionary<string, User> UserDictionary { get; set; } = new ConcurrentDictionary<string, User>();
        private ConcurrentDictionary<string, bool> WatchedUsernamesDictionary { get; set; } = new ConcurrentDictionary<string, bool>();

        /// <summary>
        ///     Gets the name of the group for the specified <paramref name="username"/>.
        /// </summary>
        /// <remarks>The group name is fetched from cached data, and lookups should always be fast.</remarks>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The group for the specified username.</returns>
        public string GetGroup(string username)
        {
            // note: this is an extremely hot path; keep the work done to an absolute minimum.
            if (UserDictionary.TryGetValue(username ?? string.Empty, out var user))
            {
                if (IsBlacklisted(user.Username))
                {
                    return Application.BlacklistedGroup;
                }

                if (user.Status?.IsPrivileged ?? false)
                {
                    return Application.PrivilegedGroup;
                }

                if (user.Group != null)
                {
                    return user.Group;
                }

                var thresholds = OptionsMonitor.CurrentValue.Groups.Leechers.Thresholds;

                if (user.Statistics?.FileCount < thresholds.Files || user.Statistics?.DirectoryCount < thresholds.Directories)
                {
                    return Application.LeecherGroup;
                }
            }

            return Application.DefaultGroup;
        }

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
        ///     Gets the name of the group for the specified <paramref name="username"/>, or, if the user's information isn't
        ///     cached, fetches and caches the user's information from the server, then returns the group.
        /// </summary>
        /// <remarks>The fetch of fresh data can be forced by specifying <paramref name="forceFetch"/> = true.</remarks>
        /// <param name="username">The username of the peer.</param>
        /// <param name="forceFetch">
        ///     A value determining whether the user's information should be fetched from the server, regardless of local cache.
        /// </param>
        /// <returns>The group for the specified username.</returns>
        public async Task<string> GetOrFetchGroupAsync(string username, bool forceFetch = false)
        {
            if (!UserDictionary.ContainsKey(username) || forceFetch)
            {
                // ensure the record will exist
                UserDictionary.TryAdd(username, new User { Username = username });

                // try to populate the user's info
                await GetStatisticsAsync(username);
                await GetStatusAsync(username);
            }

            return GetGroup(username);
        }

        /// <summary>
        ///     Retrieves the current <see cref="Statistics"/> of a peer, and caches the result.
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
        ///     Retrieves the current <see cref="Status"/> of a peer, and caches the result.
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
        ///     Gets a value indicating whether the specified <paramref name="username"/> and/or <paramref name="ipAddress"/> are blacklisted.
        /// </summary>
        /// <param name="username">The username to check.</param>
        /// <param name="ipAddress">The IPAddress to check, if available.</param>
        /// <returns>A value indicating whether the specified user and/or IP are blacklisted.</returns>
        public bool IsBlacklisted(string username, IPAddress ipAddress = null)
        {
            var blacklist = OptionsMonitor.CurrentValue.Groups.Blacklisted;

            if (blacklist.Members.Contains(username))
            {
                return true;
            }

            // check the user-curated list of blacklisted CIDRs that exists along with the list of
            // blacklisted usernames.  these CIDRs should be one-offs and would not be expected to appear in a
            // blacklist supplied by a third party (but might?)
            if (ipAddress is not null && blacklist.Cidrs.Select(c => IPAddressRange.Parse(c)).Any(range => range.Contains(ipAddress)))
            {
                return true;
            }

            // check the managed blacklist loaded from a third party blacklist file
            if (ipAddress is not null && Blacklist.Contains(ipAddress))
            {
                return true;
            }

            return false;
        }

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
        ///     Adds the specified username to the server-side user list.
        /// </summary>
        /// <remarks>
        ///     <para>Idempotent; if a user is already watched, subsequent calls will only update their status and statistics.</para>
        ///     <para>
        ///         Any user we want to track in any way needs to be watched so that their statistics and status are updated
        ///         properly via server side events. This seems extreme and wasteful, but the only alternative is to periodically
        ///         spam the server for the information instead of letting the server spam the client when things change.
        ///     </para>
        /// </remarks>
        /// <param name="username">The username of the peer.</param>
        /// <returns>The operation context.</returns>
        public async Task WatchAsync(string username)
        {
            // watch the user, server side
            await Client.WatchUserAsync(username);
            UserDictionary.TryAdd(username, new User { Username = username });

            Log.Information("Added user {Username} to watch list", username);

            // the server does not automatically send status and statistics information when watching a user initially. explicitly
            // fetch both, so that the user has been watched and all information populated when this method returns.
            await GetStatusAsync(username);
            await GetStatisticsAsync(username);

            // delay the add until last, so that IsWatched won't return true until stats and status are populated. this may result
            // in several unnecessary calls to WatchAsync (if someone is checking IsWatched() and WatchAsync()ing if false), but we
            // can be sure that if IsWatched() is true, we have valid stats and status. if we can't ensure this, the application
            // will have non-deterministic behavior when it makes decisions about user groups, limits, governance etc.
            WatchedUsernamesDictionary.TryAdd(username, true);
        }

        private void Client_PrivilegedUserListReceived(IEnumerable<string> list)
        {
            foreach (var username in list)
            {
                UserDictionary.AddOrUpdate(
                    key: username,
                    addValue: new User() { Status = new Status { IsPrivileged = true } },
                    updateValueFactory: (key, user) => user with { Username = username, Status = user.Status with { IsPrivileged = true } });
            }
        }

        private void Configure(Options options, bool force = false)
        {
            var optionsHash = Compute.Sha1Hash(options.Groups.UserDefined.ToJson());

            if (optionsHash != LastOptionsHash || force)
            {
                // get a list of tracked names that haven't been explicitly added to any group, including those that were previously
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

                        if (Client.State.HasFlag(SoulseekClientStates.Connected) && Client.State.HasFlag(SoulseekClientStates.LoggedIn))
                        {
                            _ = WatchAsync(username);
                        }
                    }
                }

                LastOptionsHash = optionsHash;
            }

            var blacklistOptionsHash = Compute.Sha1Hash(options.Blacklist.ToJson());

            // there's no forced re-config of the blacklist; either the config changed or it didn't.
            // if the underlying file changed, users can toggle the blacklist off and on or restart
            // the application. these sorts of blacklists should be relatively static (i think)
            if (blacklistOptionsHash != LastBlacklistOptionsHash)
            {
                if (!string.IsNullOrEmpty(LastBlacklistOptionsHash))
                {
                    Log.Debug("Blacklist options changed: {JSON}", options.Blacklist.ToJson());
                }

                if (!options.Blacklist.Enabled)
                {
                    Log.Debug("Blacklist disabled; clearing contents");
                    Blacklist.Clear();
                    Log.Information("Blacklist disabled");
                }
                else
                {
                    // the option validation logic should have ensured the file format could be auto-detected and that the file
                    // contained no formatting errors. this should only fail on transient I/O errors.
                    _ = Task.Run(() => Blacklist.Load(options.Blacklist.File, BlacklistFormat.AutoDetect))
                        .ContinueWith(task =>
                        {
                            // if the task faulted, the load of the file failed, and the user isn't getting the
                            // intended protection from the blacklist. in this case we can:
                            //   1) log an error and continue, potentially against the desires of the user
                            //   2) kill the application and force the user to deal with the cause
                            // a user taking advantage of the blacklist feature would absolutely want to know if it wasn't working,
                            // so we're taking door #2.
                            if (task.IsFaulted)
                            {
                                Log.Fatal(task.Exception, "Fatal error loading blacklist from file {File}: {Message}", options.Blacklist.File, task.Exception?.Message);
                                Program.Exit(1);
                            }
                            else
                            {
                                Log.Information("Blacklist updated with {Count} CIDRs from file {File}", Blacklist.Count, options.Blacklist.File);
                            }
                        });
                }

                LastBlacklistOptionsHash = blacklistOptionsHash;
            }
        }

        private void Reset()
        {
            WatchedUsernamesDictionary.Clear();
            UserDictionary.Clear();
        }

        private void UpdateStatistics(string username, Statistics statistics)
        {
            UserDictionary.AddOrUpdate(
                key: username,
                addValue: new User() { Statistics = statistics },
                updateValueFactory: (key, user) => user with { Username = username, Statistics = statistics });
        }

        private void UpdateStatus(string username, Status status)
        {
            UserDictionary.AddOrUpdate(
                key: username,
                addValue: new User() { Status = status },
                updateValueFactory: (key, user) => user with { Username = username, Status = status });
        }
    }
}
