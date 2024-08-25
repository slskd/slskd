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

namespace slskd.Events;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
///     <para>
///         This design was chosen over built-in C# events to give greater control over how events are dispatched,
///         and bound.
///     </para>
/// </remarks>
public class EventBus
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EventBus"/> class.
    /// </summary>
    /// <param name="contextFactory"/>
    public EventBus(IDbContextFactory<EventsDbContext> contextFactory)
    {
        ContextFactory = contextFactory;
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<EventBus>();
    private IDbContextFactory<EventsDbContext> ContextFactory { get; }

    /// <summary>
    ///     Gets the internal list of event subscriptions.
    /// </summary>
    /// <remarks>
    ///     Note that the value is a dictionary to prevent multiple subscriptions from the same subscriber, and to
    ///     support unsubscribing.
    /// </remarks>
    private ConcurrentDictionary<Type, ConcurrentDictionary<string, object>> Subscriptions { get; } = new();

    /// <summary>
    ///     Raises an event.
    /// </summary>
    /// <param name="data">The event data.</param>
    /// <typeparam name="T">The Type of the event.</typeparam>
    public virtual void Raise<T>(T data)
        where T : Event
    {
        Log.Debug("Handling {Type}: {Data}", typeof(T), data);

        // save the event to the database before broadcasting to consumers
        var ctx = ContextFactory.CreateDbContext();
        ctx.Add(EventRecord.From<T>(data));
        ctx.SaveChanges();

        // broadcast the event in a fire-and-forget fashion
        // we don't need to wait for anything, just need to kick off the tasks
        // if something throws it will be logged and the consumer can figure it out
        if (Subscriptions.TryGetValue(typeof(T), out var subscribers))
        {
            Log.Debug("{Count} subscriber(s) for {Type}: {Names}", subscribers.Count, typeof(T), string.Join(", ", subscribers.Keys));

            // we don't care about any of these tasks; contractually we are only obligated to invoke them
            _ = Task.WhenAll(subscribers.Select(subscriber =>
                    Task.Run(() => (subscriber.Value as Func<T, Task>)(data))
                        .ContinueWith(task => Log.Error(task.Exception, "Subscriber {Name} for {Type} encountered an error: {Message}", subscriber.Key, typeof(T), task.Exception.Message))));
        }
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
            addValue: new ConcurrentDictionary<string, object>(
                new Dictionary<string, object> { [subscriber] = callback }),
            updateValueFactory: (_, subscribers) =>
            {
                subscribers.AddOrUpdate(
                    key: subscriber,
                    addValue: callback,
                    updateValueFactory: (_, existingSubscription) =>
                    {
                        if (existingSubscription is not null)
                        {
                            Log.Debug("Warning! {Type} subscriber {Name} attempted to create a redundant subscription.  The existing subscription was overwritten.", typeof(T), subscriber);
                        }

                        return callback;
                    });

                return subscribers;
            });

        Log.Debug("Subscribed {Name} to {Type}", subscriber, typeof(T));
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