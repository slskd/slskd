// <copyright file="RelayController.cs" company="slskd Team">
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

namespace slskd.Relay
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
    ///     Relay.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    public class RelayController : ControllerBase
    {
        public RelayController(
            IRelayService relayService,
            IOptionsMonitor<Options> optionsMonitor,
            OptionsAtStartup optionsAtStartup)
        {
            Relay = relayService;
            OptionsMonitor = optionsMonitor;
            OptionsAtStartup = optionsAtStartup;
        }

        private ILogger Log { get; } = Serilog.Log.ForContext<RelayController>();
        private IRelayService Relay { get; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private RelayMode OperationMode => OptionsAtStartup.Relay.Mode.ToEnum<RelayMode>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }

        [HttpPut("")]
        [Authorize(Policy = AuthPolicy.JwtOnly)]
        public async Task<IActionResult> Connect()
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Agent, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            await Relay.Client.StartAsync();
            return Ok();
        }

        [HttpDelete("")]
        [Authorize(Policy = AuthPolicy.JwtOnly)]
        public async Task<IActionResult> Disconnect()
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Agent, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            await Relay.Client.StopAsync();
            return NoContent();
        }

        [HttpGet("downloads/{token}")]
        [Authorize(Policy = AuthPolicy.ApiKeyOnly)]
        public IActionResult DownloadFile([FromRoute]string token)
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Controller, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            if (!Guid.TryParse(token, out var guid))
            {
                return BadRequest("Token is not in a valid format");
            }

            var agentName = Request.Headers["X-Relay-Agent"].FirstOrDefault();
            var credential = Request.Headers["X-Relay-Credential"].FirstOrDefault();
            var filename = Request.Headers["X-Relay-Filename"].FirstOrDefault();

            if (!Relay.RegisteredAgents.Any(a => a.Name == agentName))
            {
                return Unauthorized();
            }

            Log.Information("Handling file download for token {Token} from a caller claiming to be agent {Agent}", token, agentName);

            // note: the token remains valid after the validation attempt, unlike most other endpoints.
            // this is done to support retries
            if (!Relay.TryValidateFileDownloadCredential(token: guid, agentName, filename, credential))
            {
                Log.Warning("Failed to authenticate file upload token {Token} from a caller claiming to be agent {Agent}", guid, agentName);
                return Unauthorized();
            }

            var sourceFile = Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, filename);

            Log.Information("Agent {Agent} authenticated. Sending file {File}", agentName, filename);

            var stream = new FileStream(Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, filename), FileMode.Open);
            return File(stream, "application/octet-stream");
        }

        /// <summary>
        ///     Uploads a file.
        /// </summary>
        /// <param name="token">The unique identifier for the request.</param>
        /// <returns></returns>
        [HttpPost("files/{token}")]
        [RequestSizeLimit(10L * 1024L * 1024L * 1024L)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L)]
        [DisableFormValueModelBinding]
        [Authorize(Policy = AuthPolicy.ApiKeyOnly)]
        public async Task<IActionResult> UploadFile(string token)
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Controller, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            if (!Guid.TryParse(token, out var guid))
            {
                return BadRequest("Token is not in a valid format");
            }

            if (!Request.HasFormContentType
                || !MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaTypeHeader)
                || string.IsNullOrEmpty(mediaTypeHeader.Boundary.Value))
            {
                return new UnsupportedMediaTypeResult();
            }

            var agentName = Request.Headers["X-Relay-Agent"].FirstOrDefault();
            var credential = Request.Headers["X-Relay-Credential"].FirstOrDefault();

            Stream stream = default;
            string filename = default;

            try
            {
                try
                {
                    // note: while the actual HTTP request is the same as the one we use for uploading shares, we handle it
                    // differently so that we can capture the stream 'in flight' and avoid any buffering. resist the urge to
                    // refactor one or the other to make them match!
                    var reader = new MultipartReader(HeaderUtilities.RemoveQuotes(mediaTypeHeader.Boundary).Value, Request.Body);
                    var fileSection = await reader.ReadNextSectionAsync();
                    var contentDisposition = ContentDispositionHeaderValue.Parse(fileSection.ContentDisposition);
                    filename = contentDisposition.FileName.Value;
                    stream = fileSection.Body;
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to handle file upload for token {Token} from a caller claiming to be agent {Agent}: {Message}", token, agentName, ex.Message);
                    Log.Debug(ex, "Failed to handle file upload");

                    return BadRequest();
                }

                if (!Relay.RegisteredAgents.Any(a => a.Name == agentName))
                {
                    return Unauthorized();
                }

                Log.Information("Handling file upload for token {Token} from a caller claiming to be agent {Agent}", token, agentName);

                // agents must encrypt the Id they were given in the request with the secret they share with the controller, and
                // provide the encrypted value as the credential with the request. the validation below verifies a bunch of
                // things, including that the encrypted value matches the expected value. the goal here is to ensure that the
                // caller is the same caller that received the request, and that the caller knows the shared secret.
                if (!Relay.TryValidateFileStreamResponseCredential(token: guid, agentName, filename, credential))
                {
                    Log.Warning("Failed to authenticate file upload token {Token} from a caller claiming to be agent {Agent}", guid, agentName);
                    return Unauthorized();
                }

                Log.Information("File upload of {Filename} ({Token}) from agent {Agent} validated and authenticated. Forwarding file stream.", filename, token, agentName);

                // pass the stream back to the relay service, which will in turn pass it to the upload service, and use it to
                // feed data into the remote upload. await this call, it will complete when the upload is complete, one way or the other.
                await Relay.HandleFileStreamResponse(agentName, id: guid, stream);

                Log.Information("File upload of {Filename} ({Token}) from agent {Agent} complete", filename, token, agentName);
                return Ok();
            }
            finally
            {
                stream?.TryDispose();
            }
        }

        /// <summary>
        ///     Uploads share information.
        /// </summary>
        /// <param name="token">The unique identifier for the request.</param>
        /// <returns></returns>
        [HttpPost("shares/{token}")]
        [Authorize(Policy = AuthPolicy.ApiKeyOnly)]
        public async Task<IActionResult> UploadShares(string token)
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Controller, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            if (!Guid.TryParse(token, out var guid))
            {
                return BadRequest("Token is not a valid Guid");
            }

            if (!Request.HasFormContentType
                || !MediaTypeHeaderValue.TryParse(Request.ContentType, out var mediaTypeHeader)
                || string.IsNullOrEmpty(mediaTypeHeader.Boundary.Value))
            {
                return new UnsupportedMediaTypeResult();
            }

            var agentName = Request.Headers["X-Relay-Agent"].FirstOrDefault();
            var credential = Request.Headers["X-Relay-Credential"].FirstOrDefault();

            IEnumerable<Share> shares;
            IFormFile database;

            try
            {
                shares = Request.Form["shares"].ToString().FromJson<IEnumerable<Share>>();
                database = Request.Form.Files[0];
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to handle share upload from agent {Agent}: {Message}", agentName, ex.Message);
                return BadRequest();
            }

            if (!Relay.RegisteredAgents.Any(a => a.Name == agentName))
            {
                return Unauthorized();
            }

            if (!Relay.TryValidateShareUploadCredential(token: guid, agentName, credential))
            {
                Log.Warning("Failed to authenticate share upload from caller claiming to be agent {Agent} using token {Token}", agentName, guid);
                return Unauthorized();
            }

            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Program.AppName));
            var temp = Path.Combine(Path.GetTempPath(), Program.AppName, $"share_{agentName}_{Path.GetRandomFileName()}.db");

            try
            {
                Log.Debug("Uploading share from {Agent} to {Filename}", agentName, temp);

                using var outputStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write);
                using var inputStream = database.OpenReadStream();

                await inputStream.CopyToAsync(outputStream);

                Log.Debug("Upload of share from {Agent} to {Filename} complete", agentName, temp);

                await Relay.HandleShareUpload(agentName, id: guid, shares, temp);

                return Ok();
            }
            catch (ShareValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(temp);
                    System.IO.File.Delete(temp + "-wal");
                    System.IO.File.Delete(temp + "-shm");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to remove temporary share upload file: {Message}", ex.Message);
                }
            }
        }
    }
}