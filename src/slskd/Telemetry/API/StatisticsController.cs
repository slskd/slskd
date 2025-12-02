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
using System.Linq;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Soulseek;

/// <summary>
///     Statistics.
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
    /// <param name="start">The start time of the window (default: 7 days ago).</param>
    /// <param name="end">The end time of the window (default: now).</param>
    /// <param name="direction">An optional direction (Upload, Download) by which to filter records.</param>
    /// <param name="username">An optional username by which to filter records.</param>
    /// <returns></returns>
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
        [FromQuery] string direction = null,
        [FromQuery] string username = null)
    {
        var now = DateTime.UtcNow;

        start ??= now.AddDays(-7);
        end ??= now;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        TransferDirection? transferDirection = null;

        if (!string.IsNullOrWhiteSpace(direction))
        {
            if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var parsedDirection))
            {
                return BadRequest($"Invalid direction; expected one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
            }

            transferDirection = parsedDirection;
        }

        try
        {
            return Ok(Telemetry.Statistics.GetTransferSummary(
                start: start.Value,
                end: end,
                direction: transferDirection,
                username: username));
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
    /// <param name="start">The start time of the window (default: 7 days ago).</param>
    /// <param name="end">The end time of the window (default: now).</param>
    /// <param name="interval">The interval, in minutes (default: 60).</param>
    /// <param name="direction">An optional direction (Upload, Download) by which to filter records.</param>
    /// <param name="username">An optional username by which to filter records.</param>
    /// <returns></returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/histogram")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferHistogram(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int interval = 60,
        [FromQuery] string direction = null,
        [FromQuery] string username = null)
    {
        var now = DateTime.UtcNow;

        start ??= now.AddDays(-7);
        end ??= now;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        // clamp the start time to the earliest reasonable date to avoid returning a ton of empty intervals
        // and introducing performance issues client side
        if (start < Program.GenesisDateTime)
        {
            Log.Warning("A start time prior to the genesis date of the application was supplied; the start time has been adjusted to the genesis time {Genesis}", Program.GenesisDateTime);
            start = Program.GenesisDateTime;
        }

        if (interval < 5)
        {
            return BadRequest("Interval must be greater than or equal to 5");
        }

        var intervalTimeSpan = TimeSpan.FromMinutes(interval);

        TransferDirection? transferDirection = null;

        if (!string.IsNullOrWhiteSpace(direction))
        {
            if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var parsedDirection))
            {
                return BadRequest($"Invalid direction; expected one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
            }

            transferDirection = parsedDirection;
        }

        try
        {
            return Ok(Telemetry.Statistics.GetTransferHistogram(
                start: start.Value,
                end: end.Value,
                interval: intervalTimeSpan,
                direction: transferDirection,
                username: username));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer histogram over {Start}-{End}/{Interval}: {Message}", start, end, interval, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Gets the top N user summaries by count, total bytes, or average speed.
    /// </summary>
    /// <param name="direction">The direction (Upload, Download).</param>
    /// <param name="start">The start time of the window (default: 12/30/2025).</param>
    /// <param name="end">The end time of the window (default: now).</param>
    /// <param name="sortBy">The property by which to sort (Count, TotalBytes, AverageSpeed. Default: Count).</param>
    /// <param name="sortOrder">The sort order (ASC, DESC. Default: DESC).</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/leaderboard")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<TransferSummary>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferLeaderboard(
        [FromQuery] string direction,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string sortBy = "Count",
        [FromQuery] string sortOrder = "DESC",
        [FromQuery] int limit = 25,
        [FromQuery] int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return BadRequest("Direction is required");
        }

        if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var parsedDirection))
        {
            return BadRequest($"Invalid direction; expected one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
        }

        start ??= Program.GenesisDateTime;
        end ??= DateTime.UtcNow;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        if (!Enum.TryParse<LeaderboardSortBy>(sortBy, out var parsedSortBy))
        {
            return BadRequest($"Invalid sortBy; expected one of: {string.Join(", ", Enum.GetNames(typeof(LeaderboardSortBy)))}");
        }

        if (!Enum.TryParse<SortOrder>(sortOrder, out var parsedSortOrder))
        {
            return BadRequest($"Invalid sortOrder; expected one of: {string.Join(", ", Enum.GetNames(typeof(SortOrder)))}");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than zero");
        }

        if (offset < 0)
        {
            return BadRequest("Offset must be greater than or equal to zero");
        }

        try
        {
            return Ok(Telemetry.Statistics.GetTransferLeaderboard(
                direction: parsedDirection,
                start: start.Value,
                end: end,
                sortBy: parsedSortBy,
                sortOrder: parsedSortOrder,
                limit: limit,
                offset: offset));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer leaderboard over {Start}-{End}: {Message}", start, end, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Gets detailed transfer activity for the specified user.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <param name="start">The start time of the summary window (default: 7 days ago).</param>
    /// <param name="end">The end time of the summary window (default: now).</param>
    /// <returns></returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/users/{username}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, UserDirectionTransferSummary>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetUserDetails(
        [FromRoute] string username,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required");
        }

        start ??= Program.GenesisDateTime;
        end ??= DateTime.UtcNow;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        static UserDirectionTransferStatistics GetStatistics(Dictionary<TransferStates, TransferSummary> summary)
        {
            var totalTransfers = summary.Values.Sum(x => x.Count);
            var successful = summary.Where(x => x.Key == TransferStates.Succeeded).Sum(x => x.Value.Count);
            var errored = summary.Where(x => new[] { TransferStates.Errored, TransferStates.TimedOut, TransferStates.Rejected, TransferStates.Aborted }.Contains(x.Key)).Sum(x => x.Value.Count);
            var cancelled = summary.Where(x => x.Key == TransferStates.Cancelled).Sum(x => x.Value.Count);

            return new UserDirectionTransferStatistics
            {
                Total = totalTransfers,
                Successful = successful,
                Errored = errored,
                Cancelled = cancelled,
            };
        }

        try
        {
            var results = new Dictionary<TransferDirection, UserDirectionTransferSummary>();

            var summary = Telemetry.Statistics.GetTransferSummary(start, end, username: username);

            results.Add(TransferDirection.Upload, new UserDirectionTransferSummary
            {
                Summary = summary[TransferDirection.Upload],
                Statistics = GetStatistics(summary[TransferDirection.Upload]),
                Exceptions = Telemetry.Statistics.GetTransferExceptionsPareto(TransferDirection.Upload, start, end, username: username, limit: 25, offset: 0),
            });

            results.Add(TransferDirection.Download, new UserDirectionTransferSummary
            {
                Summary = summary[TransferDirection.Download],
                Statistics = GetStatistics(summary[TransferDirection.Download]),
                Exceptions = Telemetry.Statistics.GetTransferExceptionsPareto(TransferDirection.Download, start, end, username: username, limit: 25, offset: 0),
            });

            return Ok(results);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching user details for {Username}: {Message}", username, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Gets a list of transfer exceptions by direction.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="username">An optional username by which to filter exceptions.</param>
    /// <param name="sortOrder">The sort order (ASC, DESC. Default: DESC).</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/exceptions")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<TransferExceptionDetail>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferExceptions(
        [FromQuery] string direction = null,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string username = null,
        [FromQuery] string sortOrder = "DESC",
        [FromQuery] int limit = 25,
        [FromQuery] int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return BadRequest("Direction is required");
        }

        if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var parsedDirection))
        {
            return BadRequest($"Invalid direction; expected one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
        }

        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        if (!Enum.TryParse<SortOrder>(sortOrder, out var parsedSortOrder))
        {
            return BadRequest($"Invalid sortOrder; expected one of: {string.Join(", ", Enum.GetNames(typeof(SortOrder)))}");
        }

        if (limit <= 0)
        {
            return BadRequest("Limit must be greater than zero");
        }

        if (offset < 0)
        {
            return BadRequest("Offset must be greater than or equal to zero");
        }

        try
        {
            return Ok(Telemetry.Statistics.GetTransferExceptions(
                direction: parsedDirection,
                start: start.Value,
                end: end.Value,
                username: username,
                sortOrder: parsedSortOrder,
                limit: limit,
                offset: offset));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer exceptions over {Start}-{End}: {Message}", start, end, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Gets the top N exceptions by total count and direction.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="username">An optional username by which to filter exceptions.</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/exceptions/pareto")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<TransferExceptionSummary>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferExceptionsPareto(
        [FromQuery] string direction,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string username = null,
        [FromQuery] int limit = 25,
        [FromQuery] int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return BadRequest("Direction is required");
        }

        if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var parsedDirection))
        {
            return BadRequest($"Invalid direction; expected one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
        }

        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

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

        try
        {
            return Ok(Telemetry.Statistics.GetTransferExceptionsPareto(
                direction: parsedDirection,
                start: start.Value,
                end: end.Value,
                limit: limit,
                offset: offset,
                username: username));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer exceptions over {Start}-{End}: {Message}", start, end, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }

    /// <summary>
    ///     Gets the top N most frequently downloaded directories by total count and distinct users.
    /// </summary>
    /// <param name="start">The start time of the window (default: 12/30/2025).</param>
    /// <param name="end">The end time of the window (default: now).</param>
    /// <param name="username">An optional username by which to filter records.</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    /// <response code="200">The request completed successfully.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="500">An error occurred.</response>
    [HttpGet("transfers/directories")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<TransferDirectorySummary>), 200)]
    [ProducesResponseType(typeof(string), 400)]
    [ProducesResponseType(typeof(string), 500)]
    public IActionResult GetTransferDirectoryFrequency(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] string username = null,
        [FromQuery] int limit = 25,
        [FromQuery] int offset = 0)
    {
        start ??= Program.GenesisDateTime;
        end ??= DateTime.MaxValue;

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

        try
        {
            return Ok(Telemetry.Statistics.GetTransferDirectoryFrequency(
                start: start,
                end: end,
                username: username,
                limit: limit,
                offset: offset));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error fetching transfer directories over {Start}-{End}: {Message}", start, end, ex.Message);
            return StatusCode(500, ex.Message);
        }
    }
}
