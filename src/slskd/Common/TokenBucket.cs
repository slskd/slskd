// <copyright file="TokenBucket.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Implements the 'token bucket' or 'leaky bucket' rate limiting algorithm.
    /// </summary>
    public interface ITokenBucket
    {
        /// <summary>
        ///     Gets the bucket capacity.
        /// </summary>
        public long Capacity { get; }

        /// <summary>
        ///     Asynchronously retrieves the specified token <paramref name="count"/> from the bucket.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If the requested <paramref name="count"/> exceeds the bucket <see cref="Capacity"/>, the request is lowered to
        ///         the capacity of the bucket.
        ///     </para>
        ///     <para>If the bucket has tokens available, but fewer than the requested amount, the available tokens are returned.</para>
        ///     <para>
        ///         If the bucket has no tokens available, execution waits for the bucket to be replenished before servicing the request.
        ///     </para>
        /// </remarks>
        /// <param name="count">The number of tokens to retrieve.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that completes when tokens have been provided.</returns>
        Task<int> GetAsync(int count, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Returns the specified token <paramref name="count"/> to the bucket.
        /// </summary>
        /// <remarks>
        ///     <para>This method should only be called if tokens were retrieved from the bucket, but were not used.</para>
        ///     <para>
        ///         If the specified count exceeds the bucket capacity, the count is lowered to the capacity. Effectively this
        ///         allows the bucket to 'burst' up to 2x capacity to 'catch up' to the desired rate if tokens were wastefully
        ///         retrieved.
        ///     </para>
        ///     <para>If the specified count is negative, no change is made to the available count.</para>
        /// </remarks>
        /// <param name="count">The number of tokens to return.</param>
        void Return(int count);

        /// <summary>
        ///     Sets the bucket capacity to the supplied <paramref name="capacity"/>.
        /// </summary>
        /// <remarks>Change takes effect on the next reset.</remarks>
        /// <param name="capacity">The bucket capacity.</param>
        void SetCapacity(long capacity);
    }

    /// <summary>
    ///     Implements the 'token bucket' or 'leaky bucket' rate limiting algorithm.
    /// </summary>
    public class TokenBucket : ITokenBucket, IDisposable
    {
        private TaskCompletionSource<bool> waitForReset = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="TokenBucket"/> class.
        /// </summary>
        /// <param name="capacity">The bucket capacity.</param>
        /// <param name="interval">The interval at which tokens are replenished.</param>
        public TokenBucket(long capacity, int interval)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Bucket capacity must be greater than or equal to 1");
            }

            if (interval < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than or equal to 1");
            }

            Capacity = capacity;
            CurrentCount = Capacity;

            Clock = new System.Timers.Timer(interval);
            Clock.Elapsed += (sender, e) => Reset();
            Clock.Start();
        }

        /// <summary>
        ///     Gets the bucket capacity.
        /// </summary>
        public long Capacity { get; private set; }

        private System.Timers.Timer Clock { get; set; }
        private long CurrentCount { get; set; }
        private bool Disposed { get; set; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Asynchronously retrieves the specified token <paramref name="count"/> from the bucket.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         If the requested <paramref name="count"/> exceeds the bucket <see cref="Capacity"/>, the request is lowered to
        ///         the capacity of the bucket.
        ///     </para>
        ///     <para>If the bucket has tokens available, but fewer than the requested amount, the available tokens are returned.</para>
        ///     <para>
        ///         If the bucket has no tokens available, execution waits for the bucket to be replenished before servicing the request.
        ///     </para>
        /// </remarks>
        /// <param name="count">The number of tokens to retrieve.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A Task that completes when tokens have been provided.</returns>
        public Task<int> GetAsync(int count, CancellationToken cancellationToken = default)
        {
            return GetInternalAsync(Math.Min(count, (int)Math.Min(int.MaxValue, Capacity)), cancellationToken);
        }

        /// <summary>
        ///     Returns the specified token <paramref name="count"/> to the bucket.
        /// </summary>
        /// <remarks>
        ///     <para>This method should only be called if tokens were retrieved from the bucket, but were not used.</para>
        ///     <para>
        ///         If the specified count exceeds the bucket capacity, the count is lowered to the capacity. Effectively this
        ///         allows the bucket to 'burst' up to 2x capacity to 'catch up' to the desired rate if tokens were wastefully
        ///         retrieved.
        ///     </para>
        ///     <para>If the specified count is negative, no change is made to the available count.</para>
        /// </remarks>
        /// <param name="count">The number of tokens to return.</param>
        public void Return(int count)
        {
            CurrentCount += Math.Min(Math.Max(count, 0), Capacity);
        }

        /// <summary>
        ///     Sets the bucket capacity to the supplied <paramref name="capacity"/>.
        /// </summary>
        /// <remarks>Change takes effect on the next reset.</remarks>
        /// <param name="capacity">The bucket capacity.</param>
        public void SetCapacity(long capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Bucket capacity must be greater than or equal to 1");
            }

            Capacity = capacity;
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing">A value indicating whether disposal is in progress.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    Clock.Dispose();
                    SyncRoot.Dispose();
                }

                Disposed = true;
            }
        }

        private async Task<int> GetInternalAsync(int count, CancellationToken cancellationToken = default)
        {
            await SyncRoot.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // if the bucket is empty, wait for a reset, then replenish it before continuing
                // this ensures tokens are distributed in the order in which callers obtain the semaphore,
                // which is as close to a FIFO as .NET synchronization primitives will allow
                if (CurrentCount == 0)
                {
                    await waitForReset.Task.ConfigureAwait(false);
                    CurrentCount = Capacity;
                }

                // take the minimum of requested count or CurrentCount, deduct it from
                // CurrentCount (potentially zeroing the bucket), and return it
                var availableCount = Math.Min(CurrentCount, count);
                CurrentCount -= availableCount;
                return (int)availableCount;
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private void Reset()
            => Interlocked.Exchange(ref waitForReset, new TaskCompletionSource<bool>()).SetResult(true);
    }
}
