// <copyright file="SharesController.cs" company="slskd Team">
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

namespace slskd.Shares.API
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Shares.
    /// </summary>
    [ApiController]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class SharesController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharesController"/> class.
        /// </summary>
        /// <param name="shareService"></param>
        /// <param name="applicationStateMonitor"></param>
        public SharesController(
            IShareService shareService,
            IStateMonitor<State> applicationStateMonitor)
        {
            Shares = shareService;
            ApplicationStateMonitor = applicationStateMonitor;
        }

        private IShareService Shares { get; }
        private IStateMonitor<State> ApplicationStateMonitor { get; }

        /// <summary>
        ///     Gets the current list of shares.
        /// </summary>
        /// <response code="200">The request completed successfully.</response>
        /// <returns></returns>
        [HttpGet("")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<SummarizedShare>), 200)]
        public IActionResult List()
        {
            var browse = Shares.Cache.Browse();

            var summary = Shares.Cache.Shares.Select(share =>
            {
                var directories = browse.Where(directory => directory.Name.StartsWith(share.RemotePath));
                var fileCount = directories.Aggregate(seed: 0, (sum, directory) =>
                {
                    sum += directory.FileCount;
                    return sum;
                });

                return new SummarizedShare()
                {
                    Id = share.Id,
                    Alias = share.Alias,
                    IsExcluded = share.IsExcluded,
                    LocalPath = share.LocalPath,
                    Mask = share.Mask,
                    Raw = share.Raw,
                    RemotePath = share.RemotePath,
                    Directories = directories.Count(),
                    Files = fileCount,
                };
            });

            return Ok(summary);
        }

        /// <summary>
        ///     Gets the share associated with the specified <see paramref="id"/>.
        /// </summary>
        /// <param name="id">The id of the share.</param>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The requested share could not be found.</response>
        /// <returns></returns>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(SummarizedShare), 200)]
        [ProducesResponseType(404)]
        public IActionResult Get(string id)
        {
            var share = Shares.Cache.Shares.FirstOrDefault(share => share.Id == id);

            if (share == default)
            {
                return NotFound();
            }

            var browse = Shares.Cache.Browse();
            var directories = browse.Where(directory => directory.Name.StartsWith(share.RemotePath));
            var fileCount = directories.Aggregate(seed: 0, (sum, directory) =>
            {
                sum += directory.FileCount;
                return sum;
            });

            var summary = new SummarizedShare()
            {
                Id = share.Id,
                Alias = share.Alias,
                IsExcluded = share.IsExcluded,
                LocalPath = share.LocalPath,
                Mask = share.Mask,
                Raw = share.Raw,
                RemotePath = share.RemotePath,
                Directories = directories.Count(),
                Files = fileCount,
            };

            return Ok(summary);
        }

        /// <summary>
        ///     Returns a list of all shared directories and files.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("contents")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<Soulseek.Directory>), 200)]
        public IActionResult BrowseAll()
        {
            return Ok(Shares.Cache.Browse());
        }

        /// <summary>
        ///     Gets the contents of the share associated with the specified <see paramref="id"/>.
        /// </summary>
        /// <param name="id">The id of the share.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The requested share could not be found.</response>
        [HttpGet("{id}/contents")]
        [ProducesResponseType(typeof(IEnumerable<Soulseek.Directory>), 200)]
        [ProducesResponseType(404)]
        public IActionResult BrowseShare(string id)
        {
            var share = Shares.Cache.Shares.FirstOrDefault(share => share.Id == id);

            if (share == default)
            {
                return NotFound();
            }

            var contents = Shares.Cache.Browse()
                .Where(directory => directory.Name.StartsWith(share.RemotePath));

            return Ok(contents);
        }

        /// <summary>
        ///     Initiates a scan of the configured shares.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The request completed successfully.</response>
        /// <response code="409">A share scan is already in progress.</response>
        [HttpPut]
        [Route("")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(409)]
        public IActionResult RescanSharesAsync()
        {
            if (ApplicationStateMonitor.CurrentValue.SharedFileCache.Filling)
            {
                return Conflict("A share scan is already in progress.");
            }

            _ = Shares.Cache.FillAsync();

            return Ok();
        }
    }
}
