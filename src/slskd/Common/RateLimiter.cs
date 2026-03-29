// <copyright file="RateLimiter.cs" company="JP Dillingham">
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
    using System.ComponentModel;
    using System.Threading;

    /// <summary>
    ///     Ensures a minimum interval between successive invocations of a delegate.
    /// </summary>
    public class RateLimiter : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimiter"/> class.
        /// </summary>
        /// <param name="interval">The minimum interval between invocations.</param>
        /// <param name="concurrencyLimit">
        ///     Limit the number of concurrent executions of the specified action, in cases where the logic runs slower than the configured interval. Set this to null to remove the limit.
        /// </param>
        /// <param name="flushOnDispose">A value indicating whether pending action(s) should be executed during disposal.</param>
        public RateLimiter(int interval, int? concurrencyLimit = 1, bool flushOnDispose = false)
        {
            Timer = new System.Timers.Timer(interval)
            {
                AutoReset = true,
            };

            Timer.Elapsed += Timer_Elapsed;

            FlushOnDispose = flushOnDispose;

            if (concurrencyLimit.HasValue)
            {
                ConcurrentExecutionPreventionSemaphore = new SemaphoreSlim(concurrencyLimit.Value, concurrencyLimit.Value);
            }
        }

        private bool Disposed { get; set; }
        private bool FlushOnDispose { get; }
        private bool Init { get; set; }
        private Action Staged { get; set; }
        private System.Timers.Timer Timer { get; set; }
        private SemaphoreSlim ConcurrentExecutionPreventionSemaphore { get; } = null;

        /// <summary>
        ///     Releases all resources used by the <see cref="Component"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Invokes the specified <paramref name="action"/>, dropping invocations created prior to the elapse of the
        ///     configured interval.
        /// </summary>
        /// <param name="action">The delegate to invoke.</param>
        public void Invoke(Action action)
        {
            if (!Init)
            {
                Init = true;
                Timer.Start();
                action();
                return;
            }

            Staged = action;
        }

        /// <summary>
        ///     Releases all resources used by the <see cref="Component"/>.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    Timer.Elapsed -= Timer_Elapsed;

                    // if an action is staged, invoke it to 'flush'
                    if (FlushOnDispose)
                    {
                        Staged?.Invoke();
                    }

                    Staged = null;
                    Timer.Dispose();
                }

                Disposed = true;
            }
        }

        private void Timer_Elapsed(object sender, EventArgs args)
        {
            if (ConcurrentExecutionPreventionSemaphore?.Wait(0) ?? true)
            {
                try
                {
                    Staged?.Invoke();
                    Staged = null;
                }
                finally
                {
                    ConcurrentExecutionPreventionSemaphore?.Release();
                }
            }
        }
    }
}