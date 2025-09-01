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
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soulseek;

/// <summary>
///     Telemetry.
/// </summary>
[Route("api/v{version:apiVersion}/telemetry/[controller]")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
public class StatisticsController : ControllerBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StatisticsController"/> class.
    /// </summary>
    /// <param name="telemetryService"></param>
    public StatisticsController(TelemetryService telemetryService)
    {
        Telemetry = telemetryService;
    }

    private TelemetryService Telemetry { get; }

    /// <summary>
    ///     Summarizes transfer statistics for all users for the specified time range.
    /// </summary>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <returns>A dictionary keyed by direction and state and containing summary information.</returns>
    [HttpGet("statistics/transfers")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>), 200)]
    public async Task<IActionResult> GetTransferSummary(
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        return Ok(Telemetry.Statistics.GetTransferSummary(start.Value, end));
    }

    /// <summary>
    ///     Summarizes transfer statistics for the specified direction (Upload or Download) for all users and for the
    ///     specified time range.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download).</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <returns>A dictionary keyed by state and containing summary information.</returns>
    [HttpGet("statistics/transfers/{direction}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferStates, TransferSummary>), 200)]
    public async Task<IActionResult> GetTransferSummaryByDirection(
        [FromRoute, Required] string direction,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        // todo: pluralize this for consistency
        if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var directionEnum))
        {
            return BadRequest($"Direction must be one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
        }

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        var data = Telemetry.Statistics.GetTransferSummary(start.Value, end, directionEnum);

        return Ok(data[directionEnum]);
    }

    [HttpGet("statistics/transfers/{direction}/users")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<string, TransferSummary>), 200)]
    public async Task<IActionResult> GetSuccessfulTransferSummaryByDirectionAndUsername(
        [FromRoute] string direction,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var directionEnum))
        {
            return BadRequest($"Direction must be one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
        }

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        return Ok(Telemetry.Statistics.GetSuccessfulTransferSummaryByDirectionAndUsername(directionEnum, start.Value, end));
    }

    [HttpGet("statistics/transfers/users/{username}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>), 200)]
    public async Task<IActionResult> GetTransferSummaryByUsername(
        [FromRoute] string username,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        return Ok(Telemetry.Statistics.GetTransferSummary(start, end, username: username));
    }
}
