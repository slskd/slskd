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
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
///     Telemetry.
/// </summary>
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("0")]
[ApiController]
[Produces("text/plain", "application/json")]
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
    ///     Gets application metrics.
    /// </summary>
    /// <returns></returns>
    [HttpGet("metrics")]
    [Authorize(Policy = AuthPolicy.Any)]
    public async Task<IActionResult> Get()
    {
        var response = await Telemetry.Prometheus.GetMetricsAsString();

        if (Request.Headers.Accept.ToString().Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            Dictionary<string, PrometheusMetric> dict = await Telemetry.Prometheus.GetMetricsAsObject();
            return Ok(dict);
        }

        return Content(response, "text/plain");
    }
}
