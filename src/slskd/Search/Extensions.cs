// <copyright file="Extensions.cs" company="slskd Team">
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
    using System.Linq;
    using Soulseek;

    public static class Extensions
    {
        /// <summary>
        ///     Returns a copy of the specified <paramref name="options"/> with the specified actions bound, while retaining the existing actions.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="stateChanged"></param>
        /// <param name="responseReceived"></param>
        /// <returns></returns>
        public static SearchOptions WithActions(
            this SearchOptions options,
            Action<SearchStateChangedEventArgs> stateChanged = null,
            Action<SearchResponseReceivedEventArgs> responseReceived = null)
        {
            stateChanged ??= (args) => { };
            responseReceived ??= (args) => { };

            return new SearchOptions(
                options.SearchTimeout,
                options.ResponseLimit,
                options.FilterResponses,
                options.MinimumResponseFileCount,
                options.MinimumPeerFreeUploadSlots,
                options.MaximumPeerQueueLength,
                options.MinimumPeerUploadSpeed,
                options.FileLimit,
                options.RemoveSingleCharacterSearchTerms,
                options.ResponseFilter,
                options.FileFilter,
                stateChanged: (args) =>
                {
                    if (options.StateChanged != null)
                    {
                        options.StateChanged(args);
                    }

                    stateChanged(args);
                },
                responseReceived: (args) =>
                {
                    if (options.ResponseReceived != null)
                    {
                        options.ResponseReceived(args);
                    }

                    responseReceived(args);
                });
        }

        /// <summary>
        ///     Returns a copy of the specified <paramref name="options"/> with the specified filter delegates overridden.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="responseFilter"></param>
        /// <param name="fileFilter"></param>
        /// <returns></returns>
        public static SearchOptions WithFilters(
            this SearchOptions options,
            Func<SearchResponse, bool> responseFilter = null,
            Func<Soulseek.File, bool> fileFilter = null)
        {
            return new SearchOptions(
                options.SearchTimeout,
                options.ResponseLimit,
                filterResponses: options.FilterResponses || responseFilter != null || fileFilter != null,
                options.MinimumResponseFileCount,
                options.MinimumPeerFreeUploadSlots,
                options.MaximumPeerQueueLength,
                options.MinimumPeerUploadSpeed,
                options.FileLimit,
                options.RemoveSingleCharacterSearchTerms,
                responseFilter,
                fileFilter,
                options.StateChanged,
                options.ResponseReceived);
        }

        /// <summary>
        ///     Creates a projection over the specified <paramref name="query"/> which omits responses.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public static IQueryable<Search> WithoutResponses(this IQueryable<Search> query)
        {
            return query.Select(s => new Search()
            {
                Id = s.Id,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                FileCount = s.FileCount,
                LockedFileCount = s.LockedFileCount,
                ResponseCount = s.ResponseCount,
                SearchText = s.SearchText,
                State = s.State,
                Token = s.Token,
            });
        }

        public static Search WithSoulseekSearch(this Search search, Soulseek.Search s)
        {
            return new Search()
            {
                Id = search.Id,
                StartedAt = search.StartedAt,
                EndedAt = search.EndedAt,
                FileCount = s.FileCount,
                LockedFileCount = s.LockedFileCount,
                ResponseCount = s.ResponseCount,
                SearchText = search.SearchText,
                State = s.State,
                Token = search.Token,
            };
        }
    }
}
