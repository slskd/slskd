// <copyright file="TransfersController.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Transfers.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using slskd.Users;
    using Soulseek;

    /// <summary>
    ///     Transfers.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class TransfersController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransfersController"/> class.
        /// </summary>
        /// <param name="optionsSnapshot"></param>
        /// <param name="userService"></param>
        /// <param name="transferService"></param>
        public TransfersController(
            ITransferService transferService,
            IUserService userService,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            Transfers = transferService;
            Users = userService;
            OptionsSnapshot = optionsSnapshot;
        }

        private static SemaphoreSlim DownloadRequestLimiter { get; } = new SemaphoreSlim(2, 2);
        private ITransferService Transfers { get; }
        private IUserService Users { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<TransfersController>();

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
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelDownloadAsync([FromRoute, UrlEncoded, Required] string username, [FromRoute, Required] string id, [FromQuery] bool remove = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                Transfers.Downloads.TryCancel(guid);

                if (remove)
                {
                    Transfers.Downloads.Remove(guid);
                }

                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        ///     Removes all completed downloads, regardless of whether they failed or succeeded.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The downloads were removed successfully.</response>
        [HttpDelete("downloads/all/completed")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public IActionResult ClearCompletedDownloads()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            try
            {
                Transfers.Downloads.Remove(t => !t.Removed && TransferStateCategories.Completed.Contains(t.State));
                return NoContent();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove completed downloads: {Message}", ex.Message);
                return StatusCode(500, ex.Message);
            }
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
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public IActionResult CancelUpload([FromRoute, UrlEncoded, Required] string username, [FromRoute, Required] string id, [FromQuery] bool remove = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                Transfers.Uploads.TryCancel(guid);

                if (remove)
                {
                    Transfers.Uploads.Remove(guid);
                }

                return NoContent();
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        ///     Removes all completed uploads, regardless of whether they failed or succeeded.
        /// </summary>
        /// <returns></returns>
        /// <response code="204">The uploads were removed successfully.</response>
        [HttpDelete("uploads/all/completed")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public IActionResult ClearCompletedUploads()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            try
            {
                Transfers.Uploads.Remove(t => !t.Removed && TransferStateCategories.Completed.Contains(t.State));
                return NoContent();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove completed uploads: {Message}", ex.Message);
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        ///     (Obsolete) Enqueues the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="requests">The list of download requests.</param>
        /// <returns></returns>
        /// <response code="201">The download was successfully enqueued.</response>
        /// <response code="403">The download was rejected.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [Obsolete("Will be phased out in future versions; use batches")]
        [HttpPost("downloads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> EnqueueAsync([FromRoute, UrlEncoded, Required] string username, [FromBody] IEnumerable<QueueDownloadRequest> requests)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetReadableString());
            }

            if (!requests?.Any() ?? true)
            {
                return BadRequest("At least one file is required");
            }

            if (requests.Any(r => r is null))
            {
                return BadRequest("One or more files in the request are null");
            }

            if (requests.Any(r => FileSafety.ContainsTraversalSegments(r.Filename)))
            {
                return BadRequest("One or more files in the request contain a dangerous path traversal segment");
            }

            if (!DownloadRequestLimiter.Wait(0))
            {
                return StatusCode(429, "Only one concurrent operation is permitted. Wait until the previous request completes");
            }

            try
            {
                var endpoint = await Users.GetIPEndPointAsync(username);

                if (Users.IsBlacklisted(username, endpoint.Address))
                {
                    throw new UserOfflineException($"User {username} appears to be offline");
                }

                var (enqueued, failed) = await Transfers.Downloads.EnqueueAsync(username, requests.Select(r => (r.Filename, r.Size)));

                return StatusCode(201, new { Enqueued = enqueued, Failed = failed });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue {Count} files for {Username}: {Message}", requests.Count(), username, ex.Message);
                return StatusCode(500, ex.Message);
            }
            finally
            {
                DownloadRequestLimiter.Release();
            }
        }

        /// <summary>
        ///     Enqueues a batch of downloads.
        /// </summary>
        /// <param name="request">The batch details.</param>
        /// <returns></returns>
        /// <response code="201">One or more downloads were successfully enqueued.</response>
        /// <response code="200">The request succeeded, but all downloads were already enqueued.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [HttpPost("downloads/batches")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 409)]
        [ProducesResponseType(typeof(string), 429)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> EnqueueBatchAsync([FromBody] QueueDownloadBatchRequest request)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetReadableString());
            }

            if ((request.Files?.Count ?? 0) == 0)
            {
                return BadRequest("At least one file is required");
            }

            if (request.Files.Any(r => r is null))
            {
                return BadRequest("One or more files in the request are null");
            }

            if (request.Files.DistinctBy(f => f.Filename).Count() != request.Files.Count)
            {
                return BadRequest("Two or more files in the request are repeated");
            }

            // a user would have to deliberately fake this, as no OS and no client are capable of scanning
            // such a file into configured shares
            if (request.Files.Any(r => FileSafety.ContainsTraversalSegments(r.Filename)))
            {
                Log.Warning("Attempt to enqueue one or more files containing unsafe path segments from user {Username} (one or more of path traversal characters '.' and '..')", request.Username);
                return BadRequest("One or more files in the request contain an unsafe path traversal segment");
            }

            Guid? batchId;
            Guid? searchId;

            // validation rules should prevent any problems here, but we have a backstop just in case
            try
            {
                batchId = string.IsNullOrWhiteSpace(request.Id) ? null : Guid.Parse(request.Id);
                searchId = string.IsNullOrWhiteSpace(request.SearchId) ? null : Guid.Parse(request.SearchId);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to parse Guid from enqueue batch input: {Message}", ex.Message);
                return BadRequest("One or more provided identifiers is not a valid GUID/UUIDv4");
            }

            batchId ??= Guid.NewGuid();

            if (!DownloadRequestLimiter.Wait(0))
            {
                return StatusCode(429, "Only one concurrent operation is permitted. Wait until the previous request completes");
            }

            try
            {
                var endpoint = await Users.GetIPEndPointAsync(request.Username);

                if (Users.IsBlacklisted(request.Username, endpoint.Address))
                {
                    throw new UserOfflineException($"User {request.Username} appears to be offline");
                }

                // throws DuplicateException if a record already exists
                await Transfers.Downloads.Batches.CreateAsync(new()
                {
                    Id = batchId.Value,
                    SearchId = searchId,
                    Username = request.Username,
                    Options = new()
                    {
                        Destination = request.Options.Destination,
                    },
                });

                // Transfer records will have been inserted before this returns, unless they were rejected
                // because they were already in progress, in which case they will show up in 'failed'
                var (enqueued, failed) = await Transfers.Downloads.EnqueueAsync(
                    username: request.Username,
                    files: request.Files.Select(r => (r.Filename, r.Size)),
                    batchId: batchId);

                if (failed.Count > 0)
                {
                    Log.Warning("Failed to enqueue {Count} of {Total} files for {Username}; transfers already queued or in progress", failed.Count, request.Files.Count);
                }

                // the returned batch will have whatever Transfers were successfully inserted attached (via Include())
                var batch = await Transfers.Downloads.Batches.FindAsync(b => b.Id == batchId);

                // all of the files were already queued; list of transfers should be empty
                if (batch.Transfers.Count == 0)
                {
                    return Ok(batch);
                }

                return StatusCode(201, batch);
            }
            catch (DuplicateException ex)
            {
                Log.Error(ex, "Failed to enqueue {Count} files for {Username}: A Batch with ID {BatchId} already exists", request.Files.Count, request.Username, request.Id);
                return Conflict($"A batch with ID {batchId} already exists");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue {Count} files for {Username}: {Message}", request.Files.Count, request.Username, ex.Message);
                return StatusCode(500, ex.Message);
            }
            finally
            {
                DownloadRequestLimiter.Release();
            }
        }

        /// <summary>
        ///     Gets all downloads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetDownloadsAsync([FromQuery] bool includeRemoved = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var downloads = Transfers.Downloads.List(includeRemoved: includeRemoved);

            var response = downloads.GroupBy(t => t.Username).Select(grouping => new UserResponse()
            {
                Username = grouping.Key,
                Directories = grouping.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            });

            return Ok(response);
        }

        /// <summary>
        ///     Gets the specified batch and associated transfers.
        /// </summary>
        /// <param name="id">The id of the batch.</param>
        /// <returns></returns>
        /// <response code="400">The specified id is not valid.</response>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified batch was not found.</response>
        [HttpGet("downloads/batches/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public async Task<IActionResult> Get([FromRoute, Required] string id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                var found = await Transfers.Downloads.Batches.FindAsync(b => b.Id == guid);

                if (found is null)
                {
                    return NotFound();
                }

                return Ok(found);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get batch with ID {Id}: {Message}", guid, ex.Message);
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        ///     Gets all downloads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("downloads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetDownloadsAsync([FromRoute, UrlEncoded, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var downloads = Transfers.Downloads.List(d => d.Username == username);

            if (!downloads.Any())
            {
                return NotFound();
            }

            var response = new UserResponse()
            {
                Username = username,
                Directories = downloads.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            };

            return Ok(response);
        }

        [HttpGet("downloads/{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(API.Transfer), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetDownload([FromRoute, UrlEncoded, Required] string username, [FromRoute, Required] string id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            var download = Transfers.Downloads.Find(t => t.Id == guid);

            if (download == default)
            {
                return NotFound();
            }

            return Ok(download);
        }

        /// <summary>
        ///     Gets the download for the specified username matching the specified filename, and requests
        ///     the current place in the remote queue of the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="id">The id of the download.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The specified download was not found.</response>
        [HttpGet("downloads/{username}/{id}/position")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(API.Transfer), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetPlaceInQueueAsync([FromRoute, UrlEncoded, Required] string username, [FromRoute, Required] string id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (Users.IsBlacklisted(username))
            {
                return NotFound();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            try
            {
                var place = await Transfers.Downloads.GetPlaceInQueueAsync(guid);
                return Ok(place);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        ///     Gets all uploads.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromQuery] bool includeRemoved = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            // todo: refactor this so it doesn't return the world. start and end time params
            // should be required.  consider pagination.
            var uploads = Transfers.Uploads.List(t => true, includeRemoved: includeRemoved);

            var response = uploads.GroupBy(t => t.Username).Select(grouping => new UserResponse()
            {
                Username = grouping.Key,
                Directories = grouping.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            });

            return Ok(response);
        }

        /// <summary>
        ///     Gets all uploads for the specified username.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, UrlEncoded, Required] string username)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var uploads = Transfers.Uploads.List(d => d.Username == username, includeRemoved: false);

            if (!uploads.Any())
            {
                return NotFound();
            }

            var response = new UserResponse()
            {
                Username = username,
                Directories = uploads.GroupBy(g => g.Filename.DirectoryName()).Select(d => new DirectoryResponse()
                {
                    Directory = d.Key,
                    FileCount = d.Count(),
                    Files = d.ToList(),
                }),
            };

            return Ok(response);
        }

        /// <summary>
        ///     Gets the upload for the specified username matching the specified filename.
        /// </summary>
        /// <param name="username">The username of the upload destination.</param>
        /// <param name="id">The id of the upload.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet("uploads/{username}/{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        public IActionResult GetUploads([FromRoute, UrlEncoded, Required] string username, [FromRoute, Required] string id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!Guid.TryParse(id, out var guid))
            {
                return BadRequest();
            }

            var upload = Transfers.Uploads.Find(t => t.Id == guid);

            if (upload == default)
            {
                return NotFound();
            }

            return Ok(upload);
        }
    }
}