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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.API.DTO;
    using slskd.Search;
    using Soulseek;

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
        /// <param name="client"></param>
        /// <param name="tracker"></param>
        public SearchesController(ISoulseekClient client, ISearchTracker tracker, ISearchService searchService)
        {
            Client = client;
            Tracker = tracker;
            SearchService = searchService;
        }

        private ISearchService SearchService { get; }
        private ISoulseekClient Client { get; }
        private ISearchTracker Tracker { get; }

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
        [ProducesResponseType(typeof(IEnumerable<Soulseek.SearchResponse>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(typeof(string), 500)]
        public async Task<IActionResult> Post([FromBody]SearchRequest request)
        {
            var id = request.Id ?? Guid.NewGuid();
            var searchText = string.Join(' ', request.SearchText.Split(' ').Where(term => term.Length > 1));

            slskd.Search.Search search = null;

            try
            {
                search = await SearchService.SearchAsync(id, SearchQuery.FromText(searchText), SearchScope.Network);
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
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">A matching search was not found.</response>
        [HttpGet("{id}")]
        [Authorize]
        [ProducesResponseType(typeof(slskd.Search.Search), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById([FromRoute]Guid id)
        {
            var search = await SearchService.FindAsync(search => search.Id == id);

            if (search == default)
            {
                Console.WriteLine($"Search: {id} not found");
                return NotFound();
            }

            return Ok(search);
        }
    }
}
