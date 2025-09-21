// <copyright file="UploadScheduler.cs" company="slskd Team">
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

namespace slskd.Transfers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using slskd.Transfers.Uploads;
using slskd.Users;
using Soulseek;

public class UploadScheduler
{
    public UploadScheduler(IUserService userService, IUploadService uploadService, ISoulseekClient soulseekClient, IOptionsMonitor<Options> optionsMonitor)
    {
        Users = userService;
        Uploads = uploadService;
        SoulseekClient = soulseekClient;
        OptionsMonitor = optionsMonitor;

        Timer = new System.Timers.Timer(5000)
        {
            AutoReset = true,
            Enabled = true,
        };
        Timer.Elapsed += (_, args) =>
        {
            _ = MonitorAsync();
        };

        SoulseekClient.TransferStateChanged += (_, args) =>
        {
            _ = ScheduleAsync();
        };

        Configure(OptionsMonitor.CurrentValue);
    }

    private System.Timers.Timer Timer { get; }
    private IUserService Users { get; }
    private IUploadService Uploads { get; }
    private ISoulseekClient SoulseekClient { get; }
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private Dictionary<string, UploadGroup> Groups { get; set; } = new Dictionary<string, UploadGroup>();
    private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
    private int LastGlobalSlots { get; set; }
    private string LastOptionsHash { get; set; }
    private int GlobalSlots { get; set; } = 0;
    private ILogger Log { get; } = Serilog.Log.ForContext<UploadScheduler>();
    private ConcurrentDictionary<Guid, Task<Transfer>> ScheduledTasks { get; } = [];

    public virtual async Task ScheduleAsync(params Guid[] parents)
    {
        var runId = Guid.NewGuid();

        try
        {
            var upload = await GetNextUpload();

            if (upload is null)
            {
                Log.Debug("[{Id}] Scheduler found no upload to schedule", runId);
                return;
            }

            if (!ScheduledTasks.TryAdd(upload.Id, Uploads.UploadAsync(upload)))
            {
                throw new Exception($"Failed to schedule upload task; task for id {upload.Id} must already be scheduled");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Id}] Scheduler run did not complete successfully: {Message}", runId, ex.Message);
        }
    }

    private async Task MonitorAsync()
    {
        var completedTasks = ScheduledTasks.Where(kvp => kvp.Value.IsCompleted);

        foreach (var task in completedTasks)
        {
            try
            {
                var upload = await task.Value;

                if (task.Value.Status != TaskStatus.RanToCompletion && !upload.State.HasFlag(TransferStates.Completed))
                {
                    Log.Warning("Upload of {File} to {User} did not update cleanly");
                    upload.State = TransferStates.Completed | upload.State;
                    upload.EndedAt ??= DateTime.UtcNow;
                    Uploads.Update(upload);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Upload {Id} failed, see database for details", task.Key);
            }
            finally
            {
                ScheduledTasks.TryRemove(task.Key, out _);
            }
        }
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
            _ = ScheduleAsync();
        }
    }

    private async Task<Transfer> GetNextUpload()
    {
        await Task.Yield();

        SyncRoot.Wait();

        var sw = new Stopwatch();
        sw.Start();

        try
        {
            /*
                first, check to see if we have any free upload slots. if not, there's nothing to do

                note that we're only stopping if the number of tracked uploads is greater than the cap; we do this because
                often we will attempt to schedule another upload while a previous upload is still finalizing. we may end up
                starting more transfers than we can actually support, in which case one transfer will get stuck behind the
                global semaphore, which is better than 'starving' the semaphore (unless it causes problems)
            */
            Log.Debug("Current uploads: {Count}, global cap: {Cap}", SoulseekClient.Uploads.Count, GlobalSlots);

            if (SoulseekClient.Uploads.Count > GlobalSlots)
            {
                return null;
            }

            /*
                next, snapshot a list of all uploads in the Queued | Locally state. this will include transfers that
                have been enqueued and that have never made an UploadAsync() attempt, and sometimes will also include
                transfers for which UploadAsync() has been called but that have not yet transitioned out of the locally
                queued state. this is ok! it is only a _missed opportunity_ to attempt to upload something else. UploadAsync()
                will exit gracefully in these cases, and we should correct it the next time we run Process()

                note: we need *all* transfers at this stage because we don't know which group the user associated with
                the transfer is in yet. we do this dynamically to allow users to move around at run-time, which is particularly
                important for dynamic groups, particularly leechers.
            */
            var readyUploads = Uploads
                .List(t => t.State.HasFlag(TransferStates.Queued) && t.State.HasFlag(TransferStates.Locally), includeRemoved: true);

            if (readyUploads.Count == 0)
            {
                Log.Debug("No uploads are ready, nothing to schedule");
                return null;
            }
            else
            {
                Log.Debug("{Count} uploads are ready", readyUploads.Count);
            }

            var readyUploadsByGroup = readyUploads
                .GroupBy(t => Users.GetGroup(t.Username))
                .ToDictionary(g => g.Key, g => g.ToList());

            var inProgressUploadsByGroup = SoulseekClient.Uploads
                .GroupBy(u => Users.GetGroup(u.Username))
                .ToDictionary(g => g.Key, g => g.ToList());

            Log.Debug("Groups: {Groups}", string.Join(',', Groups.Values));

            // sort groups in order of priority (privileged = 0 goes first, and so on) and iterate over them
            // until we find a group with a free slot that we can occupy with a new upload, then choose which
            // upload among the waiting up loads for that group to start, and return it
            foreach (var group in Groups.Values.OrderBy(g => g.Priority).ThenBy(g => g.Name))
            {
                Log.Debug("Processing group: {Group}", group.Name);

                // skip this group if there are no ready transfers for it
                if (!readyUploadsByGroup.TryGetValue(group.Name, out var ready))
                {
                    Log.Debug("No ready uploads for group {Group}", group.Name);
                    continue;
                }

                var slotsUsed = 0;

                if (inProgressUploadsByGroup.TryGetValue(group.Name, out List<Soulseek.Transfer> value))
                {
                    slotsUsed = value.Count;
                }

                // skip this group if all of the available slots are taken by in progress transfers
                if (slotsUsed >= group.Slots)
                {
                    Log.Debug("No free slots for group {Group} (used {Count} of {Limit})", group.Name, slotsUsed, group.Slots);
                    continue;
                }

                /*
                    we've found a group for which there are 1) ready uploads and 2) free slots
                    choose an upload and return it
                */
                if (group.Strategy == QueueStrategy.FirstInFirstOut)
                {
                    // if this is a FIFO group, select whatever transfer was enqueued first
                    Log.Debug("FIFO strategy; selecting oldest enqueued file");
                    return ready.OrderBy(u => u.EnqueuedAt).First();
                }

                // if this is a round robin group, do our best to choose fairly among everyone waiting
                var users = ready.Select(u => u.Username).Distinct().ToList();
                var random = new Random().Next(users.Count());
                var luckyUser = users[random];
                Log.Debug("RoundRobin strategy; selecting user number {Random} of {Count} users: {Name}", random, users.Count(), luckyUser);
                return ready.Where(u => u.Username == luckyUser).OrderBy(u => u.EnqueuedAt).First();
            }

            Log.Debug("Processing {Count} eligible uploads", readyUploads.Count);
            return null;
        }
        finally
        {
            sw.Stop();
            Log.Debug("GetNextUpload() completed in {Duration}ms", sw.ElapsedMilliseconds);
            SyncRoot.Release();
        }
    }
}