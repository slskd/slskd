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
        }

        private IUserService Users { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private ConcurrentDictionary<string, int> ActiveUploadCounts { get; } = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, List<Entry>> WaitingUploads { get; } = new ConcurrentDictionary<string, List<Entry>>();

        public void Enqueue(Transfer transfer)
        {
            var group = Users.GetGroup(transfer.Username);

            var entry = new Entry() { Username = transfer.Username, Filename = transfer.Filename };

            SyncRoot.Wait();

            try
            {
                WaitingUploads.AddOrUpdate(
                    key: group,
                    addValue: new List<Entry>(new[] { entry }),
                    updateValueFactory: (key, bag) =>
                    {
                        bag.Add(entry);
                        return bag;
                    });

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
                if (!WaitingUploads.TryGetValue(group, out var list))
                {
                    throw new Exception($"No such group: {group}");
                }

                var entry = list.FirstOrDefault(e => e.Username == transfer.Username && e.Filename == transfer.Filename);

                if (entry == default)
                {
                    throw new Exception($"No such transfer: {transfer.Username}/{transfer.Filename}");
                }

                entry.Ready = DateTime.UtcNow;

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
                if (!WaitingUploads.TryGetValue(group, out var list))
                {
                    throw new Exception($"No such group: {group}");
                }

                var entry = list.FirstOrDefault(e => e.Username == transfer.Username && e.Filename == transfer.Filename);

                list.Remove(entry);

                ActiveUploadCounts.AddOrUpdate(
                    key: group,
                    addValue: 0,
                    updateValueFactory: (key, count) => Math.Min(0, --count));
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
                // sort groups in ascending priority order
                // while slots are available
                //  take lowest priority group
                //  sort waiting uploads according to strategy
                //  release uploads in order, up to the slot limit of the group
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private class Entry
        {
            public string Username { get; set; }
            public string Filename { get; set; }
            public DateTime Enqueued { get; set; } = DateTime.UtcNow;
            public DateTime? Ready { get; set; } = null;
            public TaskCompletionSource TaskCompletionSource { get; set; } = new TaskCompletionSource();
        }
    }
}
