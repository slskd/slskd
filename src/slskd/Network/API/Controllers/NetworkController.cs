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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
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

        [HttpPost("files/{agentName}/{token}")]
        [Authorize]
        public async Task<IActionResult> UploadFile(string agentName, string token)
        {
            if (!Guid.TryParse(token, out var guid))
            {
                return BadRequest("Token is not a valid Guid");
            }

            string credential;
            IFormFile file;

            try
            {
                credential = Request.Form["credential"].ToString();
                file = Request.Form.Files[0];
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to handle file upload from agent {Agent}: {Message}", agentName, ex.Message);
                return BadRequest();
            }

            if (!Network.TryValidateFileUploadCredential(token: guid, agentName, filename: file.FileName, credential))
            {
                Log.Warning("Failed to authenticate file upload from caller claiming to be agent {Agent}", agentName);
                return Unauthorized();
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

        [HttpPost("shares/{agentName}/{token}")]
        [Authorize]
        public async Task<IActionResult> UploadShares(string agentName, string token)
        {
            if (!Guid.TryParse(token, out var guid))
            {
                return BadRequest("Token is not a valid Guid");
            }

            string credential;
            IEnumerable<Share> shares;
            IFormFile database;

            try
            {
                credential = Request.Form["credential"].ToString();
                shares = Request.Form["shares"].ToString().FromJson<IEnumerable<Share>>();
                database = Request.Form.Files[0];
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to handle share upload from agent {Agent}: {Message}", agentName, ex.Message);
                return BadRequest();
            }

            if (!Network.TryValidateShareUploadCredential(token: guid, agentName, credential))
            {
                Log.Warning("Failed to authenticate share upload from caller claiming to be agent {Agent}");
                return Unauthorized();
            }

            var temp = Path.Combine(Path.GetTempPath(), $"slskd_share_{agentName}_{Path.GetRandomFileName()}.db");

            Log.Debug("Uploading share from {Agent} to {Filename}", agentName, temp);

            using var outputStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write);
            using var inputStream = database.OpenReadStream();

            await inputStream.CopyToAsync(outputStream);

            Log.Debug("Upload of share from {Agent} to {Filename} complete", agentName, temp);

            var repository = ShareRepositoryFactory.CreateFromFile(temp);

            if (!repository.TryValidate(out var problems))
            {
                return BadRequest("Invalid database: " + string.Join(", ", problems));
            }

            var destinationRepository = ShareRepositoryFactory.CreateFromHost(agentName);

            destinationRepository.RestoreFrom(repository);

            Shares.AddOrUpdateHost(new Host(agentName, shares));

            return Ok();
        }
    }
}
