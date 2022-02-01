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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.Users;
    using Soulseek;

    /// <summary>
    ///     Orchestrates uploads.
    /// </summary>
    public interface IUploadQueue
    {
        void Enqueue(Transfer transfer);
        Task StartAsync(Transfer transfer);
        void Complete(Transfer transfer);
    }

    /// <summary>
    ///     Orchestrates uploads.
    /// </summary>
    public class UploadQueue : IUploadQueue
    {
        public UploadQueue(
            IUserService userService,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Users = userService;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private IUserService Users { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private int MaxSlots { get; set; } = 0;
        private Dictionary<string, Group> Groups { get; set; } = new Dictionary<string, Group>();
        private ConcurrentDictionary<string, List<Upload>> Uploads { get; } = new ConcurrentDictionary<string, List<Upload>>();

        public void Enqueue(Transfer transfer)
        {
            var group = Users.GetGroup(transfer.Username);
            var upload = new Upload() { Username = transfer.Username, Filename = transfer.Filename };

            SyncRoot.Wait();

            try
            {
                Uploads.AddOrUpdate(
                    key: group,
                    addValue: new List<Upload>(new[] { upload }),
                    updateValueFactory: (key, list) =>
                    {
                        list.Add(upload);
                        return list;
                    });

                Console.WriteLine($"[ENQUEUED]: {upload.ToJson()}");
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

        public Task StartAsync(Transfer transfer)
        {
            var group = Users.GetGroup(transfer.Username);

            SyncRoot.Wait();

            try
            {
                if (!Uploads.TryGetValue(group, out var list))
                {
                    throw new Exception($"No such group: {group}");
                }

                var entry = list.FirstOrDefault(e => e.Username == transfer.Username && e.Filename == transfer.Filename);

                if (entry == default)
                {
                    throw new Exception($"No such transfer: {transfer.Username}/{transfer.Filename}");
                }

                entry.Ready = DateTime.UtcNow;

                Console.WriteLine($"[READY]: {entry.ToJson()}");

                return entry.TaskCompletionSource.Task;
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

        public void Complete(Transfer transfer)
        {
            var group = Users.GetGroup(transfer.Username);

            SyncRoot.Wait();

            try
            {
                Groups[group].UsedSlots = Math.Min(0, Groups[group].UsedSlots - 1);
                Console.WriteLine($"Slot returned to group {group}");
            }
            finally
            {
                SyncRoot.Release();
                Process();
            }
        }

        private void Process()
        {
            SyncRoot.Wait();

            try
            {
                if (Groups.Values.Sum(g => g.UsedSlots) >= MaxSlots)
                {
                    Console.WriteLine($"All global slots are used, nothing to process (used: {Groups.Values.Sum(g => g.UsedSlots)}, max: {MaxSlots})");
                    return;
                }

                if (!Uploads.Values.Any(v => v.Any(u => u.Ready.HasValue)))
                {
                    Console.WriteLine($"No ready uploads for any group, nothing to process");
                    return;
                }

                foreach (var group in Groups.Values.OrderBy(g => g.Priority))
                {
                    Console.WriteLine($"Processing group {group.Name} (slots: {group.Slots}, used: {group.UsedSlots}, strategy: {group.Strategy})");

                    if (group.UsedSlots >= group.Slots)
                    {
                        Console.WriteLine($"{group.Name} has no available slots, skipping (used: {group.UsedSlots}, max: {group.Slots})");
                        continue;
                    }

                    if (!Uploads.TryGetValue(group.Name, out var uploads) || !uploads.Any(u => u.Ready.HasValue))
                    {
                        Console.WriteLine($"{group.Name} has no ready uploads, skippling");
                        continue;
                    }

                    var ready = uploads.Where(u => u.Ready.HasValue);
                    Console.WriteLine($"{group.Name} has {uploads.Count} uploads, {ready.Count()} of which are ready");

                    var upload = ready
                        .OrderBy(u => group.Strategy == QueueStrategy.FirstInFirstOut ? u.Enqueued : u.Ready)
                        .FirstOrDefault();

                    Console.WriteLine($"Next upload for group {group.Name} using strategy {group.Strategy}: {upload.Filename} to {upload.Username}");

                    uploads.Remove(upload);
                    upload.TaskCompletionSource.SetResult();
                    group.UsedSlots++;
                }
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private void Configure(Options options)
        {
            SyncRoot.Wait();

            try
            {
                var o = OptionsMonitor.CurrentValue.Groups;

                var groups = new List<Group>()
                {
                    new Group()
                    {
                        Name = "default",
                        Priority = o.Default.Upload.Priority,
                        Slots = o.Default.Upload.Slots,
                        UsedSlots = 0, // TODO: copy this from existing group
                        Strategy = (QueueStrategy)Enum.Parse(typeof(QueueStrategy), o.Default.Upload.Strategy, true),
                    },
                };

                groups.AddRange(o.UserDefined.Select(kvp => new Group()
                {
                    Name = kvp.Key,
                    Priority = kvp.Value.Upload.Priority,
                    Slots = kvp.Value.Upload.Slots,
                    UsedSlots = 0, // TODO: copy this from existing group
                    Strategy = (QueueStrategy)Enum.Parse(typeof(QueueStrategy), kvp.Value.Upload.Strategy, true),
                }));

                Groups = groups.ToDictionary(g => g.Name);
                MaxSlots = options.Global.Upload.Slots;
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private class Group
        {
            public string Name { get; set; }
            public int Slots { get; set; }
            public int Priority { get; set; }
            public QueueStrategy Strategy { get; set; }
            public int UsedSlots { get; set; }
        }

        private class Upload
        {
            public string Username { get; set; }
            public string Filename { get; set; }
            public DateTime Enqueued { get; set; } = DateTime.UtcNow;
            public DateTime? Ready { get; set; } = null;
            public TaskCompletionSource TaskCompletionSource { get; set; } = new TaskCompletionSource();
        }
    }
}
