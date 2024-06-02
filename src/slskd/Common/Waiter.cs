// <copyright file="Waiter.cs" company="slskd Team">
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
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Enables arbitrary await-able things.
    /// </summary>
    public interface IWaiter : IDisposable
    {
        /// <summary>
        ///     Gets the default timeout duration.
        /// </summary>
        int DefaultTimeout { get; }

        /// <summary>
        ///     Cancels the oldest wait matching the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        void Cancel(WaitKey key);

        /// <summary>
        ///     Cancels all waits.
        /// </summary>
        void CancelAll();

        /// <summary>
        ///     Completes the oldest wait matching the specified <paramref name="key"/> with the specified <paramref name="result"/>.
        /// </summary>
        /// <typeparam name="T">The wait result type.</typeparam>
        /// <param name="key">The unique WaitKey for the wait.</param>
        /// <param name="result">The wait result.</param>
        void Complete<T>(WaitKey key, T result);

        /// <summary>
        ///     Completes the oldest wait matching the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        void Complete(WaitKey key);

        /// <summary>
        ///     Returns a value indicating whether the specified <paramref name="key"/> is being waited upon.
        /// </summary>
        /// <param name="key">The unique WaitKey to check.</param>
        /// <returns>A value indicating whether the specified key is being waited upon.</returns>
        bool IsWaitingFor(WaitKey key);

        /// <summary>
        ///     Throws the specified <paramref name="exception"/> on the oldest wait matching the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        /// <param name="exception">The Exception to throw.</param>
        void Throw(WaitKey key, Exception exception);

        /// <summary>
        ///     Causes the oldest wait matching the specified <paramref name="key"/> to time out.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        void Timeout(WaitKey key);

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> and with the specified <paramref name="timeout"/>.
        /// </summary>
        /// <typeparam name="T">The wait result type.</typeparam>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="timeout">The wait timeout.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        Task<T> Wait<T>(WaitKey key, int? timeout = null, CancellationToken? cancellationToken = null);

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> and with the specified <paramref name="timeout"/>.
        /// </summary>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="timeout">The wait timeout.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        Task Wait(WaitKey key, int? timeout = null, CancellationToken? cancellationToken = null);

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> which does not time out.
        /// </summary>
        /// <typeparam name="T">The wait result type.</typeparam>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        Task<T> WaitIndefinitely<T>(WaitKey key, CancellationToken? cancellationToken = null);

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> which does not time out.
        /// </summary>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        Task WaitIndefinitely(WaitKey key, CancellationToken? cancellationToken = null);
    }

    /// <summary>
    ///     Enables arbitrary await-able things.
    /// </summary>
    public sealed class Waiter : IWaiter
    {
        private const int DefaultTimeoutValue = 5000;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Waiter"/> class with the default timeout.
        /// </summary>
        public Waiter()
            : this(DefaultTimeoutValue)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Waiter"/> class with the specified <paramref name="defaultTimeout"/>.
        /// </summary>
        /// <param name="defaultTimeout">The default timeout duration for message waits.</param>
        public Waiter(int defaultTimeout)
        {
            DefaultTimeout = defaultTimeout;
        }

        /// <summary>
        ///     Gets the default timeout duration, in milliseconds.
        /// </summary>
        public int DefaultTimeout { get; private set; }

        private bool Disposed { get; set; }
        private ConcurrentDictionary<WaitKey, ReaderWriterLockSlim> Locks { get; } = new ConcurrentDictionary<WaitKey, ReaderWriterLockSlim>();
        private ConcurrentDictionary<WaitKey, ConcurrentQueue<PendingWait>> Waits { get; } = new ConcurrentDictionary<WaitKey, ConcurrentQueue<PendingWait>>();

        /// <summary>
        ///     Cancels the oldest wait matching the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        public void Cancel(WaitKey key)
        {
            Disposition(key, wait =>
                wait.TaskCompletionSource.TrySetCanceled());
        }

        /// <summary>
        ///     Cancels all waits.
        /// </summary>
        public void CancelAll()
        {
            var keys = Waits.Keys.ToList();

            foreach (var key in keys)
            {
                Cancel(key);
            }
        }

        /// <summary>
        ///     Completes the oldest wait matching the specified <paramref name="key"/> with the specified <paramref name="result"/>.
        /// </summary>
        /// <typeparam name="T">The wait result type.</typeparam>
        /// <param name="key">The unique WaitKey for the wait.</param>
        /// <param name="result">The wait result.</param>
        public void Complete<T>(WaitKey key, T result)
        {
            Disposition(key, wait =>
                ((TaskCompletionSource<T>)wait.TaskCompletionSource).TrySetResult(result));
        }

        /// <summary>
        ///     Completes the oldest wait matching the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        public void Complete(WaitKey key)
        {
            Complete<object>(key, null);
        }

        /// <summary>
        ///     Returns a value indicating whether the specified <paramref name="key"/> is being waited upon.
        /// </summary>
        /// <param name="key">The unique WaitKey to check.</param>
        /// <returns>A value indicating whether the specified key is being waited upon.</returns>
        public bool IsWaitingFor(WaitKey key)
            => Waits.ContainsKey(key);

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing">A value indicating whether disposal is in progress.</param>
        public void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    CancelAll();
                }

                Disposed = true;
            }
        }

        /// <summary>
        ///     Throws the specified <paramref name="exception"/> on the oldest wait matching the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        /// <param name="exception">The Exception to throw.</param>
        public void Throw(WaitKey key, Exception exception)
        {
            Disposition(key, wait =>
                wait.TaskCompletionSource.TrySetException(exception));
        }

        /// <summary>
        ///     Causes the oldest wait matching the specified <paramref name="key"/> to time out.
        /// </summary>
        /// <param name="key">The unique WaitKey for the wait.</param>
        public void Timeout(WaitKey key)
        {
            Disposition(key, wait =>
                wait.TaskCompletionSource.TrySetException(new TimeoutException($"The wait timed out after {wait.Timeout} milliseconds")));
        }

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> and with the specified <paramref name="timeout"/>.
        /// </summary>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="timeout">The wait timeout, in milliseconds.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        public Task Wait(WaitKey key, int? timeout = null, CancellationToken? cancellationToken = null)
        {
            return Wait<object>(key, timeout, cancellationToken);
        }

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> and with the specified <paramref name="timeout"/>.
        /// </summary>
        /// <typeparam name="T">The wait result type.</typeparam>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="timeout">The wait timeout, in milliseconds.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        public Task<T> Wait<T>(WaitKey key, int? timeout = null, CancellationToken? cancellationToken = null)
        {
            timeout ??= DefaultTimeout;
            cancellationToken ??= CancellationToken.None;

            var taskCompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            var wait = new PendingWait(
                taskCompletionSource,
                timeout.Value,
                cancelAction: () => Cancel(key),
                timeoutAction: () => Timeout(key),
                cancellationToken.Value);

            // obtain a read lock for the key. this is necessary to prevent this code from adding a wait to the ConcurrentQueue
            // while the containing dictionary entry is being cleaned up in Disposition(), effectively discarding the new wait.
            var recordLock = Locks.GetOrAdd(key, new ReaderWriterLockSlim());

            recordLock.EnterReadLock();

            try
            {
                Waits.AddOrUpdate(key, new ConcurrentQueue<PendingWait>(new[] { wait }), (_, queue) =>
                {
                    queue.Enqueue(wait);
                    return queue;
                });
            }
            finally
            {
                recordLock.ExitReadLock();
            }

            // defer registration to prevent the wait from being dispositioned prior to being successfully queued this is a
            // concern if we are given a timeout of 0, or a cancellation token which is already cancelled
            wait.Register();
            return ((TaskCompletionSource<T>)wait.TaskCompletionSource).Task;
        }

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> which does not time out.
        /// </summary>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        public Task WaitIndefinitely(WaitKey key, CancellationToken? cancellationToken = null)
        {
            return WaitIndefinitely<object>(key, cancellationToken);
        }

        /// <summary>
        ///     Adds a new wait for the specified <paramref name="key"/> which does not time out.
        /// </summary>
        /// <typeparam name="T">The wait result type.</typeparam>
        /// <param name="key">A unique WaitKey for the wait.</param>
        /// <param name="cancellationToken">The cancellation token for the wait.</param>
        /// <returns>A Task representing the wait.</returns>
        public Task<T> WaitIndefinitely<T>(WaitKey key, CancellationToken? cancellationToken = null)
        {
            return Wait<T>(key, int.MaxValue, cancellationToken);
        }

        private void Disposition(WaitKey key, Action<PendingWait> action)
        {
            if (Waits.TryGetValue(key, out var queue) && queue.TryDequeue(out var wait))
            {
                action(wait);
                wait.Dispose();

                if (Locks.TryGetValue(key, out var recordLock))
                {
                    // enter a read lock first; TryPeek and TryDequeue are atomic so there's no risky operation until later.
                    recordLock.EnterUpgradeableReadLock();

                    try
                    {
                        // clean up entries in the Waits and Locks dictionaries if the corresponding ConcurrentQueue is empty.
                        // this is tricky, because we don't want to remove a record if another thread is in the process of
                        // enqueueing a new wait.
                        if (queue.IsEmpty)
                        {
                            // enter the write lock to prevent Wait() (which obtains a read lock) from enqueuing any more waits
                            // before we can delete the dictionary record. it's ok and expected that Wait() might add this record
                            // back to the dictionary as soon as this unblocks; we're preventing new waits from being discarded if
                            // they are added by another thread just prior to the TryRemove() operation below.
                            recordLock.EnterWriteLock();

                            try
                            {
                                // check the queue again to ensure Wait() didn't enqueue anything between the last check and when
                                // we entered the write lock. this is guaranteed to be safe since we now have exclusive access to
                                // the record and it should be impossible to remove a record containing a non-empty queue
                                if (queue.IsEmpty)
                                {
                                    Waits.TryRemove(key, out _);
                                    Locks.TryRemove(key, out _);
                                }
                            }
                            finally
                            {
                                recordLock.ExitWriteLock();
                            }
                        }
                    }
                    finally
                    {
                        recordLock.ExitUpgradeableReadLock();
                    }
                }
            }
        }

        /// <summary>
        ///     The composite value for the wait dictionary.
        /// </summary>
        internal class PendingWait : IDisposable
        {
            /// <summary>
            ///     Initializes a new instance of the <see cref="PendingWait"/> class.
            /// </summary>
            /// <param name="taskCompletionSource">The task completion source for the wait task.</param>
            /// <param name="timeout">The number of milliseconds after which the wait is to time out.</param>
            /// <param name="cancelAction">The action to invoke when the task is cancelled.</param>
            /// <param name="timeoutAction">The action to invoke when the task times out.</param>
            /// <param name="cancellationToken">The cancellation token for the wait.</param>
            public PendingWait(dynamic taskCompletionSource, int timeout, Action cancelAction, Action timeoutAction, CancellationToken cancellationToken)
            {
                TaskCompletionSource = taskCompletionSource;
                Timeout = timeout;
                CancelAction = cancelAction;
                TimeoutAction = timeoutAction;
                CancellationToken = cancellationToken;
            }

            /// <summary>
            ///     Gets the task completion source for the wait task.
            /// </summary>
            public dynamic TaskCompletionSource { get; }

            /// <summary>
            ///     Gets the number of milliseconds after which the wait is to time out.
            /// </summary>
            public int Timeout { get; }

            private Action CancelAction { get; set; }
            private CancellationToken CancellationToken { get; set; }
            private CancellationTokenRegistration CancellationTokenRegistration { get; set; }
            private bool Disposed { get; set; }
            private Action TimeoutAction { get; set; }
            private CancellationTokenRegistration TimeoutTokenRegistration { get; set; }
            private CancellationTokenSource TimeoutTokenSource { get; set; }

            /// <summary>
            ///     Releases the managed and unmanaged resources used by the <see cref="PendingWait"/>.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            ///     Register cancellation and timeout actions.
            /// </summary>
            public void Register()
            {
                CancellationTokenRegistration = CancellationToken.Register(() => CancelAction());

                TimeoutTokenSource = new CancellationTokenSource(Timeout);
                TimeoutTokenRegistration = TimeoutTokenSource.Token.Register(() => TimeoutAction());
            }

            /// <summary>
            ///     Releases the managed and unmanaged resources used by the <see cref="PendingWait"/>.
            /// </summary>
            /// <param name="disposing">A value indicating whether the object is being disposed.</param>
            protected virtual void Dispose(bool disposing)
            {
                if (!Disposed)
                {
                    if (disposing)
                    {
                        CancellationTokenRegistration.Dispose();
                        TimeoutTokenSource.Dispose();
                        TimeoutTokenRegistration.Dispose();
                    }

                    Disposed = true;
                }
            }
        }
    }
}