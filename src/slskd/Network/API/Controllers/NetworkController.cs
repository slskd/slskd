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
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Shares;

    /// <summary>
    ///     Network.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    public class NetworkController : ControllerBase
    {
        public NetworkController(
            INetworkService networkService,
            IShareService shareService,
            IShareRepositoryFactory shareRepositoryFactory)
        {
            Network = networkService;
            Shares = shareService;

            ShareRepositoryFactory = shareRepositoryFactory;
        }

        private IShareRepositoryFactory ShareRepositoryFactory { get; }
        private IShareService Shares { get; }
        private INetworkService Network { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<NetworkController>();

        [HttpPost("files/{id}")]
        [Authorize]
        public async Task<IActionResult> UploadFile(string id)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest("Id is not a valid Guid");
            }

            // get the record for the waiting file
            if (Network.PendingFileUploads.TryGetValue(guid, out var record))
            {
                // get the stream from the multipart upload
                var stream = Request.Form.Files.First().OpenReadStream();

                // set the result for the stream
                // this should pass control to the transfer service along with
                // the stream
                record.Stream.SetResult(stream);

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

            if (Network.PendingFileUploads.TryGetValue(guid, out var record))
            {
                record.Stream.SetException(new NotFoundException($"The file could not be uploaded from the remote agent"));
                return NoContent();
            }

            return NotFound();
        }

        [HttpPost("shares/{id}")]
        [Authorize]
        public async Task<IActionResult> UploadShares(string id)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest("Id is not a valid Guid");
            }

            if (!Network.TryGetShareUploadToken(guid, out var agent))
            {
                return Unauthorized();
            }

            var temp = Path.Combine(Path.GetTempPath(), $"slskd_share_{agent}_{Path.GetRandomFileName()}.db");

            Log.Debug("Uploading share from {Agent} to {Filename}", agent, temp);

            using var outputStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write);
            using var inputStream = Request.Form.Files[0].OpenReadStream();

            await inputStream.CopyToAsync(outputStream);

            Log.Debug("Upload of share from {Agent} to {Filename} complete", agent, temp);

            var repository = ShareRepositoryFactory.CreateFromFile(temp);

            if (!repository.TryValidate(out var problems))
            {
                return BadRequest(string.Join(", ", problems));
            }

            var destinationRepository = ShareRepositoryFactory.CreateFromHost(agent);

            destinationRepository.RestoreFrom(repository);

            return Ok();
        }
    }
}
