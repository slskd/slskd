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

namespace slskd.Search
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Soulseek;

    public class SearchService : ISearchService
    {
        public SearchService(
            IDbContextFactory<SearchDbContext> contextFactory,
            ILogger<SearchService> log,
            ISoulseekClient client)
        {
            ContextFactory = contextFactory;
            Log = log;
            Client = client;
        }

        private IDbContextFactory<SearchDbContext> ContextFactory { get; }
        private ILogger<SearchService> Log { get; set; }
        private ISoulseekClient Client { get; }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; }
            = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        public async Task<Search> SearchAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null)
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
            options = options.WithActions(stateChanged: (args) => UpdateState(search, args.Search), (args) => UpdateState(search, args.Search));

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

            search.FileCount = soulseekSearch.FileCount;
            search.LockedFileCount = soulseekSearch.LockedFileCount;
            search.ResponseCount = soulseekSearch.ResponseCount;
            search.State = soulseekSearch.State;
            search.Responses = responses;

            return search;
        }

        public bool TryCancel(Guid id)
        {
            if (CancellationTokens.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        public async Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = true)
        {
            using var context = ContextFactory.CreateDbContext();
            var search = await context
                .Searches
                .FirstOrDefaultAsync(expression);

            if (!includeResponses)
            {

            }
            return search;
        }

        private void UpdateState(Search search, Soulseek.Search soulseekSearch)
        {
            _ = Task.Run(async () =>
            {
                search.FileCount = soulseekSearch.FileCount;
                search.LockedFileCount = soulseekSearch.LockedFileCount;
                search.ResponseCount = soulseekSearch.ResponseCount;
                search.State = soulseekSearch.State;

                var context = ContextFactory.CreateDbContext();

                context.Update(search);
                await context.SaveChangesAsync();
            }).ContinueWith(task =>
                Log.LogError(task.Exception, "Failed to update state of {SearchId}", search.Id),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
