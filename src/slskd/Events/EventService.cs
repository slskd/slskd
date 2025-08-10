// <copyright file="EventService.cs" company="slskd Team">
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serilog;

/// <summary>
///     Manages events.
/// </summary>
public class EventService
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="EventService"/> class.
    /// </summary>
    /// <param name="contextFactory"></param>
    public EventService(IDbContextFactory<EventsDbContext> contextFactory)
    {
        ContextFactory = contextFactory;
    }

    private IDbContextFactory<EventsDbContext> ContextFactory { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<EventService>();

    /// <summary>
    ///     Gets list of events, optionally applying the specified <paramref name="offset"/> and <paramref name="limit"/>.
    /// </summary>
    /// <param name="offset">The beginning offset for the page.</param>
    /// <param name="limit">The page size limit.</param>
    /// <returns>The retrieved list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <paramref name="offset"/> is less than zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <paramref name="limit"/> is zero.</exception>
    public virtual IReadOnlyList<EventRecord> Get(int offset = 0, int limit = int.MaxValue)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be greater than or equal to zero");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero");
        }

        try
        {
            using var context = ContextFactory.CreateDbContext();

            var events = context.Events
                .OrderByDescending(e => e.Timestamp)
                .Skip(offset)
                .Take(limit)
                .ToList()
                .AsReadOnly();

            return events;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get event records with options {Options}: {Message}", new { offset, limit }, ex.Message);
            throw;
        }
    }

    /// <summary>
    ///     Gets the total number of events.
    /// </summary>
    /// <returns>The total number of events.</returns>
    public virtual int Count()
    {
        try
        {
            using var context = ContextFactory.CreateDbContext();
            var count = context.Events.Count();
            return count;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to count event records: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    ///     Adds the specified event <paramref name="eventRecord"/>.
    /// </summary>
    /// <remarks>
    ///     To ensure proper construction of the record, use <see cref="EventRecord.From"/>.
    /// </remarks>
    /// <param name="eventRecord">The record to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when the specified record is null.</exception>
    public virtual void Add(EventRecord eventRecord)
    {
        if (eventRecord is null)
        {
            throw new ArgumentNullException(nameof(eventRecord));
        }

        try
        {
            using var context = ContextFactory.CreateDbContext();
            context.Add(eventRecord);
            context.SaveChanges();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add event record: {Message}", ex.Message);
            throw;
        }
    }
}