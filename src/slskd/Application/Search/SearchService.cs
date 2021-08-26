// <copyright file="SearchService.cs" company="slskd Team">
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

namespace slskd.Search
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using ISoulseekClient = Soulseek.ISoulseekClient;
    using SearchOptions = Soulseek.SearchOptions;
    using SearchQuery = Soulseek.SearchQuery;
    using SearchScope = Soulseek.SearchScope;
    using SearchStates = Soulseek.SearchStates;
    using SoulseekSearch = Soulseek.Search;

    /// <summary>
    ///     Handles the lifecycle and persistence of searches.
    /// </summary>
    public class SearchService : ISearchService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchService"/> class.
        /// </summary>
        /// <param name="optionsMonitor"></param>
        /// <param name="client">The client instance to use.</param>
        /// <param name="contextFactory">The database context to use.</param>
        /// <param name="log">The logger.</param>
        public SearchService(
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient client,
            IDbContextFactory<SearchDbContext> contextFactory,
            ILogger<SearchService> log)
        {
            OptionsMonitor = optionsMonitor;
            Client = client;
            ContextFactory = contextFactory;
            Log = log;
        }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; }
            = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ISoulseekClient Client { get; }
        private IDbContextFactory<SearchDbContext> ContextFactory { get; }
        private ILogger<SearchService> Log { get; set; }

        /// <summary>
        ///     Performs a search for the specified <paramref name="query"/> and <paramref name="scope"/>.
        /// </summary>
        /// <param name="id">A unique identifier for the search.</param>
        /// <param name="query">The search query.</param>
        /// <param name="scope">The search scope.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The completed search.</returns>
        public async Task<Search> CreateAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null)
        {
            var token = Client.GetNextToken();
            var cancellationTokenSource = new CancellationTokenSource();

            var search = new Search()
            {
                SearchText = query.SearchText,
                Token = token,
                Id = id,
                State = SearchStates.Requested,
                StartedAt = DateTime.UtcNow,
            };

            options ??= new SearchOptions();
            options = options.WithActions(
                stateChanged: (args) => UpdateSearchState(search, args.Search),
                responseReceived: (args) => UpdateSearchState(search, args.Search));

            try
            {
                using var context = ContextFactory.CreateDbContext();

                context.Add(search);
                await context.SaveChangesAsync();

                CancellationTokens.TryAdd(id, cancellationTokenSource);
                var responses = new List<Response>();

                var soulseekSearch = await Client.SearchAsync(
                    query,
                    responseReceived: (response) =>
                    {
                        responses.Add(Response.FromSoulseekSearchResponse(response));
                    },
                    scope,
                    token,
                    options,
                    cancellationToken: cancellationTokenSource.Token);

                CancellationTokens.TryRemove(id, out _);

                search.FileCount = soulseekSearch.FileCount;
                search.LockedFileCount = soulseekSearch.LockedFileCount;
                search.ResponseCount = soulseekSearch.ResponseCount;
                search.State = soulseekSearch.State;
                search.EndedAt = DateTime.UtcNow;
                search.Responses = responses;
                SaveSearchState(search);

                return search;
            }
            finally
            {
                CancellationTokens.TryRemove(id, out _);
            }
        }

        /// <summary>
        ///     Finds a single search matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match searches.</param>
        /// <param name="includeResponses">A value indicating whether to include search responses in the result.</param>
        /// <returns>The found search, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown an expression is not supplied.</exception>
        public Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false)
        {
            if (expression == default)
            {
                throw new ArgumentException("An expression must be supplied.", nameof(expression));
            }

            using var context = ContextFactory.CreateDbContext();

            var selector = context.Searches.Where(expression);

            if (!includeResponses)
            {
                selector = selector.WithoutResponses();
            }

            return selector.FirstOrDefaultAsync();
        }

        /// <summary>
        ///     Returns a list of all completed and in-progress searches, with responses omitted.
        /// </summary>
        /// <param name="expression">An optional expression used to match searches.</param>
        /// <returns>The list of searches matching the specified expression.</returns>
        public Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null)
        {
            expression ??= s => true;
            using var context = ContextFactory.CreateDbContext();
            return context.Searches.Where(expression).WithoutResponses().ToListAsync();
        }

        /// <summary>
        ///     Cancels the search matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the search.</param>
        /// <returns>A value indicating whether the search was sucessfully cancelled.</returns>
        public bool TryCancel(Guid id)
        {
            if (CancellationTokens.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        private void UpdateSearchState(Search search, SoulseekSearch soulseekSearch)
        {
            if (CancellationTokens.ContainsKey(search.Id))
            {
                search.FileCount = soulseekSearch.FileCount;
                search.LockedFileCount = soulseekSearch.LockedFileCount;
                search.ResponseCount = soulseekSearch.ResponseCount;
                search.State = soulseekSearch.State;

                SaveSearchState(search);
            }
        }

        private void SaveSearchState(Search search)
        {
            var context = ContextFactory.CreateDbContext();
            context.Update(search);
            context.SaveChanges();
        }
    }
}