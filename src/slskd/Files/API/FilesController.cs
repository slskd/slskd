// <copyright file="FilesController.cs" company="slskd Team">
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

namespace slskd.Files.API
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using OneOf;

    /// <summary>
    ///     Files.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class FilesController : ControllerBase
    {
        public FilesController(
            IFileService fileService,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            Files = fileService;
            OptionsSnapshot = optionsSnapshot;
        }

        private IFileService Files { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }

        [HttpGet("downloads/directories")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(FilesystemDirectory), 200)]
        public async Task<IActionResult> GetDownloadContentsAsync([FromQuery] bool recursive = false)
        {
            try
            {
                var response = await Files.ListContentsAsync(
                    rootDirectory: OptionsSnapshot.Value.Directories.Downloads,
                    enumerationOptions: new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        RecurseSubdirectories = recursive,
                    });

                return Ok(response);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("downloads/directories/{base64SubdirectoryName}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(FilesystemDirectory), 200)]
        public async Task<IActionResult> GetDownloadSubdirectoryContentsAsync([FromRoute] string base64SubdirectoryName, [FromQuery] bool recursive = false)
        {
            var decodedDir = base64SubdirectoryName.FromBase64();

            try
            {
                var response = await Files.ListContentsAsync(
                    rootDirectory: OptionsSnapshot.Value.Directories.Downloads,
                    parentDirectory: decodedDir,
                    enumerationOptions: new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        RecurseSubdirectories = recursive,
                    });

                return Ok(response);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("downloads/directories/{base64SubdirectoryName}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, OneOf<bool, string>>), 204)]
        public async Task<IActionResult> DeleteDownloadSubdirectoryAsync([FromRoute] string base64SubdirectoryName)
        {
            if (!OptionsSnapshot.Value.RemoteFileManagement)
            {
                return Forbid();
            }

            var decodedDir = base64SubdirectoryName.FromBase64();

            try
            {
                var results = await Files.DeleteDirectoriesAsync(OptionsSnapshot.Value.Directories.Downloads, decodedDir);

                return results[decodedDir].Match(
                    success => NoContent(),
                    failure => throw failure);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("downloads/files/{base64Filename}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, OneOf<bool, string>>), 204)]
        public async Task<IActionResult> DeleteDownloadFileAsync([FromRoute] string base64filename)
        {
            if (!OptionsSnapshot.Value.RemoteFileManagement)
            {
                return Forbid();
            }

            var decodedFilename = base64filename.FromBase64();

            try
            {
                var results = await Files.DeleteFilesAsync(OptionsSnapshot.Value.Directories.Downloads, base64filename);

                return results[decodedFilename].Match(
                    success => NoContent(),
                    failure => throw failure);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("incomplete/directories")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(FilesystemDirectory), 200)]
        public async Task<IActionResult> GetIncompleteContentsAsync([FromQuery] bool recursive = false)
        {
            try
            {
                var response = await Files.ListContentsAsync(
                    rootDirectory: OptionsSnapshot.Value.Directories.Incomplete,
                    enumerationOptions: new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        RecurseSubdirectories = recursive,
                    });

                return Ok(response);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("incomplete/directories/{base64SubdirectoryName}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(FilesystemDirectory), 200)]
        public async Task<IActionResult> GetIncompleteSubdirectoryContentsAsync([FromRoute, Required] string base64SubdirectoryName, [FromQuery] bool recursive = false)
        {
            var decodedDir = base64SubdirectoryName.FromBase64();

            try
            {
                var response = await Files.ListContentsAsync(
                    rootDirectory: OptionsSnapshot.Value.Directories.Incomplete,
                    parentDirectory: decodedDir,
                    enumerationOptions: new EnumerationOptions
                    {
                        AttributesToSkip = FileAttributes.System,
                        RecurseSubdirectories = recursive,
                    });

                return Ok(response);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("incomplete/directories/{base64SubdirectoryName}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, OneOf<bool, string>>), 204)]
        public async Task<IActionResult> DeleteIncompleteSubdirectoryAsync([FromRoute] string base64SubdirectoryName)
        {
            if (!OptionsSnapshot.Value.RemoteFileManagement)
            {
                return Forbid();
            }

            var decodedDir = base64SubdirectoryName.FromBase64();

            try
            {
                var results = await Files.DeleteDirectoriesAsync(OptionsSnapshot.Value.Directories.Incomplete, decodedDir);

                return results[decodedDir].Match(
                    success => NoContent(),
                    failure => throw failure);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("incomplete/files/{base64Filename}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, OneOf<bool, string>>), 204)]
        public async Task<IActionResult> DeleteIncompleteFileAsync([FromRoute] string base64filename)
        {
            if (!OptionsSnapshot.Value.RemoteFileManagement)
            {
                return Forbid();
            }

            var decodedFilename = base64filename.FromBase64();

            try
            {
                var results = await Files.DeleteFilesAsync(OptionsSnapshot.Value.Directories.Incomplete, base64filename);

                return results[decodedFilename].Match(
                    success => NoContent(),
                    failure => throw failure);
            }
            catch (ForbiddenException)
            {
                return Forbid();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }
    }
}
