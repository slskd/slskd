// <copyright file="Retry.cs" company="slskd Team">
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
        /// <param name="onFailure">An action to execute on failure.</param>
        /// <param name="maxAttempts">The maximum number of retry attempts.</param>
        /// <param name="maxDelayInMilliseconds">The maximum delay in milliseconds.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>The execution context.</returns>
        public static async Task Do(Func<Task> task, Func<int, Exception, bool> isRetryable = null, Action<int, Exception> onFailure = null, int maxAttempts = 3, int maxDelayInMilliseconds = int.MaxValue, CancellationToken cancellationToken = default)
        {
            await Do<object>(async () =>
            {
                await task();
                return Task.FromResult<object>(null);
            }, isRetryable, onFailure, maxAttempts, maxDelayInMilliseconds, cancellationToken);
        }

        /// <summary>
        ///     Executes logic with the specified retry parameters.
        /// </summary>
        /// <param name="task">The logic to execute.</param>
        /// <param name="isRetryable">A function returning a value indicating whether the last Exception is retryable.</param>
        /// <param name="onFailure">An action to execute on failure.</param>
        /// <param name="maxAttempts">The maximum number of retry attempts.</param>
        /// <param name="maxDelayInMilliseconds">The maximum delay in miliseconds.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <typeparam name="T">The Type of the logic return value.</typeparam>
        /// <returns>The execution context.</returns>
        public static async Task<T> Do<T>(Func<Task<T>> task, Func<int, Exception, bool> isRetryable = null, Action<int, Exception> onFailure = null, int maxAttempts = 3, int maxDelayInMilliseconds = int.MaxValue, CancellationToken cancellationToken = default)
        {
            isRetryable ??= (_, _) => true;
            onFailure ??= (_, _) => { };

            var exceptions = new List<Exception>();

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
                        var (delay, jitter) = Compute.ExponentialBackoffDelay(attempts, maxDelayInMilliseconds);
                        await Task.Delay(delay + jitter, cancellationToken);
                    }

                    return await task();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);

                    try
                    {
                        onFailure(attempts + 1, ex);

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