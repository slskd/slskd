// <copyright file="InterestsController.cs" company="JP Dillingham">
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

namespace slskd.Interests.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    /// <summary>
    ///     Interests, recommendations and the users who share them.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class InterestsController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="InterestsController"/> class.
        /// </summary>
        /// <param name="interestService"></param>
        public InterestsController(IInterestService interestService)
        {
            Interests = interestService;
        }

        private IInterestService Interests { get; }

        /// <summary>
        ///     Retrieves the users who like the specified <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The interest.</param>
        /// <returns>The usernames of the users.</returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="503">The client isn't connected to the server.</response>
        /// <response code="504">The server didn't respond in time.</response>
        [HttpGet("users")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<string>), 200)]
        [ProducesResponseType(typeof(string), 503)]
        [ProducesResponseType(typeof(string), 504)]
        public async Task<IActionResult> GetItemSimilarUsers([FromQuery, Required] string item)
        {
            if (InterestService.Normalize(item).Length == 0)
            {
                return BadRequest("An interest is required");
            }

            return await InvokeAsync(() => Interests.GetItemSimilarUsersAsync(item));
        }

        /// <summary>
        ///     Retrieves users with interests similar to yours.
        /// </summary>
        /// <returns>The users.</returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="503">The client isn't connected to the server.</response>
        /// <response code="504">The server didn't respond in time.</response>
        [HttpGet("users/similar")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(IEnumerable<SimilarUser>), 200)]
        [ProducesResponseType(typeof(string), 503)]
        [ProducesResponseType(typeof(string), 504)]
        public Task<IActionResult> GetSimilarUsers()
        {
            return InvokeAsync(() => Interests.GetSimilarUsersAsync());
        }

        /// <summary>
        ///     Retrieves interests related to the specified <paramref name="item"/>, or recommended from yours if no item is
        ///     given.
        /// </summary>
        /// <param name="item">The optional interest.</param>
        /// <returns>The recommendations.</returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="503">The client isn't connected to the server.</response>
        /// <response code="504">The server didn't respond in time.</response>
        [HttpGet("recommendations")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Recommendations), 200)]
        [ProducesResponseType(typeof(string), 503)]
        [ProducesResponseType(typeof(string), 504)]
        public Task<IActionResult> GetRecommendations([FromQuery] string item = null)
        {
            return string.IsNullOrWhiteSpace(item)
                ? InvokeAsync(() => Interests.GetRecommendationsAsync())
                : InvokeAsync(() => Interests.GetItemRecommendationsAsync(item));
        }

        /// <summary>
        ///     Retrieves the most popular interests on the network.
        /// </summary>
        /// <returns>The recommendations.</returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="503">The client isn't connected to the server.</response>
        /// <response code="504">The server didn't respond in time.</response>
        [HttpGet("recommendations/global")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(typeof(Recommendations), 200)]
        [ProducesResponseType(typeof(string), 503)]
        [ProducesResponseType(typeof(string), 504)]
        public Task<IActionResult> GetGlobalRecommendations()
        {
            return InvokeAsync(() => Interests.GetGlobalRecommendationsAsync());
        }

        /// <summary>
        ///     Runs a request to the server, translating connection problems into status codes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        private async Task<IActionResult> InvokeAsync<T>(Func<Task<T>> request)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            try
            {
                return Ok(await request());
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(503, ex.Message);
            }
            catch (TimeoutException ex)
            {
                return StatusCode(504, ex.Message);
            }
            catch (NotSupportedException ex)
            {
                return StatusCode(501, ex.Message);
            }
        }
    }
}
