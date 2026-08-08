// <copyright file="ReportsController.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
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
using slskd.Transfers;
using Soulseek;

/// <summary>
///     Reports.
/// </summary>
[Route("api/v{version:apiVersion}/telemetry/[controller]")]
[Tags("Telemetry")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ReportsController"/> class.
    /// </summary>
    public ReportsController(
        TelemetryService telemetryService,
        TransferService transferService)
    {
        Telemetry = telemetryService;
        Transfers = transferService;
    }

    private TelemetryService Telemetry { get; }
    private TransferService Transfers { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<ReportsController>();

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
            return Ok(Telemetry.Reports.GetTransferSummary(
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
    /// <param name="buckets">The number of evenly sized buckets that the data will be divided into (default: 100).</param>
    /// <param name="interval">The interval, in minutes (default: null).</param>
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
        [FromQuery] int? buckets = null,
        [FromQuery] int? interval = null,
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

        if (end < Program.GenesisDateTime)
        {
            return BadRequest($"End time impossibly early (earlier than slskd genesis time {Program.GenesisDateTime})");
        }

        if (buckets.HasValue && interval.HasValue)
        {
            return BadRequest("Specify either interval or buckets, not both");
        }

        if (buckets.HasValue && (buckets < 1 || buckets > 1000))
        {
            return BadRequest("Buckets must be between 1 and 1000, if specified");
        }

        if (interval.HasValue && interval <= 1)
        {
            return BadRequest("Interval must be greater than 1");
        }

        if (!buckets.HasValue && !interval.HasValue)
        {
            buckets = 100; // the default, according to API docs. update the docs if this changes
        }

        // at this point it is either buckets or interval, and both would have valid values, so convert
        // interval to a timespan if it exists and send both values into the service, it will sort things out
        TimeSpan? intervalTimeSpan = !buckets.HasValue ? TimeSpan.FromMinutes(interval.Value) : null;

        // if a caller supplies a start timestamp that is equal to, or is earlier than the genesis time for slskd (12/30/2020 6:22:00 UTC)
        // then replace the provided timestamp with the oldest transfer record (regardless of direction); there's no data older than that
        // and this will let the caller avoid a bunch of empty series on their histogram
        if (start < Program.GenesisDateTime)
        {
            start = Transfers.Query(q => q.Select(t => (DateTime?)t.RequestedAt).Min()) ?? Program.GenesisDateTime;
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
            return Ok(Telemetry.Reports.GetTransferHistogram(
                start: start.Value,
                end: end.Value,
                buckets: buckets,
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
            return Ok(Telemetry.Reports.GetTransferLeaderboard(
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
        [FromRoute, UrlEncoded] string username,
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

            var summary = Telemetry.Reports.GetTransferSummary(start, end, username: username);

            results.Add(TransferDirection.Upload, new UserDirectionTransferSummary
            {
                Summary = summary[TransferDirection.Upload],
                Statistics = GetStatistics(summary[TransferDirection.Upload]),
                Exceptions = Telemetry.Reports.GetTransferExceptionsPareto(TransferDirection.Upload, start, end, username: username, limit: 25, offset: 0),
            });

            results.Add(TransferDirection.Download, new UserDirectionTransferSummary
            {
                Summary = summary[TransferDirection.Download],
                Statistics = GetStatistics(summary[TransferDirection.Download]),
                Exceptions = Telemetry.Reports.GetTransferExceptionsPareto(TransferDirection.Download, start, end, username: username, limit: 25, offset: 0),
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
            return Ok(Telemetry.Reports.GetTransferExceptions(
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
            return Ok(Telemetry.Reports.GetTransferExceptionsPareto(
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
            return Ok(Telemetry.Reports.GetTransferDirectoryFrequency(
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
