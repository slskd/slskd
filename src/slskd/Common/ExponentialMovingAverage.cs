// <copyright file="ExponentialMovingAverage.cs" company="slskd Team">
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

using System;

namespace slskd
{
    /// <summary>
    ///     An exponential moving average.
    /// </summary>
    public class ExponentialMovingAverage
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ExponentialMovingAverage"/> class.
        /// </summary>
        /// <param name="smoothingFactor"></param>
        public ExponentialMovingAverage(double smoothingFactor)
        {
            SmoothingFactor = smoothingFactor;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ExponentialMovingAverage"/> class.
        /// </summary>
        /// <param name="smoothingFactor"></param>
        /// <param name="onUpdate"></param>
        public ExponentialMovingAverage(double smoothingFactor, Action<double> onUpdate = null)
            : this(smoothingFactor)
        {
            OnUpdate = onUpdate;
        }

        /// <summary>
        ///     Gets the current value.
        /// </summary>
        public double Value { get; private set; } = 0;

        /// <summary>
        ///     Gets a value indicating whether the average has been initialized.
        /// </summary>
        public bool Initialized { get; private set; } = false;

        private double SmoothingFactor { get; }
        private Action<double> OnUpdate { get; }

        /// <summary>
        ///     Updates the average with a new value.
        /// </summary>
        /// <param name="value"></param>
        public void Update(double value)
        {
            Value = !Initialized ? value : ((value - Value) * SmoothingFactor) + Value;
            Initialized = true;
            OnUpdate?.Invoke(Value);
        }
    }
}
