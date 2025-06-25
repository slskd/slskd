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
using System.Linq;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using slskd.Transfers;

/// <summary>
///     Events.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
public class EventsController : ControllerBase
{
    public EventsController(
        EventService eventService,
        EventBus eventBus)
    {
        Events = eventService;
        EventBus = eventBus;
    }

    private EventService Events { get; }
    private EventBus EventBus { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<EventsController>();

    /// <summary>
    ///     Retrieves a paginated list of past event records.
    /// </summary>
    /// <param name="offset">The offset (number of records) at which to start the requested page.</param>
    /// <param name="limit">The page size.</param>
    /// <returns>The list of <see cref="Event"/> records.</returns>
    /// <response code="400">The offset is less than zero, or if the limit is less than or equal to zero.</response>
    /// <response code="401">Authentication credentials are omitted.</response>
    /// <response code="403">Authentication is valid but not sufficient to access this endpoint.</response>
    /// <response code="500">An unexpected error is encountered.</response>
    /// <response code="200">The request completed successfully.</response>
    [HttpGet("", Name = nameof(GetEvents))]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(string), 500)]
    [ProducesResponseType(typeof(IEnumerable<EventRecord>), 200)]
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

    /// <summary>
    ///     Raises a sample event of the specified type.
    /// </summary>
    /// <param name="type">The type of event to raise.</param>
    /// <param name="disambiguator">An optional string used to disambiguate generated events.</param>
    /// <returns>The randomly generated event that was raised.</returns>
    /// <response code="400">The specified type is not a valid event type.</response>
    /// <response code="401">Authentication credentials are omitted.</response>
    /// <response code="403">Authentication is valid but not sufficient to access this endpoint.</response>
    /// <response code="500">An unexpected error is encountered.</response>
    /// <response code="201">The request completed successfully.</response>
    [HttpPost("{type}", Name = nameof(RaiseEvent))]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(string), 500)]
    [ProducesResponseType(typeof(Event), 201)]
    public IActionResult RaiseEvent([FromRoute] string type, [FromBody] string disambiguator)
    {
        if (!Enum.TryParse<EventType>(type, ignoreCase: true, out var eventType))
        {
            var names = Enum.GetNames(typeof(EventType))
                .Where(n => n != EventType.Any.ToString() && n != EventType.None.ToString());

            return BadRequest($"Unknown event type '{type}'; must be one of {string.Join(", ", names)}");
        }

        if (eventType is EventType.None || eventType is EventType.Any)
        {
            return BadRequest($"Event type '{type}' can not be raised");
        }

        try
        {
            var d = disambiguator;

            Event @event = eventType switch
            {
                EventType.DownloadFileComplete => new DownloadFileCompleteEvent { LocalFilename = $"{d}local.file", RemoteFilename = $"{d}remote.file", Transfer = new Transfer() },
                EventType.DownloadDirectoryComplete => new DownloadDirectoryCompleteEvent { LocalDirectoryName = $"{d}local.directory", RemoteDirectoryName = $"{d}remote.directory", Username = $"{d}username" },
                EventType.UploadFileComplete => new UploadFileCompleteEvent { LocalFilename = $"{d}local.file", RemoteFilename = $"{d}remote.file", Transfer = new Transfer() },
                EventType.PrivateMessageReceived => new PrivateMessageReceivedEvent { Username = $"{d}username", Message = $"{d}message", Blacklisted = false },
                EventType.PublicChatMessageReceived => new PublicChatMessageReceivedEvent { RoomName = $"{d}room", Username = $"{d}username", Message = $"{d}message", Blacklisted = false },
                EventType.RoomMessageReceived => new RoomMessageReceivedEvent { RoomName = $"{d}room", Username = $"{d}username", Message = $"{d}message", Blacklisted = false },
                EventType.Noop => new NoopEvent(),
                _ => throw new SlskdException($"Event type {eventType} is an enum member but is not handled.  Please submit an issue on GitHub."),
            };

            EventBus.Raise(@event);
            return StatusCode(201, @event);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to raise event {Type}: {Message}", eventType, ex.Message);
            throw;
        }
    }
}