// <copyright file="Clock.cs" company="slskd Team">
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
    using System.Threading.Tasks;
    using System.Timers;

    /// <summary>
    ///     The application clock, used for time based events.
    /// </summary>
    public static class Clock
    {
        static Clock()
        {
            EveryMinuteTimer.Elapsed += (_, _) => Fire(EveryMinute);
            EveryThirtySecondsTimer.Elapsed += (_, _) => Fire(EveryThirtySeconds);
            EveryFiveMinutesTimer.Elapsed += (_, _) => Fire(EveryFiveMinutes);
            EveryThirtyMinutesTimer.Elapsed += (_, _) => Fire(EveryThirtyMinutes);
            EveryHourTimer.Elapsed += (_, _) => Fire(EveryHour);
        }

        /// <summary>
        ///     Fires every 5 minutes.
        /// </summary>
        public static event EventHandler<ClockEventArgs> EveryFiveMinutes;

        /// <summary>
        ///     Fires every hour.
        /// </summary>
        public static event EventHandler<ClockEventArgs> EveryHour;

        /// <summary>
        ///     Fires every minute.
        /// </summary>
        public static event EventHandler<ClockEventArgs> EveryMinute;

        /// <summary>
        ///     Fires every 30 seconds.
        /// </summary>
        public static event EventHandler<ClockEventArgs> EveryThirtySeconds;

        /// <summary>
        ///     Fires every 30 minutes.
        /// </summary>
        public static event EventHandler<ClockEventArgs> EveryThirtyMinutes;

        private static Timer EveryFiveMinutesTimer { get; } = CreateTimer(interval: 1000 * 60 * 5);
        private static Timer EveryHourTimer { get; } = CreateTimer(interval: 1000 * 60 * 60);
        private static Timer EveryMinuteTimer { get; } = CreateTimer(interval: 1000 * 60);
        private static Timer EveryThirtySecondsTimer { get; } = CreateTimer(interval: 1000 * 30);
        private static Timer EveryThirtyMinutesTimer { get; } = CreateTimer(interval: 1000 * 60 * 30);

        /// <summary>
        ///     Starts the clock.
        /// </summary>
        /// <returns>A Task that completes when all startup events have finished processing.</returns>
        public static Task StartAsync()
        {
            EveryMinuteTimer.Enabled = true;
            EveryThirtySecondsTimer.Enabled = true;
            EveryFiveMinutesTimer.Enabled = true;
            EveryThirtyMinutesTimer.Enabled = true;
            EveryHourTimer.Enabled = true;

            var firstRunArgs = new ClockEventArgs(firstRun: true);

            return Task.WhenAll(
                Task.Run(() => Fire(EveryMinute, firstRunArgs)),
                Task.Run(() => Fire(EveryThirtySeconds, firstRunArgs)),
                Task.Run(() => Fire(EveryFiveMinutes, firstRunArgs)),
                Task.Run(() => Fire(EveryThirtyMinutes, firstRunArgs)),
                Task.Run(() => Fire(EveryHour, firstRunArgs)));
        }

        /// <summary>
        ///     Stops the clock.
        /// </summary>
        public static void Stop()
        {
            EveryMinuteTimer.Stop();
            EveryThirtySecondsTimer.Stop();
            EveryFiveMinutesTimer.Stop();
            EveryThirtyMinutesTimer.Stop();
            EveryHourTimer.Stop();
        }

        private static Timer CreateTimer(double interval) => new() { AutoReset = true, Interval = interval, Enabled = false };
        private static void Fire(EventHandler<ClockEventArgs> e, ClockEventArgs args = null) => e?.Invoke(null, args ?? new ClockEventArgs());
    }

    /// <summary>
    ///     EventArgs for the application clock.
    /// </summary>
    public class ClockEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ClockEventArgs"/> class.
        /// </summary>
        /// <param name="firstRun">A value indicating whether this event was raised when the clock was started.</param>
        public ClockEventArgs(bool firstRun = false)
        {
            FirstRun = firstRun;
        }

        /// <summary>
        ///     Gets a value indicating whether this event was raised when the click was started.
        /// </summary>
        public bool FirstRun { get; init; }
    }
}