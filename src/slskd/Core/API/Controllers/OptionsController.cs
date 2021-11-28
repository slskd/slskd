// <copyright file="OptionsController.cs" company="slskd Team">
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
    using slskd.Validation;
    using IOFile = System.IO.File;

    /// <summary>
    ///     Options.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class OptionsController : ControllerBase
    {
        public OptionsController(
            OptionsAtStartup optionsAtStartup,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            OptionsAtStartup = optionsAtStartup;
            OptionsSnapshot = optionsSnapshot;
        }

        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private ILogger Logger { get; } = Log.ForContext(typeof(OptionsController));

        /// <summary>
        ///     Gets the current application options.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(Options), 200)]
        public IActionResult Current()
        {
            return Ok(OptionsSnapshot.Value);
        }

        /// <summary>
        ///     Gets the application options provided at startup.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("startup")]
        [Authorize]
        [ProducesResponseType(typeof(Options), 200)]
        public IActionResult Startup()
        {
            return Ok(OptionsAtStartup);
        }

        [HttpGet]
        [Authorize]
        [Route("yaml/location")]
        public IActionResult GetYamlFileLocation()
        {
            return Ok(Program.ConfigurationFile);
        }

        [HttpGet]
        [Authorize]
        [Route("yaml")]
        public IActionResult GetYamlFile()
        {
            if (!OptionsSnapshot.Value.NoRemoteConfiguration)
            {
                return Forbid();
            }

            var yaml = IOFile.ReadAllText(Program.ConfigurationFile);
            return Ok(yaml);
        }

        [HttpPost]
        [Authorize]
        [Route("yaml")]
        public IActionResult UpdateYamlFile([FromBody] string yaml)
        {
            if (!OptionsSnapshot.Value.NoRemoteConfiguration)
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
        [Authorize]
        [Route("yaml/validate")]
        public IActionResult ValidateYamlFile([FromBody] string yaml)
        {
            if (!TryValidateYaml(yaml, out var error))
            {
                return Ok(error);
            }

            return Ok();
        }

        private bool TryValidateYaml(string yaml, out string error)
        {
            error = null;

            try
            {
                var options = yaml.FromYaml<Options>();

                if (!options.TryValidate(out var result))
                {
                    error = result.GetResultView();
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;

                if (ex.InnerException != null)
                {
                    error += $": {ex.InnerException.Message}";
                }

                return false;
            }

            return true;
        }
    }
}
