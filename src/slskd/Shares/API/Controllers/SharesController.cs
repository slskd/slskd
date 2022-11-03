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
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Soulseek;

    /// <summary>
    ///     Shares.
    /// </summary>
    [ApiController]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class SharesController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SharesController"/> class.
        /// </summary>
        /// <param name="shareService"></param>
        public SharesController(
            IShareService shareService)
        {
            Shares = shareService;
        }

        private IShareService Shares { get; }

        /// <summary>
        ///     Gets the current list of shares.
        /// </summary>
        /// <response code="200">The request completed successfully.</response>
        /// <returns></returns>
        [HttpGet("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Dictionary<string, IEnumerable<Share>>), 200)]
        public IActionResult List()
        {
            var response = Shares.Hosts.ToDictionary(
                keySelector: host => host.Name,
                elementSelector: host => host.Shares);

            return Ok(response);
        }

        /// <summary>
        ///     Gets the share associated with the specified <see paramref="id"/>.
        /// </summary>
        /// <param name="id">The id of the share.</param>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The requested share could not be found.</response>
        /// <returns></returns>
        [HttpGet("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Share), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(string id)
        {
            var share = Shares.Hosts.SelectMany(host => host.Shares).FirstOrDefault(share => share.Id == id);

            if (share == default)
            {
                return NotFound();
            }

            return Ok(share);
        }

        /// <summary>
        ///     Returns a list of all shared directories and files.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("contents")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<Directory>), 200)]
        public async Task<IActionResult> BrowseAll()
        {
            return Ok(await Shares.BrowseAsync());
        }

        /// <summary>
        ///     Gets the contents of the share associated with the specified <see paramref="id"/>.
        /// </summary>
        /// <param name="id">The id of the share.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The requested share could not be found.</response>
        [HttpGet("{id}/contents")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<Directory>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> BrowseShare(string id)
        {
            var share = Shares.Hosts.SelectMany(host => host.Shares).FirstOrDefault(share => share.Id == id);

            if (share == default)
            {
                return NotFound();
            }

            var contents = await Shares.BrowseAsync(share);

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
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(409)]
        public IActionResult RescanSharesAsync()
        {
            try
            {
                _ = Shares.ScanAsync();
            }
            catch (ShareScanInProgressException)
            {
                return Conflict("A share scan is already in progress.");
            }

            return Ok();
        }

        /// <summary>
        ///     Cancels a share scan, if one is running.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The request completed successfully.</response>
        /// <response code="409">A share scan was not in progress.</response>
        [HttpDelete]
        [Route("")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelShareScan()
        {
            if (Shares.TryCancelScan())
            {
                return StatusCode(204);
            }

            return NotFound();
        }
    }
}
