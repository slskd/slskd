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

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace slskd.Events;

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