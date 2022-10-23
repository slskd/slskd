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

            string credential = default;
            Stream stream = default;
            string filename = default;

            try
            {
                var boundary = MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType));
                var reader = new MultipartReader(boundary, Request.Body);

                var credentialSection = await reader.ReadNextSectionAsync();
                using var sr = new StreamReader(credentialSection.Body);
                credential = sr.ReadToEnd();

                var fileSection = await reader.ReadNextSectionAsync();
                ContentDispositionHeaderValue.TryParse(fileSection.ContentDisposition, out var contentDisposition);
                filename = contentDisposition.FileName.Value;
                stream = fileSection.Body;
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to handle file upload from agent {Agent}: {Message}", agentName, ex.Message);
                return BadRequest();
            }

            if (!Network.TryValidateFileUploadCredential(token: guid, agentName, filename, credential))
            {
                Log.Warning("Failed to authenticate file upload from caller claiming to be agent {Agent}", agentName);
                return Unauthorized();
            }

            Console.WriteLine($"credential: {credential}, filename: {filename}, stream pos {stream.Position} len {stream.Length}");

            Console.WriteLine("Opening request stream...");

            Console.WriteLine("Stream open. calling handler...");

            // pass the stream back to the network service, which will in turn pass it to the
            // upload service, and use it to feed data into the remote upload. await this call,
            // it will complete when the upload is complete.
            await Network.HandleGetFileStreamResponse(agentName, filename, id: guid, stream);

            stream.Dispose();
            Console.WriteLine("Upload complete");
            return Ok();
        }

        //[DisableFormValueModelBinding]
        //[RequestSizeLimit(MaxFileSize)]
        //[RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
        //public async Task ReceiveFile()
        //{
        //    if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
        //        throw new BadRequestException("Not a multipart request");

        //    var boundary = MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType));
        //    var reader = new MultipartReader(boundary, Request.Body);

        //    // note: this is for a single file, you could also process multiple files
        //    var section = await reader.ReadNextSectionAsync();

        //    if (section == null)
        //        throw new BadRequestException("No sections in multipart defined");

        //    if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
        //        throw new BadRequestException("No content disposition in multipart defined");

        //    var fileName = contentDisposition.FileNameStar.ToString();
        //    if (string.IsNullOrEmpty(fileName))
        //    {
        //        fileName = contentDisposition.FileName.ToString();
        //    }

        //    if (string.IsNullOrEmpty(fileName))
        //        throw new BadRequestException("No filename defined.");

        //    using var fileStream = section.Body;
        //    await SendFileSomewhere(fileStream);
        //}

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
