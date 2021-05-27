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

        private ConcurrentDictionary<Guid, CancellationTokenSource> InProgress { get; }
            = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        public async Task<(Guid Id, Task<Search> Completed)> BeginAsync(SearchQuery query, SearchScope scope = null, SearchOptions options = null, Guid? id = null)
        {
            scope ??= SearchScope.Network;
            options ??= new SearchOptions();
            id ??= Guid.NewGuid();

            var token = Client.GetNextToken();
            var cancellationTokenSource = new CancellationTokenSource();

            var record = new Search()
            {
                SearchText = query.SearchText,
                Token = token,
                Id = id.Value,
                State = SearchStates.Requested,
                StartedAt = DateTime.UtcNow,
            };

            options = options.WithActions(stateChanged: (args) => UpdateState(record, args), (args) => UpdateState(record, args));

            try
            {
                using var context = ContextFactory.CreateDbContext();
                context.Add(record);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Failed to save search: {Message}", ex.Message);
            }

            InProgress.TryAdd(id.Value, cancellationTokenSource);

            var task = Task.Run(async () =>
            {
                var completedSearch = await Client.SearchAsync(
                    query,
                    responseReceived: (response) => AddResponse(id.Value, response),
                    scope,
                    token,
                    options,
                    cancellationToken: cancellationTokenSource.Token);

                return Search.FromSoulseekSearch(completedSearch);
            });

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
                    await context.SaveChangesAsync();

                    Console.WriteLine($"Saved");
                }
                finally
                {
                    InProgress.TryRemove(id.Value, out _);
                }
            });

            return (id.Value, task);
        }

        public bool TryCancel(Guid id)
        {
            if (InProgress.TryGetValue(id, out var cts))
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
                .ThenInclude(r => r.Files).AsSplitQuery()
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

        private void AddResponse(Guid searchId, Soulseek.SearchResponse response)
        {
            _ = Task.Run(async () =>
            {
                using var context = ContextFactory.CreateDbContext();
                context.Add(SearchResponse.FromSoulseekSearchResponse(response, searchId));
                await context.SaveChangesAsync();
            }).ContinueWith(task =>
                Log.LogError(task.Exception, "Failed to save search response for {SearchId} from {Username}", searchId, response.Username),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private void UpdateState(Search search, SearchEventArgs args)
        {
            _ = Task.Run(async () =>
            {
                var context = ContextFactory.CreateDbContext();
                context.Update(search.WithSoulseekSearch(args.Search));
                await context.SaveChangesAsync();
            }).ContinueWith(task =>
                Log.LogError(task.Exception, "Failed to update state of {SearchId}", search.Id),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
