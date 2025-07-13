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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using slskd.Search.API;
    using Soulseek;
    using SearchOptions = Soulseek.SearchOptions;
    using SearchQuery = Soulseek.SearchQuery;
    using SearchScope = Soulseek.SearchScope;
    using SearchStates = Soulseek.SearchStates;

    /// <summary>
    ///     Handles the lifecycle and persistence of searches.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        ///     Deletes the specified search.
        /// </summary>
        /// <param name="search">The search to delete.</param>
        /// <returns>The operation context.</returns>
        Task DeleteAsync(Search search);

        /// <summary>
        ///     Finds a single search matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match searches.</param>
        /// <param name="includeResponses">A value indicating whether to include search responses in the result.</param>
        /// <returns>The found search, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when an expression is not supplied.</exception>
        Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false);

        /// <summary>
        ///     Returns a list of all completed and in-progress searches, with responses omitted, matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match searches.</param>
        /// <returns>The list of searches matching the specified expression, or all searches if no expression is specified.</returns>
        Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null);

        /// <summary>
        ///     Updates the specified <paramref name="search"/>.
        /// </summary>
        /// <remark>
        ///     Round-trips the database; use accordingly.
        /// </remark>
        /// <param name="search">The search to update.</param>
        void Update(Search search);

        /// <summary>
        ///     Performs a search for the specified <paramref name="query"/> and <paramref name="scope"/>.
        /// </summary>
        /// <param name="id">A unique identifier for the search.</param>
        /// <param name="query">The search query.</param>
        /// <param name="scope">The search scope.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The completed search.</returns>
        Task<Search> StartAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null);

        /// <summary>
        ///     Cancels the search matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the search.</param>
        /// <returns>A value indicating whether the search was successfully cancelled.</returns>
        bool TryCancel(Guid id);

        /// <summary>
        ///     Removes <see cref="SearchStates.Completed"/> searches older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which records are eligible for pruning, in minutes.</param>
        /// <returns>The number of pruned records.</returns>
        Task<int> PruneAsync(int age);
    }

    /// <summary>
    ///     Handles the lifecycle and persistence of searches.
    /// </summary>
    public class SearchService : ISearchService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchService"/> class.
        /// </summary>
        /// <param name="searchHub"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="soulseekClient"></param>
        /// <param name="contextFactory">The database context to use.</param>
        public SearchService(
            IHubContext<SearchHub> searchHub,
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient,
            IDbContextFactory<SearchDbContext> contextFactory)
        {
            SearchHub = searchHub;
            OptionsMonitor = optionsMonitor;
            Client = soulseekClient;
            ContextFactory = contextFactory;
        }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; }
            = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        private ISoulseekClient Client { get; }
        private IDbContextFactory<SearchDbContext> ContextFactory { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<Application>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHubContext<SearchHub> SearchHub { get; set; }

        /// <summary>
        ///     Deletes the specified search.
        /// </summary>
        /// <param name="search">The search to delete.</param>
        /// <returns>The operation context.</returns>
        public Task DeleteAsync(Search search)
        {
            if (search == default)
            {
                throw new ArgumentNullException(nameof(search));
            }

            return DoDeleteAsync(search);

            async Task DoDeleteAsync(Search search)
            {
                using var context = ContextFactory.CreateDbContext();
                context.Searches.Remove(search);
                context.SaveChanges();

                await SearchHub.BroadcastDeleteAsync(search);
            }
        }

        /// <summary>
        ///     Finds a single search matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match searches.</param>
        /// <param name="includeResponses">A value indicating whether to include search responses in the result.</param>
        /// <returns>The found search, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when an expression is not supplied.</exception>
        public Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false)
        {
            if (expression == default)
            {
                throw new ArgumentException("An expression must be supplied.", nameof(expression));
            }

            using var context = ContextFactory.CreateDbContext();

            var selector = context.Searches
                .AsNoTracking()
                .Where(expression);

            if (!includeResponses)
            {
                selector = selector.WithoutResponses();
            }

            return selector.FirstOrDefaultAsync();
        }

        /// <summary>
        ///     Returns a list of all completed and in-progress searches, with responses omitted, matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match searches.</param>
        /// <returns>The list of searches matching the specified expression, or all searches if no expression is specified.</returns>
        public Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null)
        {
            expression ??= s => true;
            using var context = ContextFactory.CreateDbContext();

            return context.Searches
                .AsNoTracking()
                .Where(expression)
                .WithoutResponses()
                .ToListAsync();
        }

        /// <summary>
        ///     Updates the specified <paramref name="search"/>.
        /// </summary>
        /// <param name="search">The search to update.</param>
        public void Update(Search search)
        {
            using var context = ContextFactory.CreateDbContext();
            context.Update(search);
            context.SaveChanges();
        }

        /// <summary>
        ///     Performs a search for the specified <paramref name="query"/> and <paramref name="scope"/>.
        /// </summary>
        /// <param name="id">A unique identifier for the search.</param>
        /// <param name="query">The search query.</param>
        /// <param name="scope">The search scope.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The completed search.</returns>
        public async Task<Search> StartAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null)
        {
            var token = Client.GetNextToken();

            var cancellationTokenSource = new CancellationTokenSource();
            CancellationTokens.TryAdd(id, cancellationTokenSource);

            var rateLimiter = new RateLimiter(250);

            // initialize the search record, save it to the database, and broadcast the creation
            // we do this so the UI has some feedback to show to the user that we've gotten their request
            var search = new Search()
            {
                SearchText = query.SearchText,
                Token = token,
                Id = id,
                State = SearchStates.Requested,
                StartedAt = DateTime.UtcNow,
            };

            bool searchCreated = false;
            bool searchBroadcasted = false;

            try
            {
                using var context = ContextFactory.CreateDbContext();
                context.Add(search);
                context.SaveChanges();

                searchCreated = true;

                await SearchHub.BroadcastCreateAsync(search);

                searchBroadcasted = true;

                // initialize the list of responses that we'll use to accumulate them
                // populated by the responseHandler we pass to SearchAsync
                List<SearchResponse> responses = new();
                List<(SearchStates State, DateTime Timestamp, string Source)> stateTransitions = new();

                options ??= new SearchOptions();
                options = options.WithActions(
                    stateChanged: (args) =>
                    {
                        // this logic is executed by Soulseek.NET upon each transition of the searcn, including the final
                        // transition to Completed. if for some reason something goes wrong, we won't see the final Completed
                        // transition, we should instead see the task below throw, in which case it will become
                        // faulted and we'll set the Completed flag manually in the catch block
                        search = search.WithSoulseekSearch(args.Search);
                        Update(search);

                        stateTransitions.Add((args.Search.State, DateTime.UtcNow, "StateChangedDelegate"));

                        SearchHub.BroadcastUpdateAsync(search);

                        Log.Debug("Search for '{Query}' state changed: {State} (id: {Id})", query, search.State, id);
                    },
                    responseReceived: (args) => rateLimiter.Invoke(() =>
                    {
                        // note: this is rate limited, but has the potential to update the database every 250ms (or whatever the
                        // interval is set to) for the duration of the search. any issues with disk i/o or performance while searches
                        // are running should investigate this as a cause
                        search.ResponseCount = args.Search.ResponseCount;
                        search.FileCount = args.Search.FileCount;
                        search.LockedFileCount = args.Search.LockedFileCount;

                        Update(search);

                        // note that we're not actually doing anything with the response here, that's happening in the
                        // response handler. we're only updating counts here.
                        SearchHub.BroadcastUpdateAsync(search);
                    }));

                // initiate the search. this can throw at invocation if there's a problem with
                // the client state (e.g. disconnected) or a problem with the search (e.g. no terms)
                var soulseekSearchTask = Client.SearchAsync(
                    query,
                    responseHandler: (response) => responses.Add(response),
                    scope,
                    token,
                    options,
                    cancellationToken: cancellationTokenSource.Token);

                // search looks ok so far; let the rest of the logic run asynchronously
                // on a background thread. this logic needs to clean up after itself and
                // update the search record to accurately reflect the final state
                _ = Task.Run(async () =>
                {
                    try
                    {
                        try
                        {
                            var soulseekSearch = await soulseekSearchTask;
                            stateTransitions.Add((SearchStates.None, DateTime.UtcNow, "Callsite")); // bogus entry to try and capture when it returned

                            search = search.WithSoulseekSearch(soulseekSearch);

                            Log.Debug("Search for '{Query}' ended normally (id: {Id})", query, id);
                        }
                        catch (Exception ex)
                        {
                            // OperationCanceledException might be thrown somewhere deeper, and we don't want that to count.
                            // a search that was actually cancelled by the user will meet the criteria below.
                            if (ex is OperationCanceledException && cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                Log.Information("Search for '{Query}' was cancelled");
                                search.State = SearchStates.Completed | SearchStates.Cancelled;
                            }
                            else
                            {
                                Log.Error(ex, "Failed to execute search for '{Query}' (id: {Id}): {Message}", query, id, ex.Message);
                                search.State = SearchStates.Completed | SearchStates.Errored;
                            }
                        }

                        // todo: remove this once we're sure the problem is fixed
                        if (!search.State.HasFlag(SearchStates.Completed))
                        {
                            Log.Warning("Gotcha! Search for '{Search}' concluded without the Completed flag. Time is now {Timestamp}. States: {States}", query.SearchText, DateTime.UtcNow, stateTransitions);
                            search.State |= SearchStates.Completed;
                        }

                        search.EndedAt = DateTime.UtcNow;
                        search.Responses = responses.Select(r => Response.FromSoulseekSearchResponse(r));

                        Update(search);

                        // zero responses before broadcasting, as we don't want to blast this
                        // data out over the SignalR socket
                        await SearchHub.BroadcastUpdateAsync(search with { Responses = [] });

                        Log.Debug("Search for '{Query}' finalized (id: {Id}): {Search}", query, id, search with { Responses = [] });
                    }
                    catch (Exception ex)
                    {
                        // record may be left 'hanging' and will need to be cleaned up at the next boot; we tried to update but failed
                        Log.Error(ex, "Failed to finalize search for '{Query}' (id: {Id}): {Message}", query, id, ex.Message);
                        throw;
                    }
                    finally
                    {
                        rateLimiter.Dispose();
                        CancellationTokens.TryRemove(id, out _);
                    }
                }, cancellationToken: cancellationTokenSource.Token);

                // broadcast and return the _newly created_ search; it will continue to be updated in the background
                await SearchHub.BroadcastUpdateAsync(search);
                return search;
            }
            catch (Exception ex)
            {
                // we'll end up here if the initial call throws for an ArgumentException, InvalidOperationException if
                // the app isn't connected, and a few other straightforward issues that arise before even requesting the search
                Log.Error(ex, "Failed to execute search {Search}: {Message}", new { query, scope, options }, ex.Message);

                // selectively 'undo' whatever actions we were able to take successfully
                if (searchCreated)
                {
                    search.State = SearchStates.Completed | SearchStates.Errored;
                    search.EndedAt = search.StartedAt;
                    Update(search);

                    if (searchBroadcasted)
                    {
                        await SearchHub.BroadcastUpdateAsync(search with { Responses = [] });
                    }
                }

                throw;
            }
        }

        /// <summary>
        ///     Cancels the search matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the search.</param>
        /// <returns>A value indicating whether the search was successfully cancelled.</returns>
        public bool TryCancel(Guid id)
        {
            if (CancellationTokens.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Removes <see cref="SearchStates.Completed"/> searches older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which searches are eligible for pruning, in minutes.</param>
        /// <returns>The number of pruned records.</returns>
        public async Task<int> PruneAsync(int age)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                var cutoffDateTime = DateTime.UtcNow.AddMinutes(-age);

                // unlike other pruning operations, we don't care about state, since there's a 60 minute minimum
                // and searches are guaranteed to be at least 60 minutes old by the time they can be pruned, they will
                // be completed unless someone applied some rather dumb settings
                var expired = context.Searches
                    .Where(s => s.EndedAt.HasValue && s.EndedAt.Value < cutoffDateTime)
                    .WithoutResponses()
                    .ToList();

                // defer the deletion to DeleteAsync() so that SignalR broadcasting works properly and the UI
                // is updated in real time
                foreach (var search in expired)
                {
                    await DeleteAsync(search);
                }

                return expired.Count;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to prune searches: {Message}", ex.Message);
                throw;
            }
        }
    }
}