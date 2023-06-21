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
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("downloads/{base64SubdirectoryName}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<FilesystemDirectory>), 200)]
        public async Task<IActionResult> GetDownloadContentsAsync([FromRoute]string base64SubdirectoryName = null, [FromQuery]bool recursive = false)
        {
            var dir = OptionsSnapshot.Value.Directories.Downloads;

            if (!string.IsNullOrEmpty(base64SubdirectoryName))
            {
                var decodedDir = base64SubdirectoryName.FromBase64();
                dir = Path.Combine(dir, decodedDir);
            }

            try
            {
                var response = await Files.ListContentsAsync(dir, new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.System,
                    RecurseSubdirectories = recursive,
                });

                return Ok(response);
            }
            catch (InvalidDirectoryException)
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
