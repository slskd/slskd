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
    using System.Threading;
    using System.Threading.Tasks;
    using Soulseek;

    public class SearchService : ISearchService
    {
        public SearchService(SearchDbContext context, ISoulseekClient client)
        {
            Context = context;
            Client = client;

            Client.SearchStateChanged += Client_SearchStateChanged;
            Client.SearchRequestReceived += Client_SearchRequestReceived;
        }

        private void Client_SearchRequestReceived(object sender, SearchRequestEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Client_SearchStateChanged(object sender, SearchStateChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private SearchDbContext Context { get; }
        private ISoulseekClient Client { get; }
        private ConcurrentDictionary<Guid, int> Lookup { get; }
            = new ConcurrentDictionary<Guid, int>();
        private ConcurrentDictionary<int, CancellationTokenSource> InProgress { get; }
            = new ConcurrentDictionary<int, CancellationTokenSource>();

        public async Task<Guid> BeginAsync(SearchQuery query, SearchScope scope = null, SearchOptions options = null)
        {
            scope = scope ?? SearchScope.Network;
            var token = Client.GetNextToken();
            options = options ?? new SearchOptions();
            var cts = new CancellationTokenSource();
            var id = Guid.NewGuid();

            var record = new Search()
            {
                SearchText = query.SearchText,
                Token = token,
                Id = id,
                State = SearchStates.Requested,
            };

            Context.Add(record);
            await Context.SaveChangesAsync();

            InProgress.TryAdd(token, cts);
            Lookup.TryAdd(id, token);

            _ = Client.SearchAsync(query, scope, token, options, cancellationToken: cts.Token).ContinueWith(async task =>
            {
                try
                {
                    var result = await task;

                    // todo: update db with status and end time
                }
                finally
                {
                    InProgress.TryRemove(token, out _);
                    Lookup.TryRemove(id, out _);
                }
            });

            return id;
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

        public Task<Search> FindAsync(Guid id)
        {
            return Context.Searches.FindAsync(id).AsTask();
        }
    }
}
