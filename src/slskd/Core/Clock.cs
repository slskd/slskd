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
    using System.Timers;

    /// <summary>
    ///     The application clock, used for time based events.
    /// </summary>
    public static class Clock
    {
        static Clock()
        {
            EveryMinuteTimer.Elapsed += (_, _) => EveryMinute?.Invoke(null, EventArgs.Empty);
            EveryFiveMinutesTimer.Elapsed += (_, _) => EveryFiveMinutes?.Invoke(null, EventArgs.Empty);
            EveryHourTimer.Elapsed += (_, _) => EveryHour?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        ///     Fires every 60 seconds.
        /// </summary>
        public static event EventHandler EveryMinute;

        /// <summary>
        ///     Fires every 5 minutes.
        /// </summary>
        public static event EventHandler EveryFiveMinutes;

        /// <summary>
        ///     Fires every 60 minutes.
        /// </summary>
        public static event EventHandler EveryHour;

        private static Timer EveryMinuteTimer { get; } = CreateTimer(interval: 1000 * 60);
        private static Timer EveryFiveMinutesTimer { get; } = CreateTimer(interval: 1000 * 60 * 5);
        private static Timer EveryHourTimer { get; } = CreateTimer(interval: 1000 * 60 * 60);

        private static Timer CreateTimer(double interval) => new() { AutoReset = true, Interval = interval, Enabled = true };
    }
}