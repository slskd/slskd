// <copyright file="SavedReleasesController.cs" company="JP Dillingham">
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
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Search;

    /// <summary>
    ///     Releases that were searched for on Soulseek.
    /// </summary>
    [Route("api/v{version:apiVersion}/musicbrainz/saved")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class SavedReleasesController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SavedReleasesController"/> class.
        /// </summary>
        /// <param name="savedReleaseService">The saved release service.</param>
        /// <param name="searchService">The search service.</param>
        public SavedReleasesController(ISavedReleaseService savedReleaseService, ISearchService searchService)
        {
            SavedReleases = savedReleaseService;
            Searches = searchService;
        }

        private ISavedReleaseService SavedReleases { get; }
        private ISearchService Searches { get; }

        /// <summary>
        ///     Lists the saved releases, most recent first.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        [HttpGet]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IReadOnlyList<SavedRelease>), 200)]
        public IActionResult List()
        {
            return Ok(SavedReleases.List());
        }

        /// <summary>
        ///     Gets a saved release.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">The release isn't saved.</response>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(SavedRelease), 200)]
        public IActionResult Get([FromRoute] Guid id)
        {
            var saved = SavedReleases.Find(id);
            return saved is null ? NotFound() : Ok(saved);
        }

        /// <summary>
        ///     Saves a release and the Soulseek search for it.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <param name="request">The release and search.</param>
        /// <returns></returns>
        /// <response code="200">The release was saved.</response>
        /// <response code="400">The request is invalid.</response>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(SavedRelease), 200)]
        public async Task<IActionResult> Save([FromRoute] Guid id, [FromBody] SavedRelease request)
        {
            if (request?.Release is null || request.Release.Id != id)
            {
                return BadRequest("The release ID in the body must match the ID in the route");
            }

            return Ok(await SavedReleases.SaveAsync(request));
        }

        /// <summary>
        ///     Removes a saved release, and its Soulseek search unless asked not to.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <param name="keepSearch">Whether to keep the Soulseek search.</param>
        /// <returns></returns>
        /// <response code="204">The release was removed.</response>
        /// <response code="404">The release isn't saved.</response>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        public async Task<IActionResult> Remove([FromRoute] Guid id, [FromQuery] bool keepSearch = false)
        {
            var removed = await SavedReleases.RemoveAsync([id]);

            if (removed.Count == 0)
            {
                return NotFound();
            }

            if (!keepSearch)
            {
                await DeleteSearchesAsync(removed);
            }

            return NoContent();
        }

        /// <summary>
        ///     Removes every saved release, and their Soulseek searches unless asked not to.
        /// </summary>
        /// <param name="keepSearches">Whether to keep the Soulseek searches.</param>
        /// <returns></returns>
        /// <response code="200">The releases were removed; the response is the number removed.</response>
        [HttpDelete]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(int), 200)]
        public async Task<IActionResult> RemoveAll([FromQuery] bool keepSearches = false)
        {
            var removed = await SavedReleases.RemoveAsync();

            if (!keepSearches)
            {
                await DeleteSearchesAsync(removed);
            }

            return Ok(removed.Count);
        }

        private async Task DeleteSearchesAsync(IEnumerable<SavedRelease> releases)
        {
            foreach (var searchId in releases.Select(saved => saved.SearchId).OfType<Guid>())
            {
                var search = await Searches.FindAsync(search => search.Id == searchId);

                if (search is not null)
                {
                    Searches.TryCancel(searchId);
                    await Searches.DeleteAsync(search);
                }
            }
        }
    }
}
