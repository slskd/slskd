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
        public ApplicationController(IManagementService managementService, IHostApplicationLifetime lifetime)
        {
            Management = managementService;
            Lifetime = lifetime;
        }

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
            return Ok(Management.GetServiceState());
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
    }
}
