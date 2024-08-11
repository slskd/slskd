// <copyright file="EventBus.cs" company="slskd Team">
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

namespace slskd;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

/// <summary>
///     The event bus supporting interoperability and ancillary functions.
/// </summary>
/// <remarks>
///     <para>
///         This "bus" is meant to mimic an event bus you'd find in a distributed system; fire and forget. If an
///         Exception is encountered while an event is being raised, it will be logged and swallowed.
///     </para>
///     <para>
///         It is also intended to be used *ONLY* for ancillary and/or third party logic that isn't part of the
///         core application.  The core application should continue to use regular old C# events and method calls
///         to communicate within and among modules.
///     </para>
/// </remarks>
public class EventBus
{
    private ILogger Log { get; } = Serilog.Log.ForContext<EventBus>();

    /// <summary>
    ///     Gets the internal list of event subscriptions.
    /// </summary>
    /// <remarks>
    ///     Note that the value is a dictionary to prevent multiple subscriptions from the same subscriber, and to
    ///     support unsubscribing.
    /// </remarks>
    private ConcurrentDictionary<Type, ConcurrentDictionary<string, Func<Event, Task>>> Subscriptions { get; } = new();

    /// <summary>
    ///     Raises an event.
    /// </summary>
    /// <param name="data">The event data.</param>
    /// <typeparam name="T">The Type of the event.</typeparam>
    public virtual void Raise<T>(T data)
        where T : Event
    {
        Log.Debug("Handling {Type}: {Data}", typeof(T), data);

        if (!Subscriptions.TryGetValue(typeof(T), out var subscribers))
        {
            Log.Debug("No subscribers for {Type}", typeof(T));
        }

        // we don't care about any of these tasks; contractually we are only obligated to invoke them
        _ = subscribers.Select(subscriber =>
            Task.Run(() => subscriber.Value(data)).ContinueWith(task =>
            {
                Log.Error(task.Exception, "Subscriber {Name} for {Type} encountered an error: {Message}", subscriber.Key, typeof(T), task.Exception.Message);
            }, continuationOptions: TaskContinuationOptions.OnlyOnFaulted));
    }

    /// <summary>
    ///     Subscribes a <paramref name="subscriber"/>'s <paramref name="callback"/> to an event.
    /// </summary>
    /// <param name="subscriber">The unique name of the subscriber.</param>
    /// <param name="callback">The callback function to execute when an event is raised.</param>
    /// <typeparam name="T">The Type of the event.</typeparam>
    /// <exception cref="ArgumentException">Thrown if the specified <paramref name="subscriber"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the specified <paramref name="callback"/> is null.</exception>
    public virtual void Subscribe<T>(string subscriber, Func<T, Task> callback)
        where T : Event
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriber);

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        Subscriptions.AddOrUpdate(
            key: typeof(T),
            addValue: new ConcurrentDictionary<string, Func<Event, Task>>(
                new Dictionary<string, Func<Event, Task>> { [subscriber] = (Func<Event, Task>)callback }),
            updateValueFactory: (_, subscribers) =>
            {
                subscribers.AddOrUpdate(
                    key: subscriber,
                    addValue: (Func<Event, Task>)callback,
                    updateValueFactory: (_, existingSubscription) =>
                    {
                        if (existingSubscription is not null)
                        {
                            Log.Debug("Warning! {Type} subscriber {Name} attempted to create a redundant subscription.  The existing subscription was overwritten.", typeof(T), subscriber);
                        }

                        return (Func<Event, Task>)callback;
                    });

                return subscribers;
            });
    }

    /// <summary>
    ///     Unsubscribes a <paramref name="subscriber"/> from an event.
    /// </summary>
    /// <remarks>
    ///     Will not throw if a subscription doesn't exist.
    /// </remarks>
    /// <typeparam name="T">The Type of the event.</typeparam>
    /// <param name="subscriber">The unique name of the subscriber.</param>
    public virtual void Unsubscribe<T>(string subscriber)
    {
        if (Subscriptions.TryGetValue(typeof(T), out var subscriptions))
        {
            subscriptions.TryRemove(subscriber, out _);
        }
    }
}