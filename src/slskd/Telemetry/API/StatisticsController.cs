// <copyright file="StatisticsController.cs" company="slskd Team">
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

namespace slskd.Telemetry;

using System;
using System.Collections.Generic;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Soulseek;

/// <summary>
///     Telemetry.
/// </summary>
[Route("api/v{version:apiVersion}/telemetry/[controller]")]
[Tags("Telemetry")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
public class StatisticsController : ControllerBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StatisticsController"/> class.
    /// </summary>
    public StatisticsController(TelemetryService telemetryService)
    {
        Telemetry = telemetryService;
    }

    private TelemetryService Telemetry { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<MetricsController>();

    /// <summary>
    ///     Gets a summary of all transfer activity over the specified timeframe, grouped by direction and final state.
    /// </summary>
    /// <param name="start">The start time (default: 7 days ago).</param>
    /// <param name="end">The end time (default: now).</param>
    /// <param name="username">An optional username by which to filter activity.</param>
    /// <returns>A dictionary keyed by direction and state and containing summary information.</returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/summary")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferSummary(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string username = null)
    {
        var now = DateTime.UtcNow;

        start ??= now.AddDays(-7);
        end ??= now;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        try
        {
            return Ok(Telemetry.Statistics.GetTransferSummary(start.Value, end, username: username));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer summary over {Start}-{End}: {Message}", start, end, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Gets a histogram of all transfer activity over the specified timeframe, aggregated into fixed size time intervals
    ///     and grouped by direction and final state.
    /// </summary>
    /// <param name="start">The start time (default: 7 days ago).</param>
    /// <param name="end">The end time (default: now).</param>
    /// <param name="interval">The interval, in minutes (default: 60).</param>
    /// <param name="username">An optional username by which to filter activity.</param>
    /// <returns>A dictionary keyed by direction and state and containing summary information.</returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/histogram")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferSummaryHistogram(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int interval = 60,
        [FromQuery] string username = null)
    {
        var now = DateTime.UtcNow;

        start ??= now.AddDays(-7);
        end ??= now;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        if (interval < 5)
        {
            return BadRequest("Interval must be greater than or equal to 5");
        }

        var intervalTimeSpan = TimeSpan.FromMinutes(interval);

        try
        {
            return Ok(Telemetry.Statistics.GetTransferSummaryHistogram(start.Value, end.Value, interval: intervalTimeSpan, username: username));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer histogram over {Start}-{End}/{Interval}: {Message}", start, end, interval, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Returns the top N user summaries by total count and direction.
    /// </summary>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    [HttpGet("transfers/users")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, List<UserTransferSummary>>), 200)]
    public IActionResult GetSuccessfulTransferSummaryByUsername(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;
        limit ??= 25;
        offset ??= 0;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than zero");
        }

        if (offset < 0)
        {
            return BadRequest("Offset must be greater than or equal to zero");
        }

        var downloads = Telemetry.Statistics.GetTransferRanking(direction: TransferDirection.Download, start.Value, end, limit: limit.Value, offset: offset.Value);
        var uploads = Telemetry.Statistics.GetTransferRanking(direction: TransferDirection.Upload, start.Value, end, limit: limit.Value, offset: offset.Value);

        var dict = new Dictionary<TransferDirection, List<TransferSummary>>()
        {
            { TransferDirection.Download, downloads },
            { TransferDirection.Upload, uploads },
        };

        return Ok(dict);
    }

    [HttpGet("transfers/directories")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<string, int>), 200)]
    public IActionResult GetTransferSummaryByDirectory(
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null)
    {
        // todo: get a list of all directories downloaded at least once, along with the number of times downloaded (doesn't matter what status)
        return null;
    }

    /// <summary>
    ///     Returns the top N errors by total count and direction.
    /// </summary>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    [HttpGet("transfers/errors")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>), 200)]
    public IActionResult GetTransferErrorSummary(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;
        limit ??= 25;
        offset ??= 0;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than zero");
        }

        if (offset < 0)
        {
            return BadRequest("Offset must be greater than or equal to zero");
        }

        var downloads = Telemetry.Statistics.GetTransferErrorSummary(start, end, TransferDirection.Download, limit: limit.Value, offset: offset.Value);
        var uploads = Telemetry.Statistics.GetTransferErrorSummary(start, end, TransferDirection.Upload, limit: limit.Value, offset: offset.Value);

        var dict = new Dictionary<TransferDirection, Dictionary<string, long>>()
        {
            { TransferDirection.Download, downloads },
            { TransferDirection.Upload, uploads },
        };

        return Ok(dict);
    }
}
