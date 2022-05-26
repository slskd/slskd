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
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Shares.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class SharesController : ControllerBase
    {
        public SharesController(IShareService shareService)
        {
            Shares = shareService;
        }

        private IShareService Shares { get; }

        [HttpGet("")]
        public IActionResult List()
        {
            return Ok(Shares.Cache.Shares);
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            var share = Ok(Shares.Cache.Shares.FirstOrDefault(share => share.Id == id));

            if (share == default)
            {
                return NotFound();
            }

            return share;
        }

        [HttpGet("contents")]
        public IActionResult BrowseAll()
        {
            return Ok(Shares.Cache.Browse());
        }

        [HttpGet("{id}/contents")]
        public IActionResult BrowseShare(string id)
        {
            var share = Shares.Cache.Shares.FirstOrDefault(share => share.Id == id);

            if (share == default)
            {
                return NotFound();
            }

            var browse = Shares.Cache.Browse();

            foreach (var dir in browse)
            {
                Console.WriteLine($"{dir.Name} ?? ${share.RemotePath}");
            }

            var contents = Shares.Cache.Browse().Where(directory => directory.Name.StartsWith(share.RemotePath));

            return Ok(contents);
        }
    }
}
