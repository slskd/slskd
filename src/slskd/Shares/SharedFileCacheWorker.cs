// <copyright file="SharedFileCacheWorker.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Serilog;

    /// <summary>
    ///     Shared file cache worker.
    /// </summary>
    public class SharedFileCacheWorker : ISharedFileCacheWorker
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharedFileCacheWorker"/> class.
        /// </summary>
        /// <param name="id">The worker's unique Id.</param>
        /// <param name="directoryChannel">The channel from which the worker will receive directories to scan.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
        /// <param name="handler">An <see cref="Action"/> to be performed on each item read from the channel.</param>
        public SharedFileCacheWorker(
            int id,
            Channel<string> directoryChannel,
            CancellationToken cancellationToken,
            Action<string> handler)
        {
            Id = id;
            DirectoryChannel = directoryChannel;
            CancellationToken = cancellationToken;
            Handler = handler;
        }

        /// <summary>
        ///     Gets the <see cref="Task"/> that completes when the worker has completed all of its work.
        /// </summary>
        public Task Completed => TaskCompletionSource.Task;

        /// <summary>
        ///     Gets the Id of the worker.
        /// </summary>
        public int Id { get; }

        private Channel<string> DirectoryChannel { get; }
        private Action<string> Handler { get; }
        private TaskCompletionSource TaskCompletionSource { get; } = new TaskCompletionSource();
        private CancellationToken CancellationToken { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<SharedFileCacheWorker>();

        /// <summary>
        ///     Starts the worker.
        /// </summary>
        public void Start()
        {
            _ = Read();
            Log.Debug("Shared file cache worker {Id} started", Id);
        }

        private async Task Read()
        {
            try
            {
                while (!CancellationToken.IsCancellationRequested && !DirectoryChannel.Reader.Completion.IsCompleted)
                {
                    var directory = await DirectoryChannel.Reader.ReadAsync(CancellationToken);
                    Handler(directory);
                }
            }
            catch (ChannelClosedException)
            {
                // noop. the channel might close between the time we check and when we go to read; this just means there is no more data.
            }
            finally
            {
                Log.Debug($"Shared file cache worker {Id}'s work is complete.", Id);
                TaskCompletionSource.SetResult();
            }
        }
    }
}
