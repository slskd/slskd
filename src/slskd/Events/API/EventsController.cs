// <copyright file="EventsController.cs" company="slskd Team">
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

namespace slskd.Events.API;

using System;
using System.Collections.Generic;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

/// <summary>
///     Events.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
public class EventsController : ControllerBase
{
    public EventsController(
        EventService eventService)
    {
        Events = eventService;
    }

    private EventService Events { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<EventsController>();

    /// <summary>
    ///     Retrieves a paginated list of past <see cref="Event"/> records.
    /// </summary>
    /// <param name="offset">The offset (number of records) at which to start the requested page.</param>
    /// <param name="limit">The page size.</param>
    /// <returns>The list of <see cref="Event"/> records.</returns>
    [HttpGet("", Name = nameof(GetEvents))]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(IEnumerable<EventRecord>), 200)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetEvents([FromQuery] int offset = 0, [FromQuery] int limit = 100)
    {
        if (offset < 0)
        {
            return BadRequest("Offset must be greater than or equal to zero");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than zero");
        }

        try
        {
            var eventRecords = Events.Get(offset, limit);
            var count = Events.Count();

            Response.Headers.Append("X-Total-Count", count.ToString());

            return Ok(eventRecords);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to list events: {Message}", ex.Message);
            throw;
        }
    }
}