// <copyright file="ManagedState.cs" company="slskd Team">
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
    using System.Text.Json;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     Provides observable read access for state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public interface IStateMonitor<T>
    {
        /// <summary>
        ///     Gets the current state.
        /// </summary>
        T CurrentValue { get; }

        /// <summary>
        ///     Registers a listener to be called whenever the tracked state changes.
        /// </summary>
        /// <param name="listener">Registers a listener to be called whenver state changes.</param>
        /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
        IDisposable OnChange(Action<(T Previous, T Current)> listener);
    }

    /// <summary>
    ///     Provides write access for state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public interface IStateMutator<T>
    {
        /// <summary>
        ///     Replaces the current state with the value resolved by the <paramref name="setter"/>.
        /// </summary>
        /// <param name="setter">Given the current state, resolves a new state value.</param>
        /// <returns>The updated state.</returns>
        T SetValue(Func<T, T> setter);
    }

    /// <summary>
    ///     Provides point-in-time read access for state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public interface IStateSnapshot<out T>
    {
        /// <summary>
        ///     Gets the snapshotted state.
        /// </summary>
        T Value { get; }
    }

    /// <summary>
    ///     Provides observable management of state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public interface IManagedState<T> : IStateMonitor<T>, IStateMutator<T>
    {
        /// <summary>
        ///     Gets a point-in-time snapshot of the current state.
        /// </summary>
        IStateSnapshot<T> Snapshot { get; }
    }

    /// <summary>
    ///     ManagedState extension methods.
    /// </summary>
    public static class ManagedStateExtensions
    {
        /// <summary>
        ///     <para>
        ///         Adds managed state of an object of the specified <typeparamref name="T"/> to the specified <see cref="IServiceCollection"/>.
        ///     </para>
        ///     <para>
        ///         <list type="bullet">
        ///             <item>A singleton instance of <see cref="IManagedState{T}"/> to be used by instances requiring full control over the state</item>
        ///             <item>A singleton instance of <see cref="IStateMutator{T}"/> to be used by instances requiring the ability to mutate the state</item>
        ///             <item>A singleton instance of <see cref="IStateMonitor{T}"/> to be used by instances requiring observable read access to the state</item>
        ///             <item>A transient instance of <see cref="IStateSnapshot{T}"/> to be used by scoped or transient instances requiring the current state</item>
        ///         </list>
        ///     </para>
        /// </summary>
        /// <typeparam name="T">The type of the managed state object.</typeparam>
        /// <param name="services">The IServiceCollection to which the managed state is to be added.</param>
        /// <param name="setter">An optional anonymous function used to apply an initial state mutation.</param>
        /// <returns>The IServiceCollection with managed state added.</returns>
        public static IServiceCollection AddManagedState<T>(this IServiceCollection services, Func<T, T> setter = null)
            where T : class
        {
            services.AddSingleton<IManagedState<T>, ManagedState<T>>();
            services.AddSingleton(services =>
            {
                var monitor = (IStateMutator<T>)services.GetRequiredService<IManagedState<T>>();

                if (setter != null)
                {
                    monitor.SetValue(setter);
                }

                return monitor;
            });
            services.AddSingleton(services => (IStateMonitor<T>)services.GetRequiredService<IManagedState<T>>());
            services.AddTransient(services => services.GetRequiredService<IManagedState<T>>().Snapshot);

            return services;
        }
    }

    /// <summary>
    ///     Provides a point-in-time snapshot of state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public class StateSnapshot<T> : IStateSnapshot<T>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="StateSnapshot{T}"/> class.
        /// </summary>
        /// <param name="value">The current value of the state.</param>
        public StateSnapshot(T value)
        {
            Value = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        }

        /// <summary>
        ///     Gets a snapshot of the current application state.
        /// </summary>
        public T Value { get; }
    }

    /// <summary>
    ///     Provides observable management of state objects.
    /// </summary>
    /// <typeparam name="T">The type of the tracked state object.</typeparam>
    public class ManagedState<T> : IManagedState<T>
    {
        private event Action<(T, T)> Changed;

        /// <summary>
        ///     Gets the current application state.
        /// </summary>
        public T CurrentValue { get; private set; } = (T)Activator.CreateInstance(typeof(T));

        /// <summary>
        ///     Gets a point-in-time snapshot of the current state.
        /// </summary>
        public IStateSnapshot<T> Snapshot => new StateSnapshot<T>(CurrentValue);

        private object Lock { get; } = new object();

        /// <summary>
        ///     Registers a listener to be called whenever the stracked state changes.
        /// </summary>
        /// <param name="listener">Registers a listener to be called whenver state changes.</param>
        /// <returns>An <see cref="IDisposable"/> which should be disposed to stop listening for changes.</returns>
        public IDisposable OnChange(Action<(T Previous, T Current)> listener)
        {
            var disposable = new ManagedStateDisposable<T>(this, listener);
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
#pragma warning disable S3260 // Non-derived "private" classes and records should be "sealed"
        private class ManagedStateDisposable<T> : IDisposable
#pragma warning restore S3260 // Non-derived "private" classes and records should be "sealed"
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type
        {
            public ManagedStateDisposable(ManagedState<T> stateMonitor, Action<(T Previous, T Current)> listener)
            {
                StateMonitor = stateMonitor;
                Listener = listener;
            }

            private bool Disposed { get; set; }
            private Action<(T Previous, T Current)> Listener { get; }
            private ManagedState<T> StateMonitor { get; }

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
