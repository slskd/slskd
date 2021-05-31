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

namespace slskd.API.Controllers
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.API.DTO;
    using slskd.Search;
    using SearchQuery = Soulseek.SearchQuery;
    using SearchScope = Soulseek.SearchScope;

    /// <summary>
    ///     Search.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class SearchesController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchesController"/> class.
        /// </summary>
        /// <param name="searchService"></param>
        public SearchesController(ISearchService searchService)
        {
            Searches = searchService;
        }

        private ISearchService Searches { get; }

        /// <summary>
        ///     Performs a search for the specified <paramref name="request"/>.
        /// </summary>
        /// <param name="request">The search request.</param>
        /// <returns></returns>
        /// <response code="200">The search completed successfully.</response>
        /// <response code="400">The specified <paramref name="request"/> was malformed.</response>
        /// <response code="500">The search terminated abnormally.</response>
        [HttpPost("")]
        [Authorize]
        public async Task<IActionResult> Post([FromBody]SearchRequest request)
        {
            var id = request.Id ?? Guid.NewGuid();
            var searchText = string.Join(' ', request.SearchText.Split(' ').Where(term => term.Length > 1));

            Search search = null;

            try
            {
                search = await Searches.CreateAsync(id, SearchQuery.FromText(searchText), SearchScope.Network);
                return Ok(search.Responses);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search terminated abnormally: {ex.Message}");
            }
            finally
            {
                search = null;
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
        [Authorize]
        public async Task<IActionResult> GetById([FromRoute]Guid id, [FromQuery]bool includeResponses = false)
        {
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
        [Authorize]
        public async Task<IActionResult> GetResponsesById([FromRoute] Guid id)
        {
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
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await Searches.ListAsync());
        }
    }
}
