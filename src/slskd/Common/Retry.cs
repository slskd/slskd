// <copyright file="Retry.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Retry logic.
    /// </summary>
    public static class Retry
    {
        /// <summary>
        ///     Executes logic with the specified retry parameters.
        /// </summary>
        /// <param name="task">The logic to execute.</param>
        /// <param name="isRetryable">A function returning a value indicating whether the last Exception is retryable.</param>
        /// <param name="onRetry">An action to execute before beginning a retry attempt.</param>
        /// <param name="onFailure">An action to execute on failure.</param>
        /// <param name="maxAttempts">The maximum number of retry attempts.</param>
        /// <param name="baseDelayInMilliseconds">The base delay in milliseconds.</param>
        /// <param name="maxDelayInMilliseconds">The maximum delay in milliseconds.</param>
        /// <param name="exceptionHistoryLimit">The maximum number of Exceptions to keep in history.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The execution context.</returns>
        public static async Task Do(
            Func<Task> task,
            Func<int, Exception, bool> isRetryable = null,
            Action<int, int> onRetry = null,
            Action<int, Exception> onFailure = null,
            int maxAttempts = 3,
            int baseDelayInMilliseconds = 1000,
            int maxDelayInMilliseconds = int.MaxValue,
            int exceptionHistoryLimit = 5,
            CancellationToken cancellationToken = default)
        {
            await Do<object>(async () =>
            {
                await task();
                return Task.FromResult<object>(null);
            }, isRetryable, onRetry, onFailure, maxAttempts, baseDelayInMilliseconds, maxDelayInMilliseconds, exceptionHistoryLimit, cancellationToken);
        }

        /// <summary>
        ///     Executes logic with the specified retry parameters.
        /// </summary>
        /// <param name="task">The logic to execute.</param>
        /// <param name="isRetryable">A function returning a value indicating whether the last Exception is retryable.</param>
        /// <param name="onRetry">An action to execute before beginning a retry attempt.</param>
        /// <param name="onFailure">An action to execute on failure.</param>
        /// <param name="maxAttempts">The maximum number of retry attempts.</param>
        /// <param name="baseDelayInMilliseconds">The base delay in milliseconds.</param>
        /// <param name="maxDelayInMilliseconds">The maximum delay in milliseconds.</param>
        /// <param name="exceptionHistoryLimit">The maximum number of Exceptions to keep in history.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <typeparam name="T">The Type of the logic return value.</typeparam>
        /// <returns>The execution context.</returns>
        public static async Task<T> Do<T>(
            Func<Task<T>> task,
            Func<int, Exception, bool> isRetryable = null,
            Action<int, int> onRetry = null,
            Action<int, Exception> onFailure = null,
            int maxAttempts = 3,
            int baseDelayInMilliseconds = 1000,
            int maxDelayInMilliseconds = int.MaxValue,
            int exceptionHistoryLimit = 5,
            CancellationToken cancellationToken = default)
        {
            isRetryable ??= (_, _) => true;

            var exceptions = new Queue<Exception>();

            for (int attempts = 0; attempts < maxAttempts; attempts++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                try
                {
                    if (attempts > 0)
                    {
                        var (delay, jitter) = Compute.ExponentialBackoffDelay(attempts, baseDelayInMilliseconds, maxDelayInMilliseconds);

                        onRetry?.Invoke(attempts + 1, delay + jitter);
                        await Task.Delay(delay + jitter, cancellationToken);
                    }

                    return await task();
                }
                catch (Exception ex)
                {
                    exceptions.Enqueue(ex);

                    if (exceptions.Count > exceptionHistoryLimit)
                    {
                        exceptions.Dequeue();
                    }

                    try
                    {
                        onFailure?.Invoke(attempts + 1, ex);

                        if (!isRetryable(attempts + 1, ex))
                        {
                            break;
                        }
                    }
                    catch (Exception retryEx)
                    {
                        throw new RetryException($"Failed to retry operation: {retryEx.Message}", ex);
                    }
                }
            }

            throw new AggregateException(exceptions);
        }
    }
}