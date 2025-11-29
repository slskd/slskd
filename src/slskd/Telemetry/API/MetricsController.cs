// <copyright file="MetricsController.cs" company="slskd Team">
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
///     Telemetry.
/// </summary>
[Route("api/v{version:apiVersion}/telemetry/[controller]")]
[Tags("Telemetry")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="MetricsController"/> class.
    /// </summary>
    public MetricsController(TelemetryService telemetryService)
    {
        Telemetry = telemetryService;
    }

    private TelemetryService Telemetry { get; }

    /// <summary>
    ///     Gets KPIs for the app, based on the Prometheus dashboard
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
    ///     * process_start_time_seconds = unix timestamp of start time.
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
    ///     Gets all application metrics.
    /// </summary>
    /// <remarks>
    ///     Returns a list of all application metrics, in Prometheus format.  JSON formatted metrics can be retrieved
    ///     by setting the 'Accept' header to 'application/json'.
    /// </remarks>
    /// <returns></returns>
    [HttpGet("")]
    [Authorize(Policy = AuthPolicy.Any)]
    [Produces("text/plain", "application/json")]
    [ProducesResponseType(typeof(string), 200, "text/plain")]
    [ProducesResponseType(typeof(Dictionary<string, PrometheusMetric>), 200, "application/json")]
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
    [HttpGet("kpis")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> GetKpis()
    {
        Dictionary<string, PrometheusMetric> dict = await Telemetry.Prometheus.GetMetricsAsObject(include: KpiRegexes);
        return Ok(dict);
    }
}
