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

namespace slskd.Core.API
{
    using System;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using IOFile = System.IO.File;

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
            OptionsSnapshot = optionsShapshot;
            ApplicationStateMonitor = applicationStateMonitor;
        }

        private IApplication Application { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private IStateMonitor<State> ApplicationStateMonitor { get; }
        private ILogger Logger { get; set; } = Log.ForContext(typeof(Program));

        [HttpGet]
        [Route("")]
        [Authorize]
        public IActionResult GetOptions()
        {
            return Ok(OptionsSnapshot.Value);
        }

        [HttpGet]
        [Route("yaml")]
        public IActionResult GetYamlFile()
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            var yaml = IOFile.ReadAllText(Program.ConfigurationFile);
            return Ok(yaml);
        }

        [HttpPost]
        [Route("yaml")]
        public IActionResult UpdateYamlFile([FromBody]string yaml)
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            if (!TryValidateYaml(yaml, out var error))
            {
                Logger.Error(error, "Failed to validate YAML configuration");
                return BadRequest(error);
            }

            IOFile.WriteAllText(Program.ConfigurationFile, yaml);
            return Ok();
        }

        [HttpPost]
        [Route("yaml/validate")]
        public IActionResult ValidateYamlFile([FromBody] string yaml)
        {
            if (!TryValidateYaml(yaml, out var error))
            {
                return BadRequest(error);
            }

            return Ok();
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

        private bool TryValidateYaml(string yaml, out string error)
        {
            error = null;

            try
            {
                _ = yaml.FromYaml<Options>();
            }
            catch (Exception ex)
            {
                error = $"{ex.Message}: {ex.InnerException.Message}";
                return false;
            }

            return true;
        }
    }
}
