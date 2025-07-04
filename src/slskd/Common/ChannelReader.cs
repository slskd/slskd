// <copyright file="ChannelReader.cs" company="slskd Team">
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

    /// <summary>
    ///     Reads and handles items from a channel.
    /// </summary>
    public interface IChannelReader
    {
        /// <summary>
        ///     Gets the <see cref="Task"/> that completes when the reader has read all available items from the channel.
        /// </summary>
        Task Completed { get; }

        /// <summary>
        ///     Gets a value indicating whether the reader has started reading.
        /// </summary>
        bool Started { get; }

        /// <summary>
        ///     Starts the reader.
        /// </summary>
        void Start();
    }

    /// <summary>
    ///     Shared file cache worker.
    /// </summary>
    /// <typeparam name="T">The type of the underlying channel.</typeparam>
    public class ChannelReader<T> : IChannelReader
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ChannelReader{T}"/> class.
        /// </summary>
        /// <param name="channel">The channel from which the reader will read.</param>
        /// <param name="handler">An <see cref="Action"/> to be invoked for each item read from the channel.</param>
        /// <param name="exceptionHandler">An optional <see cref="Action"/> to be invoked if the reader encounters an <see cref="Exception"/>.</param>
        /// <param name="automaticallyStart">An optional value indicating whether to automatically start the reader.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to monitor for cancellation.</param>
        public ChannelReader(
            Channel<T> channel,
            Action<T> handler,
            Action<Exception> exceptionHandler = null,
            bool automaticallyStart = true,
            CancellationToken cancellationToken = default)
        {
            Channel = channel;
            Handler = handler;
            ExceptionHandler = exceptionHandler;

            CancellationToken = cancellationToken;

            if (automaticallyStart)
            {
                Start();
            }
        }

        /// <summary>
        ///     Gets the <see cref="Task"/> that completes when the worker has completed all of its work.
        /// </summary>
        public Task Completed => TaskCompletionSource.Task;

        private bool started;

        /// <summary>
        ///     Gets a value indicating whether the reader has started reading.
        /// </summary>
        public bool Started
        {
            get
            {
                lock (SyncRoot)
                {
                    return started;
                }
            }
            private set
            {
                lock (SyncRoot)
                {
                    started = value;
                }
            }
        }

        private Channel<T> Channel { get; }
        private Action<T> Handler { get; }
        private Action<Exception> ExceptionHandler { get; }
        private TaskCompletionSource TaskCompletionSource { get; } = new TaskCompletionSource();
        private CancellationToken CancellationToken { get; }
        private object SyncRoot { get; } = new object();

        /// <summary>
        ///     Starts the reader.
        /// </summary>
        public void Start()
        {
            lock (SyncRoot)
            {
                if (!Started)
                {
                    _ = Read();
                    Started = true;
                }
            }
        }

        private async Task Read()
        {
            try
            {
                while (!Channel.Reader.Completion.IsCompleted)
                {
                    var item = await Channel.Reader.ReadAsync(CancellationToken);
                    Handler(item);
                }
            }
            catch (ChannelClosedException)
            {
                // noop. the channel might close between the time we check and when we go to read; this just means there is no more data.
            }
            catch (Exception ex)
            {
                ExceptionHandler?.Invoke(ex);
                TaskCompletionSource.SetException(ex);
                throw;
            }
            finally
            {
                TaskCompletionSource.TrySetResult();
            }
        }
    }
}
