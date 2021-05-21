// <copyright file="SearchRequest.cs" company="slskd Team">
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

namespace slskd.API.DTO
{
    using System;
    using Soulseek;

    /// <summary>
    ///     A search request.
    /// </summary>
    public class SearchRequest
    {
        /// <summary>
        ///     Gets or sets the unique search identifier.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        ///     Gets or sets the maximum number of file results to accept before the search is considered complete. (Default = 10,000).
        /// </summary>
        public int? FileLimit { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether responses are to be filtered. (Default = true).
        /// </summary>
        public bool? FilterResponses { get; set; }

        /// <summary>
        ///     Gets or sets the maximum queue depth a peer may have in order for a response to be processed. (Default = 1000000).
        /// </summary>
        public int? MaximumPeerQueueLength { get; set; }

        /// <summary>
        ///     Gets or sets the minimum number of free upload slots a peer must have in order for a response to be processed.
        ///     (Default = 0).
        /// </summary>
        public int? MinimumPeerFreeUploadSlots { get; set; }

        /// <summary>
        ///     Gets or sets the minimum upload speed a peer must have in order for a response to be processed. (Default = 0).
        /// </summary>
        public int? MinimumPeerUploadSpeed { get; set; }

        /// <summary>
        ///     Gets or sets the minimum number of files a response must contain in order to be processed. (Default = 1).
        /// </summary>
        public int? MinimumResponseFileCount { get; set; }

        /// <summary>
        ///     Gets or sets the maximum number of search results to accept before the search is considered complete. (Default = 100).
        /// </summary>
        public int? ResponseLimit { get; set; }

        /// <summary>
        ///     Gets or sets the search text.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        ///     Gets or sets the search timeout value, in seconds, used to determine when the search is complete. (Default = 15).
        /// </summary>
        /// <remarks>The timeout duration is from the time of the last response.</remarks>
        public int? SearchTimeout { get; set; }

        /// <summary>
        ///     Gets or sets the search token.
        /// </summary>
        public int? Token { get; set; }

        /// <summary>
        ///     Maps to a new instance of <see cref="SearchOptions"/>.
        /// </summary>
        /// <param name="responseFilter"></param>
        /// <param name="fileFilter"></param>
        /// <param name="stateChanged"></param>
        /// <param name="responseReceived"></param>
        /// <returns></returns>
        public SearchOptions ToSearchOptions(
            Func<SearchResponse, bool> responseFilter = null,
            Func<File, bool> fileFilter = null,
            Action<SearchStateChangedEventArgs> stateChanged = null,
            Action<SearchResponseReceivedEventArgs> responseReceived = null)
        {
            var def = new SearchOptions();

            return new SearchOptions(
                searchTimeout: SearchTimeout ?? def.SearchTimeout,
                responseLimit: ResponseLimit ?? def.ResponseLimit,
                fileLimit: FileLimit ?? def.FileLimit,
                filterResponses: FilterResponses ?? def.FilterResponses,
                minimumResponseFileCount: MinimumResponseFileCount ?? def.MinimumResponseFileCount,
                minimumPeerFreeUploadSlots: MinimumPeerFreeUploadSlots ?? def.MinimumPeerFreeUploadSlots,
                maximumPeerQueueLength: MaximumPeerQueueLength ?? def.MaximumPeerQueueLength,
                minimumPeerUploadSpeed: MinimumPeerUploadSpeed ?? def.MinimumPeerUploadSpeed,
                responseFilter: responseFilter,
                fileFilter: fileFilter,
                responseReceived: responseReceived,
                stateChanged: stateChanged);
        }
    }
}