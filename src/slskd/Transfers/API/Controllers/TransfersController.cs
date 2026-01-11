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
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;

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
        /// <param name="optionsSnapshot"></param>
        /// <param name="transferService"></param>
        public TransfersController(
            ITransferService transferService,
            IOptionsSnapshot<Options> optionsSnapshot)
        {
            Transfers = transferService;
            OptionsSnapshot = optionsSnapshot;
        }

        private static SemaphoreSlim DownloadRequestLimiter { get; } = new SemaphoreSlim(2, 2);
        private ITransferService Transfers { get; }
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
        ///     Enqueues the specified download.
        /// </summary>
        /// <param name="username">The username of the download source.</param>
        /// <param name="requests">The list of download requests.</param>
        /// <returns></returns>
        /// <response code="201">The download was successfully enqueued.</response>
        /// <response code="403">The download was rejected.</response>
        /// <response code="500">An unexpected error was encountered.</response>
        [HttpPost("downloads/{username}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 403)]
        [ProducesResponseType(typeof(string), 500)]
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
#pragma warning disable SA1611 // Element parameters should be documented
        public async Task<IActionResult> EnqueueAsync([FromRoute, UrlEncoded, Required] string username, [FromBody] IEnumerable<QueueDownloadRequest> requests, CancellationToken cancellationToken = default)
#pragma warning restore SA1611 // Element parameters should be documented
#pragma warning restore CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (!DownloadRequestLimiter.Wait(0))
            {
                return StatusCode(429, "Only one concurrent operation is permitted. Wait until the previous request completes");
            }

            try
            {
                var (enqueued, failed) = await Transfers.Downloads.EnqueueAsync(username, requests.Select(r => (r.Filename, r.Size)), cancellationToken);

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