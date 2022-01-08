// <copyright file="TransfersController.cs" company="slskd Team">
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

namespace slskd.Transfers.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Integrations.FTP;
    using Soulseek;

    /// <summary>
    ///     Transfers.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class TransfersController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransfersController"/> class.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="soulseekClient"></param>
        /// <param name="tracker"></param>
        /// <param name="ftpClient"></param>
        public TransfersController(
            IOptionsSnapshot<Options> options,
            ISoulseekClient soulseekClient,
            ITransferTracker tracker,
            IFTPService ftpClient)
        {
            Client = soulseekClient;
            Tracker = tracker;
            Options = options.Value;
            FTP = ftpClient;
        }

        private Options Options { get; }
        private ISoulseekClient Client { get; }
        private ITransferTracker Tracker { get; }
        private IFTPService FTP { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<TransfersController>();

        /// <summary>
        ///     Cancels the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="id">The id of the download.</param>
        /// <param name="remove">A value indicating whether the tracked download should be removed after cancellation.</param>
        /// <returns></returns>
        /// <response code="204">The download was cancelled successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpDelete("downloads/{username}/{id}")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelDownload([FromRoute, Required] string username, [FromRoute, Required]string id, [FromQuery]bool remove = false)
        {
            return CancelTransfer(TransferDirection.Download, username, id, remove);
        }

        /// <summary>
        ///     Cancels the specified upload.
        /// </summary>
        /// <param name="username">The username of the upload destination.</param>
        /// <param name="id">The id of the upload.</param>
        /// <param name="remove">A value indicating whether the tracked upload should be removed after cancellation.</param>
        /// <returns></returns>
        /// <response code="204">The upload was cancelled successfully.</response>
        /// <response code="404">The specified upload was not found.</response>
        [HttpDelete("uploads/{username}/{id}")]
        [Authorize]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelUpload([FromRoute, Required] string username, [FromRoute, Required]string id, [FromQuery]bool remove = false)
        {
            return CancelTransfer(TransferDirection.Upload, username, id, remove);
        }

        /// <summary>
        ///     Enqueues the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="requests">The list of download requests.</param>
        /// <returns></returns>
        /// <response code="201">The download was successfully enqueued.</response>
        /// <response code="403">The download was rejected.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [HttpPost("downloads/{username}")]
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> Enqueue([FromRoute, Required]string username, [FromBody]IEnumerable<QueueDownloadRequest> requests)
        {
            try
            {
                Log.Information("Downloading {Count} files from user {Username}", requests.Count(), username);

                Log.Debug("Priming connection for user {Username}", username);
                await Client.ConnectToUserAsync(username, invalidateCache: true);
                Log.Debug("Connection for user '{Username}' primed", username);

                foreach (var request in requests)
                {
                    Log.Debug("Attempting to enqueue {Filename} from user {Username}", request.Filename, username);

                    var waitUntilEnqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var cts = new CancellationTokenSource();

                    var downloadTask = Task.Run(async () =>
                    {
                        await Client.DownloadAsync(username, request.Filename, () => GetLocalFileStream(request.Filename, Options.Directories.Incomplete), request.Size, 0, request.Token, new TransferOptions(disposeOutputStreamOnCompletion: true, stateChanged: (e) =>
                        {
                            Tracker.AddOrUpdate(e, cts);

                            if (e.Transfer.State == TransferStates.Queued || e.Transfer.State == TransferStates.Initializing)
                            {
                                waitUntilEnqueue.TrySetResult(true);
                            }
                        }, progressUpdated: (e) => Tracker.AddOrUpdate(e, cts)), cts.Token);

                        MoveFile(request.Filename, Options.Directories.Incomplete, Options.Directories.Downloads);

                        if (Options.Integration.Ftp.Enabled)
                        {
                            _ = FTP.UploadAsync(request.Filename.ToLocalFilename(Options.Directories.Downloads));
                        }
                    });

                    // wait until either the waitUntilEnqueue task completes because the download was successfully queued, or the
                    // downloadTask throws due to an error prior to successfully queueing.
                    var task = await Task.WhenAny(waitUntilEnqueue.Task, downloadTask);

                    if (task == downloadTask)
                    {
                        if (downloadTask.Exception is AggregateException)
                        {
                            var rejected = downloadTask.Exception?.InnerExceptions.Where(e => e is TransferRejectedException) ?? Enumerable.Empty<Exception>();
                            if (rejected.Any())
                            {
                                Log.Error("Download of {Filename} from {Username} was rejected: {Reason}", request.Filename, username, rejected.First().Message);
                                return StatusCode(403, rejected.First().Message);
                            }
                        }

                        Log.Error("Failed to download {Filename} from {Username}: Message", request.Filename, username, downloadTask.Exception.Message);
                        return StatusCode(500, downloadTask.Exception.Message);
                    }

                    Log.Debug("Successfully enqueued {Filename} from user {Username}", request.Filename, username);
                }

                // if nothing threw, just return ok. the download will continue waiting in the background.
                Log.Information("Successfully enqueued {Count} files from user {Username}", requests.Count(), username);
                return StatusCode(201);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download file(s) from user {Username}: {Message}", username, ex.Message);
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        ///     Gets all downloads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetDownloads()
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Download)
                .ToMap());
        }

        /// <summary>
        ///     Gets all downloads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/{username}")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetDownloads([FromRoute, Required]string username)
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Download)
                .FromUser(username)
                .ToMap());
        }

        /// <summary>
        ///     Gets the downlaod for the specified username matching the specified filename, and requests
        ///     the current place in the remote queue of the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="id">The id of the download.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpGet("downloads/{username}/{id}")]
        [Authorize]
        [ProducesResponseType(typeof(API.Transfer), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetPlaceInQueue([FromRoute, Required]string username, [FromRoute, Required]string id)
        {
            var record = Tracker.Transfers.WithDirection(TransferDirection.Download).FromUser(username).WithId(id);

            if (record == default)
            {
                return NotFound();
            }

            record.Transfer.PlaceInQueue = await Client.GetDownloadPlaceInQueueAsync(username, record.Transfer.Filename);
            return Ok(record.Transfer);
        }

        /// <summary>
        ///     Gets all uploads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetUploads()
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Upload)
                .ToMap());
        }

        /// <summary>
        ///     Gets all uploads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, Required]string username)
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Upload)
                .FromUser(username)
                .ToMap());
        }

        /// <summary>
        ///     Gets the upload for the specified username matching the specified filename.
        /// </summary>
        /// <param name="username">The username of the upload destination.</param>
        /// <param name="id">The id of the upload.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}/{id}")]
        [Authorize]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, Required]string username, [FromRoute, Required]string id)
        {
            return Ok(Tracker.Transfers
                .WithDirection(TransferDirection.Upload)
                .FromUser(username)
                .WithId(id).Transfer);
        }

        private static FileStream GetLocalFileStream(string remoteFilename, string saveDirectory)
        {
            var localFilename = remoteFilename.ToLocalFilename(baseDirectory: saveDirectory);
            var path = Path.GetDirectoryName(localFilename);

            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }

            return new FileStream(localFilename, FileMode.Create);
        }

        private static void MoveFile(string filename, string sourceDirectory, string destinationDirectory)
        {
            var sourceFilename = filename.ToLocalFilename(sourceDirectory);
            var destinationFilename = filename.ToLocalFilename(destinationDirectory);

            var destinationPath = Path.GetDirectoryName(destinationFilename);

            if (!System.IO.Directory.Exists(destinationPath))
            {
                System.IO.Directory.CreateDirectory(destinationPath);
            }

            System.IO.File.Move(sourceFilename, destinationFilename, overwrite: true);

            if (!System.IO.Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(sourceFilename)).Any())
            {
                System.IO.Directory.Delete(Path.GetDirectoryName(sourceFilename));
            }
        }

        private IActionResult CancelTransfer(TransferDirection direction, string username, string id, bool remove = false)
        {
            if (Tracker.TryGet(direction, username, id, out var transfer))
            {
                transfer.CancellationTokenSource.Cancel();

                if (remove)
                {
                    Tracker.TryRemove(direction, username, id);
                }

                return NoContent();
            }

            return NotFound();
        }
    }
}