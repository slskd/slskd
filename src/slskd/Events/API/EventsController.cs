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

    [HttpGet("")]
    [Authorize(Policy = AuthPolicy.Any)]
    public IActionResult GetEvents([FromQuery] int offset = 0, [FromQuery] int limit = 100)
    {
        if (offset < 0)
        {
            return BadRequest("Offset must be greater than or equal to zero.");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than zero");
        }

        var events = Events.Get(offset, limit);
        var count = Events.Count();

        Response.Headers.Append("X-Total-Count", count.ToString());

        return Ok(events);
    }
}