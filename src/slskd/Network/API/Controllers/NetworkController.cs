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
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Net.Http.Headers;
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
        [RequestSizeLimit(10L * 1024L * 1024L * 1024L)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L)]
        [DisableFormValueModelBinding]
        [Authorize]
        public async Task<IActionResult> UploadFile(string agentName, string token)
        {
            if (!Guid.TryParse(token, out var guid))
            {
                return BadRequest("Token is not a valid Guid");
            }

            if (!Network.RegisteredAgents.Any(a => a.Name == agentName))
            {
                return Unauthorized();
            }

            if (!Request.HasFormContentType
                || !MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaTypeHeader)
                || string.IsNullOrEmpty(mediaTypeHeader.Boundary.Value))
            {
                return new UnsupportedMediaTypeResult();
            }

            Log.Information("Handling file upload for token {Token} from a caller claiming to be agent {Agent}", token, agentName);

            string credential = default;
            Stream stream = default;
            string filename = default;

            try
            {
                try
                {
                    var reader = new MultipartReader(HeaderUtilities.RemoveQuotes(mediaTypeHeader.Boundary).Value, Request.Body);

                    // the multipart response contains two sections; a credential, and the file
                    var credentialSection = await reader.ReadNextSectionAsync();
                    using var sr = new StreamReader(credentialSection.Body);
                    credential = sr.ReadToEnd();

                    var fileSection = await reader.ReadNextSectionAsync();
                    var contentDisposition = ContentDispositionHeaderValue.Parse(fileSection.ContentDisposition);
                    filename = contentDisposition.FileName.Value;
                    stream = fileSection.Body;
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to handle file upload for token {Token} from a caller claiming to be agent {Agent}: {Message}", token, agentName, ex.Message);
                    return BadRequest();
                }

                // agents must encrypt the Id they were given in the request with the secret they share with the controller, and provide
                // the encrypted value as the credential with the request. the validation below verifies a bunch of things, including that
                // the encrypted value matches the expected value. the goal here is to ensure that the caller is the same caller that
                // received the request, and that the caller knows the shared secret.
                if (!Network.TryValidateFileStreamResponseCredential(token: guid, agentName, filename, credential))
                {
                    Log.Warning("Failed to authenticate file upload token {Token} from a caller claiming to be agent {Agent}", agentName);
                    return Unauthorized();
                }

                Log.Information("File upload of {Filename} ({Token}) from agent {Agent} validated and authenticated. Forwarding file stream.", filename, token, agentName);

                // pass the stream back to the network service, which will in turn pass it to the
                // upload service, and use it to feed data into the remote upload. await this call,
                // it will complete when the upload is complete, one way or the other.
                await Network.HandleFileStreamResponse(agentName, id: guid, stream);

                Log.Information("File upload of {Filename} ({Token}) from agent {Agent} complete", filename, token, agentName);
                return Ok();
            }
            finally
            {
                stream?.Dispose();
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

            try
            {
                await Network.HandleShareUpload(agentName, id: guid, shares, temp);
            }
            catch (ShareValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok();
        }
    }
}
