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
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using slskd.Transfers;

public record Event
{
}

public record DownloadFileCompleteEvent : Event
{
    public string LocalFilename { get; init; }
    public string RemoteFilename { get; init; }
    public string Username { get; init; }
    public Transfer Transfer { get; init; }
}

public record DownloadDirectoryCompleteEvent : Event
{
    public string LocalDirectoryName { get; init; }
    public string RemoteFilename { get; init; }
    public string Username { get; init; }
}

/// <summary>
///     The application event bus.
/// </summary>
public class EventBus
{
    private ILogger Log { get; } = Serilog.Log.ForContext<EventBus>();
    private ConcurrentDictionary<Type, ConcurrentBag<Func<Event, Task>>> Subscriptions { get; } = new();

    public async Task RaiseAsync<T>(T data)
        where T : Event
    {
        Log.Debug("Handling {Type}: {Data}", typeof(T), data);

        if (!Subscriptions.TryGetValue(typeof(T), out var callbacks))
        {
            Log.Debug("No subscribers for {Type}", typeof(T));
        }

        // fire and forget on a few levels! there's not much we can (or want to)
        // do here, just rely on the continuation to log the error
        _ = callbacks.Select(callback => Task.Run(() =>
        {
            _ = callback(data);
        }).ContinueWith((task, obj) =>
        {
            Log.Error(task?.Exception, "Error handling {Type}: {Message}", typeof(T), task?.Exception?.Message);
        }, TaskContinuationOptions.OnlyOnFaulted));
    }

    public void Subscribe<T>(Func<T, Task> callback)
        where T : Event
    {
    }
}