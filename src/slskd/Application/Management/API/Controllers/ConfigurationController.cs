// <copyright file="ConfigurationController.cs" company="slskd Team">
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

namespace slskd.Management.API
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Management;

    /// <summary>
    ///     Configuration.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ConfigurationController : ControllerBase
    {
        public ConfigurationController(
            IApplication application,
            IOptionsSnapshot<Options> optionsShapshot,
            IStateMonitor<State> applicationStateMonitor)
        {
            Application = application;
            OptionsShapshot = optionsShapshot;
            ApplicationStateMonitor = applicationStateMonitor;
        }

        private IApplication Application { get; }
        private IOptionsSnapshot<Options> OptionsShapshot { get; }
        private IStateMonitor<State> ApplicationStateMonitor { get; }

        [HttpGet]
        [Route("")]
        [Authorize]
        public IActionResult GetOptions()
        {
            // todo: sanitize this to remove passwords
            return Ok(OptionsShapshot.Value);
        }

        [HttpPut]
        [Route("shares")]
        [Authorize]
        public IActionResult RescanSharesAsync()
        {
            if (ApplicationStateMonitor.CurrentValue.SharedFileCache.Filling)
            {
                return Conflict("A share scan is already in progress.");
            }

            _ = Application.RescanSharesAsync();

            return Ok();
        }
    }
}
