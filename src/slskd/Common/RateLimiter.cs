// <copyright file="RateLimiter.cs" company="slskd Team">
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
    using System.ComponentModel;

    /// <summary>
    ///     Ensures a minimum interval between successive invocations of a delegate.
    /// </summary>
    public class RateLimiter : IDisposable
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimiter"/> class.
        /// </summary>
        /// <param name="interval">The minimum interval between invocations.</param>
        public RateLimiter(int interval)
        {
            Timer = new System.Timers.Timer(interval)
            {
                AutoReset = true,
            };

            Timer.Elapsed += (s, e) =>
            {
                Staged?.Invoke();
                Staged = null;
            };
        }

        private bool Disposed { get; set; }
        private bool Init { get; set; }
        private Action Staged { get; set; }
        private System.Timers.Timer Timer { get; set; }

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
                    Timer.Dispose();
                }

                Disposed = true;
            }
        }
    }
}