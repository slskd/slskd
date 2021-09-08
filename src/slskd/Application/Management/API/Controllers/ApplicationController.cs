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

namespace slskd.Management.API
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
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
        public ApplicationController(IManagementService managementService, IHostApplicationLifetime lifetime, IApplication application)
        {
            Management = managementService;
            Lifetime = lifetime;
            Application = application;
        }

        private IApplication Application { get; }
        private IManagementService Management { get; }
        private IHostApplicationLifetime Lifetime { get; }

        /// <summary>
        ///     Gets the current state of the application.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize]
        public IActionResult State()
        {
            return Ok(Management.ApplicationState);
        }

        /// <summary>
        ///     Stops the application.
        /// </summary>
        /// <returns></returns>
        [HttpDelete]
        [Authorize]
        public IActionResult Shutdown()
        {
            Lifetime.StopApplication();
            return NoContent();
        }

        /// <summary>
        ///     Restarts the application.
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Authorize]
        public IActionResult Restart()
        {
            Process.Start(Process.GetCurrentProcess().MainModule.FileName, Environment.CommandLine);
            Lifetime.StopApplication();

            return NoContent();
        }

        /// <summary>
        ///     Gets the current application version.
        /// </summary>
        /// <returns></returns>
        [HttpGet("version")]
        [Authorize]
        public IActionResult GetVersion()
        {
            return Ok(Program.InformationalVersion);
        }

        /// <summary>
        ///     Checks for updates.
        /// </summary>
        /// <returns></returns>
        [HttpGet("version/latest")]
        [Authorize]
        public async Task<IActionResult> CheckVersion([FromQuery]bool forceCheck = false)
        {
            if (forceCheck)
            {
                await Application.CheckVersionAsync();
            }

            var state = Management.ApplicationState;
            return Ok(new CheckVersionResponse() { UpdateAvailable = state.UpdateAvailable, LatestVersion = state.LatestVersion });
        }
    }
}
