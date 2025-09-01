// <copyright file="TelemetryController.cs" company="slskd Team">
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soulseek;

/// <summary>
///     Telemetry.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
public class TelemetryController : ControllerBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TelemetryController"/> class.
    /// </summary>
    /// <param name="telemetryService"></param>
    public TelemetryController(TelemetryService telemetryService)
    {
        Telemetry = telemetryService;
    }

    private TelemetryService Telemetry { get; }

    /// <summary>
    ///     KPIs for the app, based on the Prometheus dashboard
    ///     * process_Working_set_bytes = total memory used
    ///     * dotnet_total_memory_bytes = sum of all managed memory
    ///     * process_cpu_seconds_total = total cpu time spent (not a great measure)
    ///     * system_runtime_cpu_usage = % of cpu consumed
    ///     * system_net_sockets_* = TCP activity
    ///     KPIs for the _system_, not the app
    ///     * node_memory_* = info about _system_ memory
    ///     * node_filesystem_avail_bytes = how much space is left on the system
    ///     * node_network = info about network adapters on the system
    ///     Other
    ///     * process_start_time_seconds = unix timestamp of start time
    /// </summary>
    private List<Regex> KpiRegexes { get; } = new List<Regex>
    {
        new Regex("slskd_.*", RegexOptions.Compiled),
        new Regex("node_(?!cpu)", RegexOptions.Compiled),
        new Regex("process_.*", RegexOptions.Compiled),
        new Regex("dotnet_total_memory_bytes", RegexOptions.Compiled),
        new Regex("system_runtime_[cpu_usage|working_set|alloc_total]", RegexOptions.Compiled),
        new Regex("system_net_sockets.*", RegexOptions.Compiled),
        new Regex("microsoft_aspnetcore_server_kestrel_[current|total]_connections", RegexOptions.Compiled),
    };

    /// <summary>
    ///     Gets application metrics.
    /// </summary>
    /// <remarks>
    ///     Returns a list of all application metrics.
    /// </remarks>
    /// <returns></returns>
    [HttpGet("metrics")]
    [Authorize(Policy = AuthPolicy.Any)]
    [Produces("text/plain", "application/json")]
    public async Task<IActionResult> Get()
    {
        if (Request.Headers.Accept.ToString().Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, PrometheusMetric> dict = await Telemetry.Prometheus.GetMetricsAsObject();
            return Ok(dict);
        }

        var response = await Telemetry.Prometheus.GetMetricsAsString();
        return Content(response, "text/plain");
    }

    /// <summary>
    ///     Gets gets key performance indicators for the application.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics/kpis")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> GetKpis()
    {
        Dictionary<string, PrometheusMetric> dict = await Telemetry.Prometheus.GetMetricsAsObject(include: KpiRegexes);
        return Ok(dict);
    }

    /// <summary>
    ///     Summarizes transfer statistics, grouped by direction and final transfer state, for all users, and for the
    ///     specified time range.
    /// </summary>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <returns>A dictionary keyed by direction and state and containing summary information.</returns>
    [HttpGet("statistics/transfers")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>), 200)]
    public IActionResult GetTransferSummary(
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
    ///     Summarizes transfer statistics for the specified direction, grouped by final transfer state,
    ///     for all users, and for the specified time range.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download).</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <returns>A dictionary keyed by state and containing summary information.</returns>
    [HttpGet("statistics/transfers/{direction}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferStates, TransferSummary>), 200)]
    public IActionResult GetTransferSummaryByDirection(
        [FromRoute, Required] string direction,
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

        var data = Telemetry.Statistics.GetTransferSummary(start.Value, end, directionEnum);

        return Ok(data[directionEnum]);
    }

    /// <summary>
    ///     Returns the top N user summaries for the specified direction, by total count and direction.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download).</param>
    /// <param name="start">The start time.</param>
    /// <param name="end">The end time.</param>
    /// <param name="limit">The number of records to return (Default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    [HttpGet("statistics/transfers/{direction}/users")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(List<UserTransferSummary>), 200)]
    public IActionResult GetSuccessfulTransferSummaryByDirectionAndUsername(
        [FromRoute] string direction,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int? limit = null,
        [FromQuery] int? offset = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;
        limit ??= 25;
        offset ??= 0;

        if (!Enum.TryParse<TransferDirection>(direction, ignoreCase: true, out var directionEnum))
        {
            return BadRequest($"Direction must be one of: {string.Join(", ", Enum.GetNames(typeof(TransferDirection)))}");
        }

        if (start >= end)
        {
            return BadRequest("End time must be later than start time");
        }

        return Ok(Telemetry.Statistics.GetSuccessfulTransferSummaryByDirectionAndUsername(directionEnum, start.Value, end, limit: limit.Value, offset: offset.Value));
    }

    [HttpGet("statistics/transfers/users/{username}")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ProducesResponseType(typeof(Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>), 200)]
    public IActionResult GetTransferSummaryByUsername(
        [FromRoute] string username,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        return Ok(Telemetry.Statistics.GetTransferSummary(start, end, username: username));
    }
}
