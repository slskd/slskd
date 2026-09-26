// <copyright file="MusicBrainzController.cs" company="JP Dillingham">
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

namespace slskd.Integrations.MusicBrainz.API
{
    using System;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     MusicBrainz release lookups.
    /// </summary>
    [Route("api/v{version:apiVersion}/musicbrainz")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class MusicBrainzController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicBrainzController"/> class.
        /// </summary>
        /// <param name="musicBrainzService">The MusicBrainz service.</param>
        /// <param name="coverArtService">The cover art service.</param>
        public MusicBrainzController(IMusicBrainzService musicBrainzService, ICoverArtService coverArtService)
        {
            MusicBrainz = musicBrainzService;
            CoverArt = coverArtService;
        }

        private ICoverArtService CoverArt { get; }
        private IMusicBrainzService MusicBrainz { get; }

        /// <summary>
        ///     Gets the front cover of a release, from the Cover Art Archive or, failing that, from Deezer or iTunes.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <param name="size">The approximate width wanted: 250, 500 or 1200.</param>
        /// <param name="group">The release group MBID; its cover is used when the release has none.</param>
        /// <param name="artist">The release artist, used to look the cover up elsewhere.</param>
        /// <param name="title">The release title, used to look the cover up elsewhere.</param>
        /// <returns></returns>
        /// <response code="200">The cover image.</response>
        /// <response code="404">No cover was found.</response>
        /// <response code="503">MusicBrainz lookups are disabled.</response>
        [HttpGet("releases/{id:guid}/cover")]
        [Authorize(Policy = AuthPolicy.Any)]
        [Produces("image/jpeg", "image/png", "image/webp", "application/json")]
        public async Task<IActionResult> GetCover([FromRoute] Guid id, [FromQuery] int size = 250, [FromQuery] Guid? group = null, [FromQuery] string artist = null, [FromQuery] string title = null)
        {
            return await Call(async () =>
            {
                var cover = await CoverArt.GetCoverAsync(id, group, artist, title, size, HttpContext.RequestAborted);

                if (cover is null)
                {
                    return NotFound();
                }

                Response.Headers.CacheControl = "private, max-age=86400";
                Response.Headers["X-Cover-Source"] = cover.Source;
                return File(cover.Data, cover.ContentType);
            });
        }

        /// <summary>
        ///     Searches MusicBrainz for releases.
        /// </summary>
        /// <param name="query">Plain text, or a MusicBrainz search query such as <c>artist:"x" AND release:"y"</c>.</param>
        /// <param name="limit">The maximum number of releases to return, from 1 to 100.</param>
        /// <param name="offset">The number of releases to skip.</param>
        /// <param name="dismax">Whether to search the query as plain text across titles, artists and labels, rather than as Lucene syntax.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="400">The query is missing.</response>
        /// <response code="502">MusicBrainz could not be reached or rejected the request.</response>
        /// <response code="503">MusicBrainz lookups are disabled.</response>
        [HttpGet("releases")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(MusicBrainzReleaseSearchResult), 200)]
        public async Task<IActionResult> SearchReleases([FromQuery] string query, [FromQuery] int limit = 25, [FromQuery] int offset = 0, [FromQuery] bool dismax = false)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("A query is required");
            }

            return await Call(async () => Ok(await MusicBrainz.SearchReleasesAsync(query, limit, offset, dismax, HttpContext.RequestAborted)));
        }

        /// <summary>
        ///     Gets a MusicBrainz release and its track list.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">MusicBrainz has no release with the ID.</response>
        /// <response code="502">MusicBrainz could not be reached or rejected the request.</response>
        /// <response code="503">MusicBrainz lookups are disabled.</response>
        [HttpGet("releases/{id:guid}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(MusicBrainzRelease), 200)]
        public async Task<IActionResult> GetRelease([FromRoute] Guid id)
        {
            return await Call(async () =>
            {
                var release = await MusicBrainz.GetReleaseAsync(id, HttpContext.RequestAborted);
                return release is null ? NotFound($"MusicBrainz has no release with ID {id}") : Ok(release);
            });
        }

        private async Task<IActionResult> Call(Func<Task<IActionResult>> action)
        {
            if (!MusicBrainz.Enabled)
            {
                return StatusCode(503, "MusicBrainz lookups are disabled in the configuration (integrations.musicbrainz.disabled)");
            }

            try
            {
                return await action();
            }
            catch (MusicBrainzException ex)
            {
                return StatusCode(502, ex.Message);
            }
        }
    }
}
