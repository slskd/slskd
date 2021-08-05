// <copyright file="IStateMonitor.cs" company="slskd Team">
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

    /// <summary>
    ///     Provideds observable management of state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public interface IStateMonitor<T>
    {
        /// <summary>
        ///     Gets the current application state.
        /// </summary>
        T CurrentValue { get; }

        /// <summary>
        ///     Registers a listener to be called whenever the tracked state changes.
        /// </summary>
        /// <param name="listener">Registers a listener to be called whenver state changes.</param>
        /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
        IDisposable OnChange(Action<(T Previous, T Current)> listener);

        /// <summary>
        ///     Replaces the current state with the value resolved by the <paramref name="setter"/>.
        /// </summary>
        /// <param name="setter">Given the current state, resolves a new state value.</param>
        /// <returns>The updated state.</returns>
        T SetValue(Func<T, T> setter);
    }
}
