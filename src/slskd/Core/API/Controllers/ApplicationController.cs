// <copyright file="ApplicationController.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd.Core.API
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    ///     Application.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ApplicationController : ControllerBase
    {
        public ApplicationController(
            IHostApplicationLifetime lifetime,
            IApplication application,
            IOptionsMonitor<Options> optionsMonitor,
            IStateMonitor<State> applicationStateMonitor)
        {
            Lifetime = lifetime;
            Application = application;
            OptionsMonitor = optionsMonitor;
            ApplicationStateMonitor = applicationStateMonitor;
        }

        private IApplication Application { get; }
        private IStateMonitor<State> ApplicationStateMonitor { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHostApplicationLifetime Lifetime { get; }

        /// <summary>
        ///     Gets the current state of the application.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult State()
        {
            return Ok(ApplicationStateMonitor.CurrentValue);
        }

        /// <summary>
        ///     Stops the application.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        public IActionResult Shutdown()
        {
            Program.MasterCancellationTokenSource.Cancel();
            Lifetime.StopApplication();

            Task.Run(async () =>
            {
                await Task.Delay(500);
                Environment.Exit(0);
            });

            return NoContent();
        }

        /// <summary>
        ///     Restarts the application.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        public IActionResult Restart()
        {
            Program.MasterCancellationTokenSource.Cancel();
            Process.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.CommandLine);
            Lifetime.StopApplication();

            return NoContent();
        }

        /// <summary>
        ///     Gets the current application version.
        /// </summary>
        /// <returns></returns>
        [HttpGet("version")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetVersion()
        {
            return Ok(Program.SemanticVersion);
        }

        /// <summary>
        ///     Checks for updates.
        /// </summary>
        /// <returns></returns>
        [HttpGet("version/latest")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> CheckVersion([FromQuery] bool forceCheck = false)
        {
            if (forceCheck)
            {
                await Application.CheckVersionAsync();
            }

            return Ok(ApplicationStateMonitor.CurrentValue.Version);
        }

        /// <summary>
        ///     Forces garbage collection.
        /// </summary>
        /// <returns></returns>
        [HttpPost("gc")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult CollectGarbage()
        {
            Application.CollectGarbage();

            return Ok();
        }

        [HttpGet("dump")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> DumpMemory()
        {
            using var dumper = new Dumper();
            var file = await dumper.DumpAsync();

            return PhysicalFile(file, "application/octet-stream", "slskd.dmp");
        }
    }
}
