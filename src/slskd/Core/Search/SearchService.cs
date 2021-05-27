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
    using System.Linq.Expressions;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Soulseek;

    public class SearchService : ISearchService
    {
        public SearchService(IDbContextFactory<SearchDbContext> contextFactory, ISoulseekClient client)
        {
            ContextFactory = contextFactory;
            Client = client;
        }

        private IDbContextFactory<SearchDbContext> ContextFactory { get; }
        private ISoulseekClient Client { get; }
        private ConcurrentDictionary<Guid, int> Lookup { get; }
            = new ConcurrentDictionary<Guid, int>();
        private ConcurrentDictionary<int, CancellationTokenSource> InProgress { get; }
            = new ConcurrentDictionary<int, CancellationTokenSource>();

        public async Task<(Guid Id, Task<Soulseek.Search> Completed)> BeginAsync(SearchQuery query, SearchScope scope = null, SearchOptions options = null, Guid? id = null)
        {
            scope = scope ?? SearchScope.Network;
            var token = Client.GetNextToken();
            id = id ?? Guid.NewGuid();

            var cts = new CancellationTokenSource();

            var record = new Search()
            {
                SearchText = query.SearchText,
                Token = token,
                Id = id.Value,
                State = SearchStates.Requested,
                StartedAt = DateTime.UtcNow,
            };

            options = options ?? new SearchOptions(stateChanged: (args) =>
            {
                record.State = args.Search.State;
                ContextFactory.CreateDbContext().Update(record);
            });

            using var context = ContextFactory.CreateDbContext();
            context.Add(record);
            context.SaveChanges();

            InProgress.TryAdd(token, cts);
            Lookup.TryAdd(id.Value, token);

            var task = Client.SearchAsync(query, responseReceived: (response) => AddResponse(id.Value, response), scope, token, options, cancellationToken: cts.Token);

            _ = task.ContinueWith(async task =>
            {
                Console.WriteLine($"Continued...");

                try
                {
                    var search = await task;
                    record.State = search.State;
                    record.EndedAt = DateTime.UtcNow;

                    Console.WriteLine($"Saving with state {record.State}");

                    using var context = ContextFactory.CreateDbContext();
                    context.Update(record);
                    context.SaveChanges();

                    Console.WriteLine($"Saved");
                }
                finally
                {
                    InProgress.TryRemove(token, out _);
                    Lookup.TryRemove(id.Value, out _);
                }
            });

            return (id.Value, task);
        }

        public bool TryCancel(Guid id)
        {
            if (Lookup.TryGetValue(id, out var token) && InProgress.TryGetValue(token, out var cts))
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
                .Include(s => s.Responses)
                .ThenInclude(r => r.Files)
                .FirstOrDefaultAsync(expression);

            //if (search == default)
            //{
            //    return default;
            //}

            //if (includeResponses)
            //{
            //    await context.Entry(search).Collection(s => s.Responses).LoadAsync();
            //}

            return search;
        }

        private void AddResponse(Guid searchId, Soulseek.SearchResponse soulseekResponse)
        {
            using var context = ContextFactory.CreateDbContext();
            context.Add(SearchResponse.FromSoulseekSearchResponse(soulseekResponse, searchId));
            context.SaveChanges();
        }
    }
}
