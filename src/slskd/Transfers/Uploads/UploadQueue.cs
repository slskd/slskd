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
    using slskd.Users;

    /// <summary>
    ///     Orchestrates uploads.
    /// </summary>
    public interface IUploadQueue
    {
        /// <summary>
        ///     Awaits the start of an upload.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="filename">The filename for which to await the start.</param>
        /// <returns>The operation context.</returns>
        Task AwaitStartAsync(string username, string filename);

        /// <summary>
        ///     Signals the completion of an upload.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="filename">The completed filename.</param>
        void Complete(string username, string filename);

        /// <summary>
        ///     Enqueues an upload.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="filename">The filename to enqueue.</param>
        void Enqueue(string username, string filename);

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
            IOptionsMonitor<Options> optionsMonitor)
        {
            Users = userService;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private int GlobalSlots { get; set; } = 0;
        private Dictionary<string, UploadGroup> Groups { get; set; } = new Dictionary<string, UploadGroup>();
        private int LastGlobalSlots { get; set; }
        private string LastOptionsHash { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<UploadQueue>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<string, List<Upload>> Uploads { get; set; } = new ConcurrentDictionary<string, List<Upload>>();
        private IUserService Users { get; }

        /// <summary>
        ///     Awaits the start of an upload.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="filename">The filename for which to await the start.</param>
        /// <returns>The operation context.</returns>
        public Task AwaitStartAsync(string username, string filename)
        {
            SyncRoot.Wait();

            try
            {
                if (!Uploads.TryGetValue(username, out var list))
                {
                    throw new SlskdException($"No enqueued uploads for user {username}");
                }

                var upload = list.FirstOrDefault(e => e.Filename == filename);

                if (upload == default)
                {
                    throw new SlskdException($"File {filename} is not enqueued for user {username}");
                }

                upload.Ready = DateTime.UtcNow;
                Log.Debug("Ready: {File} for {User} at {Time}", Path.GetFileName(upload.Filename), upload.Username, upload.Enqueued);

                return upload.TaskCompletionSource.Task;
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

        /// <summary>
        ///     Signals the completion of an upload.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="filename">The completed filename.</param>
        public void Complete(string username, string filename)
        {
            SyncRoot.Wait();

            try
            {
                if (!Uploads.TryGetValue(username, out var list))
                {
                    throw new SlskdException($"No enqueued uploads for user {username}");
                }

                var upload = list.FirstOrDefault(e => e.Filename == filename);

                if (upload == default)
                {
                    throw new SlskdException($"File {filename} is not enqueued for user {username}");
                }

                list.Remove(upload);
                Log.Debug("Complete: {File} for {User} at {Time}", Path.GetFileName(upload.Filename), upload.Username, upload.Enqueued);

                // ensure the slot is returned to the group from which it was acquired the group may have been removed during the
                // transfer. if so, do nothing.
                if (Groups.ContainsKey(upload.Group ?? string.Empty))
                {
                    var group = Groups[upload.Group];

                    group.UsedSlots = Math.Max(0, group.UsedSlots - 1);
                    Log.Debug("Group {Group} slots: {Used}/{Available}", group.Name, group.UsedSlots, group.Slots);
                }

                if (!list.Any() && Uploads.TryRemove(username, out _))
                {
                    Log.Debug("Cleaned up tracking list for {User}; no more queued uploads to track", username);
                }
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

        /// <summary>
        ///     Enqueues an upload.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="filename">The filename to enqueue.</param>
        public void Enqueue(string username, string filename)
        {
            SyncRoot.Wait();

            try
            {
                var upload = new Upload() { Username = username, Filename = filename };

                Uploads.AddOrUpdate(
                    key: username,
                    addValue: new List<Upload>(new[] { upload }),
                    updateValueFactory: (key, list) =>
                    {
                        list.Add(upload);
                        return list;
                    });

                Log.Debug("Enqueued: {File} for {User} at {Time}", Path.GetFileName(upload.Filename), upload.Username, upload.Enqueued);
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

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

        /// <summary>
        ///     Computes the estimated queue position of the specified <paramref name="filename"/> for the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the file.</param>
        /// <param name="filename">The filename of the file for which the position is to be estimated.</param>
        /// <returns>The estimated queue position of the file.</returns>
        /// <exception cref="NotFoundException">Thrown if the specified filename is not enqueued.</exception>
        public int EstimatePosition(string username, string filename)
        {
            var groupName = Users.ResolveGroup(username);
            var groupRecord = Groups.GetValueOrDefault(groupName);

            // the Uploads dictionary is keyed by username; gather all of the users that belong to the same group as the requested user
            var uploadsForGroup = Uploads.Where(kvp => Users.ResolveGroup(kvp.Key) == groupName);

            // the RoundRobin queue implementation is not strictly fair to all users; only uploads that are ready are candidates
            // for selection. this means that if Bob downloads files twice as fast as Alice, Bob is going to advance through the
            // queue twice as fast, too. assume everyone downloads at equal speed for this estimate. also assume that all files
            // are of equal length.
            if (groupRecord.Strategy == QueueStrategy.RoundRobin)
            {
                // find this user's uploads
                if (!Uploads.TryGetValue(username, out var uploadsForUser))
                {
                    throw new NotFoundException($"File {filename} is not enqueued for user {username}");
                }

                // find the position of the requested file in the user's queue
                var localPosition = uploadsForUser
                    .OrderBy(upload => upload.Enqueued)
                    .ToList()
                    .FindIndex(upload => upload.Username == username && upload.Filename == filename);

                if (localPosition < 0)
                {
                    throw new NotFoundException($"File {filename} is not enqueued for user {username}");
                }

                // start the position to the local position within this user's queue; the user's own files must be completed
                // before this one can start.
                var position = localPosition;

                // for each other user, add either localPosition or the count of that user's uploads, whichever is less
                // example:
                //
                // aaaaa
                // bb
                // cccccccccccc
                // ddddddd
                //     ^
                //
                // if we want the postion of the file over the carat above, first find the position of it
                // within its own queue (= 5). assume uploads will process top down, left to right until reaching
                // this one.  that's 5 files from a, 2 from b, 5 from c, and the other 4 from d, putting the file over
                // the carat at position 16. the actual number will vary due to many factors, including where in the
                // round-robin ordering d is actually positioned (so +/- number of users downloading).
                foreach (var group in uploadsForGroup.Where(group => group.Key != username))
                {
                    position += Math.Min(localPosition, group.Value.Count);
                }

                return position;
            }

            // for FIFO queues, files are uploaded in the order they are enqueued, so the position should be pretty good estimate.
            // List ordering is guaranteed, so we are getting an accurate portrayal of where this file is in the queue by order of
            // time enqueued. this includes uploads that are in progress.
            var flattenedSortedUploadsForGroup = uploadsForGroup
                .SelectMany(group => group.Value)
                .OrderBy(upload => upload.Enqueued)
                .ToList();

            var globalPosition = flattenedSortedUploadsForGroup.FindIndex(upload => upload.Username == username && upload.Filename == filename);

            if (globalPosition < 0)
            {
                throw new NotFoundException($"File {filename} is not enqueued for user {username}");
            }

            return globalPosition;
        }

        /// <summary>
        ///     Computes the estimated queue position of the specified <paramref name="username"/> if they were to enqueue a file,
        ///     or zero if the transfer could start immediately.
        /// </summary>
        /// <param name="username">The username for which to estimate.</param>
        /// <returns>
        ///     The estimated queue position if the user were to enqueue a file, or zero if the transfer could start immediately.
        /// </returns>
        public int ForecastPosition(string username)
        {
            var groupName = Users.ResolveGroup(username);

            // if there's a slot available, the user will enter the queue at position 0 (will start immediately)
            if (Groups.TryGetValue(groupName, out var groupRecord) && groupRecord.SlotAvailable)
            {
                return 0;
            }

            // the Uploads dictionary is keyed by username; gather all of the users that belong to the same group as the requested user
            var uploadsForGroup = Uploads.Where(kvp => Users.ResolveGroup(kvp.Key) == groupName);

            // assuming that the queue will be processed in a true round-robin fashion and that the user will be the last in the
            // rotation (worst case), the user's start position will be equal to the number of users downloading or waiting, + 1.
            if (groupRecord.Strategy == QueueStrategy.RoundRobin)
            {
                return uploadsForGroup.Count() + 1;
            }

            // for FIFO queues, the user will enter the queue at the very back. return the total number of uploads in progress and
            // enqueued, + 1.
            return uploadsForGroup
                .SelectMany(group => group.Value)
                .Count() + 1;
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
                            var group = Users.ResolveGroup(user.Key);

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