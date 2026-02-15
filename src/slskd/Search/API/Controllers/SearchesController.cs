// <copyright file="SearchesController.cs" company="slskd Team">
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

namespace slskd.Search.API
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using SearchQuery = Soulseek.SearchQuery;
    using SearchScope = Soulseek.SearchScope;

    /// <summary>
    ///     Search.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class SearchesController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchesController"/> class.
        /// </summary>
        /// <param name="searchService"></param>
        /// <param name="optionsSnapshot"></param>
        public SearchesController(ISearchService searchService, IOptionsSnapshot<Options> optionsSnapshot)
        {
            Searches = searchService;
            OptionsSnapshot = optionsSnapshot;
        }

        private static SemaphoreSlim SearchRequestLimiter { get; } = new SemaphoreSlim(1, 1);
        private ISearchService Searches { get; }
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<SearchesController>();

        /// <summary>
        ///     Performs a search for the specified <paramref name="request"/>.
        /// </summary>
        /// <param name="request">The search request.</param>
        /// <returns></returns>
        /// <response code="200">The search completed successfully.</response>
        /// <response code="400">The specified <paramref name="request"/> was malformed.</response>
        /// <response code="500">The search terminated abnormally.</response>
        [HttpPost("")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> Post([FromBody] SearchRequest request)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request.SearchText))
            {
                return BadRequest("SearchText may not be null or empty");
            }

            if (!SearchRequestLimiter.Wait(0))
            {
                return StatusCode(429, "Only one concurrent operation is permitted. Wait until the previous request completes");
            }

            try
            {
                var id = request.Id ?? Guid.NewGuid();

                Search search;

                try
                {
                    search = await Searches.StartAsync(id, SearchQuery.FromText(request.SearchText), SearchScope.Network, request.ToSearchOptions());
                    return Ok(search);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is Soulseek.DuplicateTokenException)
                {
                    Log.Error(ex, "Failed to execute search {Search}: {Message}", request, ex.Message);
                    return BadRequest(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    Log.Error(ex, "Failed to execute search {Search}: {Message}", request, ex.Message);
                    return Conflict(ex.Message);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to execute search {Search}: {Message}", request, ex.Message);
                    return StatusCode(500, ex.Message);
                }
            }
            finally
            {
                SearchRequestLimiter.Release();
            }
        }

        /// <summary>
        ///     Gets the state of the search corresponding to the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the search.</param>
        /// <param name="includeResponses">A value indicating whether to include search responses in the response.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">A matching search was not found.</response>
        [HttpGet("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, [FromQuery] bool includeResponses = false)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var search = await Searches.FindAsync(search => search.Id == id, includeResponses);

            if (search == default)
            {
                return NotFound();
            }

            return Ok(search);
        }

        /// <summary>
        ///     Gets the state of the search corresponding to the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the search.</param>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">A matching search was not found.</response>
        [HttpGet("{id}/responses")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetResponsesById([FromRoute] Guid id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var search = await Searches.FindAsync(search => search.Id == id, includeResponses: true);

            if (search == default)
            {
                return NotFound();
            }

            return Ok(search.Responses);
        }

        /// <summary>
        ///     Gets the list of active and completed searches.
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetAll()
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var searches = await Searches.ListAsync();
            return Ok(searches);
        }

        /// <summary>
        ///     Stops the search corresponding to the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the search.</param>
        /// <response code="200">The search was stopped.</response>
        /// <response code="304">The search was not in progress.</response>
        /// <returns></returns>
        [HttpPut("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(200)]
        [ProducesResponseType(304)]
        public async Task<IActionResult> Cancel([FromRoute] Guid id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var search = await Searches.FindAsync(search => search.Id == id);

            if (search == default)
            {
                return NotFound();
            }

            Searches.TryCancel(id);
            return Ok();
        }

        /// <summary>
        ///     Deletes the search corresponding to the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique id of the search.</param>
        /// <response code="204">The search was deleted.</response>
        /// <response code="404">A search with the specified id could not be found.</response>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [Authorize(Policy = AuthPolicy.Any)]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var search = await Searches.FindAsync(search => search.Id == id, includeResponses: false);

            if (search == default)
            {
                return NotFound();
            }

            await Searches.DeleteAsync(search);
            return NoContent();
        }
    }
}
