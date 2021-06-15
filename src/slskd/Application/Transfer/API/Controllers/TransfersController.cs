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

namespace slskd.Transfer.API
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
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
        /// <param name="client"></param>
        /// <param name="tracker"></param>
        public TransfersController(
            Microsoft.Extensions.Options.IOptionsSnapshot<Options> options,
            ISoulseekClient client,
            ITransferTracker tracker)
        {
            Client = client;
            Tracker = tracker;
            Options = options.Value;
        }

        private Options Options { get; }
        private ISoulseekClient Client { get; }
        private ITransferTracker Tracker { get; }

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
        /// <param name="request">The download request.</param>
        /// <returns></returns>
        /// <response code="201">The download was successfully enqueued.</response>
        /// <response code="403">The download was rejected.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [HttpPost("downloads/{username}")]
        [Authorize]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> Enqueue([FromRoute, Required]string username, [FromBody]QueueDownloadRequest request)
        {
            try
            {
                var waitUntilEnqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var stream = GetLocalFileStream(request.Filename, Options.Directories.Incomplete);

                var cts = new CancellationTokenSource();

                var downloadTask = Task.Run(async () =>
                {
                    await Client.DownloadAsync(username, request.Filename, stream, request.Size, 0, request.Token, new TransferOptions(disposeOutputStreamOnCompletion: true, stateChanged: (e) =>
                    {
                        Tracker.AddOrUpdate(e, cts);

                        if (e.Transfer.State == TransferStates.Queued || e.Transfer.State == TransferStates.Initializing)
                        {
                            waitUntilEnqueue.TrySetResult(true);
                        }
                    }, progressUpdated: (e) => Tracker.AddOrUpdate(e, cts)), cts.Token);

                    MoveFile(request.Filename, Options.Directories.Incomplete, Options.Directories.Downloads);
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
                            return StatusCode(403, rejected.First().Message);
                        }
                    }

                    return StatusCode(500, downloadTask.Exception.Message);
                }

                // if it didn't throw, just return ok. the download will continue waiting in the background.
                return StatusCode(201);
            }
            catch (Exception ex)
            {
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