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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
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
        private const long ONE_GIBIBYTE = 1L * 1024L * 1024L * 1024L; // 1073741824
        private const long ONE_TEBIBYTE = 1024L * ONE_GIBIBYTE; // 1099511627776

        /// <summary>
        ///     Initializes a new instance of the <see cref="RelayController"/> class.
        /// </summary>
        /// <param name="relayService"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="optionsAtStartup"></param>
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

        /// <summary>
        ///     Connects to the configured controller.
        /// </summary>
        /// <returns></returns>
        [HttpPut("agent")]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        public async Task<IActionResult> Connect()
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Agent, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            await Relay.Client.StartAsync();
            return Ok();
        }

        /// <summary>
        ///     Disconnects from the connected controller.
        /// </summary>
        /// <returns></returns>
        [HttpDelete("agent")]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        public async Task<IActionResult> Disconnect()
        {
            if (!OptionsAtStartup.Relay.Enabled || !new[] { RelayMode.Agent, RelayMode.Debug }.Contains(OperationMode))
            {
                return Forbid();
            }

            await Relay.Client.StopAsync();
            return NoContent();
        }

        /// <summary>
        ///     Downloads a file from the connected controller.
        /// </summary>
        /// <param name="token">The unique identifier for the request.</param>
        /// <returns></returns>
        [HttpGet("controller/downloads/{token}")]
        [Authorize(Policy = AuthPolicy.ApiKeyOnly, Roles = AuthRole.Any)]
        public IActionResult DownloadFile([FromRoute] string token)
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
            var filename = Request.Headers["X-Relay-Filename-Base64"].FirstOrDefault()?.FromBase64();

            if (!Relay.RegisteredAgents.Any(a => a.Name == agentName) || string.IsNullOrEmpty(credential))
            {
                return Unauthorized();
            }

            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest();
            }

            Log.Information("Handling file download of {Filename} ({Token}) from a caller claiming to be agent {Agent}", filename, token, agentName);

            // note: the token remains valid after the validation attempt, unlike most other endpoints.
            // this is done to support retries
            if (!Relay.TryValidateFileDownloadCredential(token: guid, agentName, filename, credential))
            {
                Log.Warning("Failed to authenticate file upload token {Token} from a caller claiming to be agent {Agent}", guid, agentName);
                return Unauthorized();
            }

            var sourceFile = Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, filename);

            Log.Information("Agent {Agent} authenticated for token {Token}. Sending file {Filename}", agentName, guid, filename);

            var stream = new FileStream(sourceFile, FileMode.Open);
            return File(stream, "application/octet-stream");
        }

        /// <summary>
        ///     Uploads a file to the connected controller.
        /// </summary>
        /// <param name="token">The unique identifier for the request.</param>
        /// <returns></returns>
        [HttpPost("controller/files/{token}")]
        [RequestSizeLimit(10L * ONE_TEBIBYTE)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10L * ONE_TEBIBYTE)]
        [DisableFormValueModelBinding]
        [Authorize(Policy = AuthPolicy.ApiKeyOnly, Roles = AuthRole.ReadWriteOrAdministrator)]
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

            if (!Relay.RegisteredAgents.Any(a => a.Name == agentName) || string.IsNullOrEmpty(credential))
            {
                return Unauthorized();
            }

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

                    if (string.IsNullOrEmpty(filename))
                    {
                        throw new ArgumentException("Upload filename is null or empty");
                    }

                    if (stream == null || !stream.CanRead)
                    {
                        throw new ArgumentException("Unable to obtain stream from request");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to handle file upload for token {Token} from a caller claiming to be agent {Agent}: {Message}", token, agentName, ex.Message);
                    Log.Debug(ex, "Failed to handle file upload");

                    return BadRequest();
                }

                Log.Information("Handling file upload of {Filename} ({Token}) from a caller claiming to be agent {Agent}", filename, token, agentName);

                // agents must encrypt the Id they were given in the request with the secret they share with the controller, and
                // provide the encrypted value as the credential with the request. the validation below verifies a bunch of
                // things, including that the encrypted value matches the expected value. the goal here is to ensure that the
                // caller is the same caller that received the request, and that the caller knows the shared secret.
                if (!Relay.TryValidateFileStreamResponseCredential(token: guid, agentName: agentName, filename: filename, credential: credential))
                {
                    Log.Warning("Failed to authenticate file upload token {Token} from a caller claiming to be agent {Agent}", guid, agentName);
                    return Unauthorized();
                }

                Log.Information("Agent {Agent} authenticated for token {Token}. Forwarding file stream for {Filename}", agentName, guid, filename);

                // pass the stream back to the relay service, which will in turn pass it to the upload service, and use it to
                // feed data into the remote upload. await this call, it will complete when the upload is complete, one way or the other.
                await Relay.HandleFileStreamResponseAsync(agentName, id: guid, stream);

                Log.Information("File upload of {Filename} ({Token}) from agent {Agent} complete", filename, token, agentName);
                return Ok();
            }
            finally
            {
                stream?.TryDispose();
            }
        }

        /// <summary>
        ///     Uploads share information to the connected controller.
        /// </summary>
        /// <param name="token">The unique identifier for the request.</param>
        /// <returns></returns>
        [HttpPost("controller/shares/{token}")]
        [RequestSizeLimit(ONE_TEBIBYTE)]
        [RequestFormLimits(MultipartBodyLengthLimit = ONE_TEBIBYTE)]
        [Authorize(Policy = AuthPolicy.ApiKeyOnly, Roles = AuthRole.ReadWriteOrAdministrator)]
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
            var sanitizedAgentName = SanitizeAgentName(agentName);

            if (!Relay.RegisteredAgents.Any(a => a.Name == agentName) || string.IsNullOrEmpty(credential))
            {
                return Unauthorized();
            }

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

            Log.Information("Handling share upload ({Token}) from a caller claiming to be agent {Agent}", token, agentName);

            if (!Relay.TryValidateShareUploadCredential(token: guid, agentName, credential))
            {
                Log.Warning("Failed to authenticate share upload from caller claiming to be agent {Agent} using token {Token}", agentName, guid);
                return Unauthorized();
            }

            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Program.AppName));
            var temp = Path.Combine(Path.GetTempPath(), Program.AppName, $"share_{sanitizedAgentName}_{Path.GetRandomFileName()}.db");

            try
            {
                Log.Information("Agent {Agent} authenticated for token {Token}. Beginning download of shares to {Filename}", agentName, guid, temp);

                var sw = new Stopwatch();
                sw.Start();

                using var outputStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write);
                using var inputStream = database.OpenReadStream();

                await inputStream.CopyToAsync(outputStream);

                sw.Stop();

                Log.Information("Download of shares from {Agent} ({Token}) complete ({Size} in {Duration}ms)", agentName, guid, ((double)inputStream.Length).SizeSuffix(), sw.ElapsedMilliseconds);

                await Relay.HandleShareUploadAsync(sanitizedAgentName, id: guid, shares, temp);

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
        /// <summary>
        ///     Sanitizes the agent name to prevent resource injection.
        ///     Only allows alphanumeric characters, underscore, and hyphen.
        /// </summary>
        private static string SanitizeAgentName(string agentName)
        {
            if (string.IsNullOrEmpty(agentName))
                return "unknown";
            var safe = new System.Text.StringBuilder();
            foreach (var c in agentName)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    safe.Append(c);
            }
            return safe.Length > 0 ? safe.ToString() : "unknown";
        }
    }
}