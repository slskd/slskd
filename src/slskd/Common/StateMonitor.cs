// <copyright file="StateMonitor.cs" company="slskd Team">
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
    ///     Used for notifications when <see cref="ApplicationState"/> changes.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public class StateMonitor<T> : IStateMonitor<T>
    {
        private event Action<(T, T)> Changed;

        /// <summary>
        ///     Gets the current application state.
        /// </summary>
        public T CurrentValue { get; private set; } = (T)Activator.CreateInstance(typeof(T));

        private object Lock { get; } = new object();

        /// <summary>
        ///     Registers a listener to be called whenever the stracked state changes.
        /// </summary>
        /// <param name="listener">Registers a listener to be called whenver state changes.</param>
        /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
        public IDisposable OnChange(Action<(T Previous, T Current)> listener)
        {
            var disposable = new StateTrackerDisposable<T>(this, listener);
            Changed += disposable.OnChange;
            return disposable;
        }

        /// <summary>
        ///     Replaces the current state with the value resolved by the <paramref name="setter"/>.
        /// </summary>
        /// <param name="setter">Given the current state, resolves a new state value.</param>
        /// <returns>The updated state.</returns>
        public T SetValue(Func<T, T> setter)
        {
            lock (Lock)
            {
                var previous = CurrentValue.ToJson().ToObject<T>();
                CurrentValue = setter(CurrentValue);

                Changed?.Invoke((previous, CurrentValue));
                return CurrentValue;
            }
        }

#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type
        private class StateTrackerDisposable<T> : IDisposable
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type
        {
            public StateTrackerDisposable(StateMonitor<T> stateMonitor, Action<(T Previous, T Current)> listener)
            {
                StateMonitor = stateMonitor;
                Listener = listener;
            }

            private bool Disposed { get; set; }
            private Action<(T Previous, T Current)> Listener { get; }
            private StateMonitor<T> StateMonitor { get; }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            public void OnChange((T Previous, T Current) args) => Listener.Invoke(args);

            protected virtual void Dispose(bool disposing)
            {
                if (!Disposed)
                {
                    if (disposing)
                    {
                        StateMonitor.Changed -= OnChange;
                    }

                    Disposed = true;
                }
            }
        }
    }
}
