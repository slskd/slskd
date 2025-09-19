// <copyright file="UploadQueue.cs" company="slskd Team">
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

namespace slskd.Transfers
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Transfers.Uploads;

    using slskd.Users;

    /// <summary>
    ///     Orchestrates uploads.
    /// </summary>
    public interface IUploadQueue
    {
        /// <summary>
        ///     Gets information about the specified <paramref name="groupName"/>.
        /// </summary>
        /// <param name="groupName">The name of the group.</param>
        /// <returns>The group information.</returns>
        UploadGroup GetGroupInfo(string groupName);

        /// <summary>
        ///     Computes the estimated queue position of the specified <paramref name="filename"/> for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the file.</param>
        /// <param name="filename">The filename of the file for which the position is to be estimated.</param>
        /// <returns>The estimated queue position of the file.</returns>
        /// <exception cref="NotFoundException">Thrown if the specified filename is not enqueued.</exception>
        int EstimatePosition(string username, string filename);

        /// <summary>
        ///     Computes the estimated queue position of the specified <paramref name="username"/> if they were to enqueue a file,
        ///     or zero if the transfer could start immediately.
        /// </summary>
        /// <param name="username">The username for which to estimate.</param>
        /// <returns>
        ///     The estimated queue position if the user were to enqueue a file, or zero if the transfer could start immediately.
        /// </returns>
        int ForecastPosition(string username);
    }

    /// <summary>
    ///     Orchestrates uploads.
    /// </summary>
    public class UploadQueue : IUploadQueue
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UploadQueue"/> class.
        /// </summary>
        /// <param name="userService">The UserService instance to use.</param>
        /// <param name="optionsMonitor">The OptionsMonitor instance to use.</param>
        public UploadQueue(
            IUserService userService,
            IUploadService uploadService,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Users = userService;
            Uploads = uploadService;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private IUploadService Uploads { get; }
        private int GlobalSlots { get; set; } = 0;
        private Dictionary<string, UploadGroup> Groups { get; set; } = new Dictionary<string, UploadGroup>();
        private int LastGlobalSlots { get; set; }
        private string LastOptionsHash { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<UploadQueue>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private IUserService Users { get; }

        /// <summary>
        ///     Gets information about the specified <paramref name="groupName"/>.
        /// </summary>
        /// <param name="groupName">The name of the group.</param>
        /// <returns>The group information.</returns>
        public UploadGroup GetGroupInfo(string groupName)
        {
            if (Groups.TryGetValue(groupName, out var group))
            {
                return group;
            }

            throw new NotFoundException($"A group with the name {groupName} could not be found");
        }

        private void Configure(Options options)
        {
            int GetExistingUsedSlotsOrDefault(string group)
                => Groups.ContainsKey(group) ? Groups[group].UsedSlots : 0;

            SyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(options.Groups.ToJson());

                if (optionsHash == LastOptionsHash && options.Global.Upload.Slots == LastGlobalSlots)
                {
                    return;
                }

                GlobalSlots = options.Global.Upload.Slots;

                // statically add built-in groups
                var groups = new List<UploadGroup>()
                {
                    // the priority group is hard-coded with priority 0, slot count equivalent to the overall max, and a FIFO
                    // strategy. all other groups have a minimum priority of 1 (enforced by options validation) to ensure that
                    // privileged users always take priority, regardless of user configuration. the strategy is fixed to FIFO
                    // because that gives privileged users the closest experience to the official client, as well as the
                    // appearance of fairness once the first upload begins.
                    new UploadGroup()
                    {
                        Name = Application.PrivilegedGroup,
                        Priority = 0,
                        Slots = GlobalSlots,
                        UsedSlots = GetExistingUsedSlotsOrDefault(Application.PrivilegedGroup),
                        Strategy = QueueStrategy.FirstInFirstOut,
                    },
                    new UploadGroup()
                    {
                        Name = Application.DefaultGroup,
                        Priority = options.Groups.Default.Upload.Priority,
                        Slots = Math.Min(options.Groups.Default.Upload.Slots, GlobalSlots),
                        UsedSlots = GetExistingUsedSlotsOrDefault(Application.DefaultGroup),
                        Strategy = options.Groups.Default.Upload.Strategy.ToEnum<QueueStrategy>(),
                    },
                    new UploadGroup()
                    {
                        Name = Application.LeecherGroup,
                        Priority = options.Groups.Leechers.Upload.Priority,
                        Slots = Math.Min(options.Groups.Leechers.Upload.Slots, GlobalSlots),
                        UsedSlots = GetExistingUsedSlotsOrDefault(Application.LeecherGroup),
                        Strategy = options.Groups.Leechers.Upload.Strategy.ToEnum<QueueStrategy>(),
                    },
                };

                // dynamically add user-defined groups
                groups.AddRange(options.Groups.UserDefined.Select(kvp => new UploadGroup()
                {
                    Name = kvp.Key,
                    Priority = kvp.Value.Upload.Priority,
                    Slots = Math.Min(kvp.Value.Upload.Slots, GlobalSlots),
                    UsedSlots = GetExistingUsedSlotsOrDefault(kvp.Key),
                    Strategy = kvp.Value.Upload.Strategy.ToEnum<QueueStrategy>(),
                }));

                Groups = groups.ToDictionary(g => g.Name);

                LastGlobalSlots = options.Global.Upload.Slots;
                LastOptionsHash = optionsHash;
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

        /// <summary>
        ///     Process the queue and attempt to initiate the next highest priority upload, if any are available.
        /// </summary>
        /// <returns></returns>
        private Upload Process()
        {
            SyncRoot.Wait();

            try
            {
                // if the total number of used slots across all groups is greater than or equal to the configured
                // number of global slots, just exit. we *could* proceed, but uploads would stack up behind the
                // global semaphore in Soulseek.NET and we wouldn't be able to control the order in which those
                // were processed, so don't do that.
                if (Groups.Values.Sum(g => g.UsedSlots) >= GlobalSlots)
                {
                    return null;
                }

                // flip the uploads dictionary so that it is keyed by group instead of user. wait until just before we process the
                // queue to do this, and fetch each user's group as we do, to allow users to move between groups at run time. we
                // delay "pinning" an upload to a group (via UsedSlots, below) for the same reason.
                var readyUploadsByGroup = Uploads.Aggregate(
                    seed: new ConcurrentDictionary<string, List<Upload>>(),
                    func: (groups, user) =>
                    {
                        var ready = user.Value.Where(u => u.Ready.HasValue && !u.Started.HasValue);

                        if (ready.Any())
                        {
                            var group = Users.GetGroup(user.Key);

                            groups.AddOrUpdate(
                                key: group,
                                addValue: new List<Upload>(ready),
                                updateValueFactory: (group, list) =>
                                {
                                    list.AddRange(ready);
                                    return list;
                                });
                        }

                        return groups;
                    });

                // process each group in ascending order of priority, and stop after the first ready upload is released.
                foreach (var group in Groups.Values.OrderBy(g => g.Priority).ThenBy(g => g.Name))
                {
                    if (group.UsedSlots >= group.Slots || !readyUploadsByGroup.TryGetValue(group.Name, out var uploads) || !uploads.Any())
                    {
                        continue;
                    }

                    var upload = uploads
                        .OrderBy(u => group.Strategy == QueueStrategy.FirstInFirstOut ? u.Enqueued : u.Ready)
                        .First();

                    // mark the upload as started, and "pin" it to the group from which the slot is obtained, so the slot can be
                    // returned to the proper place upon completion
                    upload.Started = DateTime.UtcNow;
                    upload.Group = group.Name;
                    group.UsedSlots++;

                    // release the upload
                    upload.TaskCompletionSource.SetResult();
                    Log.Debug("Started: {File} for {User} at {Time}", Path.GetFileName(upload.Filename), upload.Username, upload.Enqueued);
                    Log.Debug("Group {Group} slots: {Used}/{Available}", group.Name, group.UsedSlots, group.Slots);

                    return upload;
                }

                return null;
            }
            finally
            {
                SyncRoot.Release();
            }
        }
    }
}