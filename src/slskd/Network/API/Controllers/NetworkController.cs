// <copyright file="NetworkController.cs" company="slskd Team">
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

namespace slskd.Network
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;

    /// <summary>
    ///     Network.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    public class NetworkController : ControllerBase
    {
        public NetworkController(INetworkService networkService)
        {
            Network = networkService;
        }

        private INetworkService Network { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkService>();

        [HttpPost("files/{id}")]
        [Authorize]
        public async Task<IActionResult> UploadFile(string id)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest("Id is not a valid Guid");
            }

            // get the record for the waiting file
            if (Network.PendingUploads.TryGetValue(guid, out var record))
            {
                // get the stream from the multipart upload
                var stream = Request.Form.Files.First().OpenReadStream();

                // set the result for the stream
                // this should pass control to the transfer service along with
                // the stream
                record.Upload.SetResult(stream);

                Console.WriteLine("Stream handed off. Waiting for completion...");

                // wait for the transfer service to set the completion result
                await record.Completion.Task;

                stream.Dispose();
                Console.WriteLine("Upload complete");
                return Ok();
            }
            else
            {
                Log.Warning("Agent");
                return NotFound();
            }
        }

        [HttpDelete("files/{id}")]
        [Authorize]
        public IActionResult NotifyUploadFailed(string id)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest("Id is not a valid Guid");
            }

            if (Network.PendingUploads.TryGetValue(guid, out var record))
            {
                record.Upload.SetException(new NotFoundException($"The file could not be uploaded from the remote agent"));
                return NoContent();
            }

            return NotFound();
        }
    }
}
