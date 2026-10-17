// <copyright file="LogsController.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Core.API
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Files;

    /// <summary>
    ///     Logs.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class LogsController : ControllerBase
    {
        public LogsController(FileService fileService)
        {
            Files = fileService;
        }

        private FileService Files { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<LogsController>();

        /// <summary>
        ///     Gets the last few application logs.
        /// </summary>
        /// <returns></returns>
        [HttpGet("live")]
        [Authorize(Policy = AuthPolicy.Any, Roles = AuthRole.AdministratorOnly)]
        public IActionResult Logs()
        {
            return Ok(Program.LogBuffer);
        }

        /// <summary>
        ///     Lists the log files currently on disk.
        /// </summary>
        /// <returns></returns>
        [HttpGet("files")]
        [Authorize(Policy = AuthPolicy.Any, Roles = AuthRole.AdministratorOnly)]
        public async Task<IActionResult> List()
        {
            if (!Directory.Exists(Path.GetFullPath(Program.LogDirectory)))
            {
                return NotFound();
            }

            var directory = await Files.ListDirectoryContentsAsync(Path.GetFullPath(Program.LogDirectory), enumerationOptions: new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint,
                RecurseSubdirectories = false,
            });

            var logs = directory.Files.Where(f => f.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase));

            return Ok(logs);
        }

        /// <summary>
        ///     Retrieves the requested log file from disk as plain text.
        /// </summary>
        /// <param name="filename">The name of the log file.</param>
        /// <returns></returns>
        [HttpGet("files/{filename}")]
        [Produces("text/plain")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK, contentType: "text/plain")]
        [Authorize(Policy = AuthPolicy.Any, Roles = AuthRole.AdministratorOnly)]
        public IActionResult Get([FromRoute] string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return BadRequest("Filename is required");
            }

            if (filename.ContainsAny('/', '\\'))
            {
                return BadRequest("Filename must not contain a path");
            }

            var sanitizedFilename = FileSafety.GetFileNameSafely(filename, sanitize: true);

            if (!sanitizedFilename.Equals(filename, StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Input filename {Filename} sanitized to {Sanitized}", filename, sanitizedFilename);
                return BadRequest("Filename contains one or more invalid characters");
            }

            if (!sanitizedFilename.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Only .log files may be retrieved");
            }

            try
            {
                var stream = Files.GetFileContents(FileSafety.CombineSafely(Program.LogDirectory, sanitizedFilename));
                return File(stream, "text/plain; charset=utf-8", enableRangeProcessing: true);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve log file {Filename}: {Message}", filename, ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
