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

namespace slskd.Search.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Soulseek;

    /// <summary>
    ///     A search request.
    /// </summary>
    public class SearchRequest : IValidatableObject
    {
        /// <summary>
        ///     Gets or sets the unique search identifier.
        /// </summary>
        public Guid? Id { get; set; }

        /// <summary>
        ///     Gets or sets the maximum number of file results to accept before the search is considered complete. (Default = 10,000).
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? FileLimit { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether responses are to be filtered. (Default = true).
        /// </summary>
        public bool? FilterResponses { get; set; }

        /// <summary>
        ///     Gets or sets the maximum queue depth a peer may have in order for a response to be processed. (Default = 1000000).
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? MaximumPeerQueueLength { get; set; }

        /// <summary>
        ///     Gets or sets the minimum upload speed a peer must have in order for a response to be processed. (Default = 0).
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? MinimumPeerUploadSpeed { get; set; }

        /// <summary>
        ///     Gets or sets the minimum number of files a response must contain in order to be processed. (Default = 1).
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? MinimumResponseFileCount { get; set; }

        /// <summary>
        ///     Gets or sets the maximum number of search results to accept before the search is considered complete. (Default = 100).
        /// </summary>
        [Range(0, int.MaxValue)]
        public int? ResponseLimit { get; set; }

        /// <summary>
        ///     Gets or sets the search text.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        ///     Gets or sets the search timeout value, in seconds, used to determine when the search is complete. (Default = 15).
        /// </summary>
        /// <remarks>The timeout duration is from the time of the last response.</remarks>
        [Range(5, int.MaxValue)]
        public int? SearchTimeout { get; set; }

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
            Action<(SearchStates PreviousState, Search Search)> stateChanged = null,
            Action<(Search Search, SearchResponse Response)> responseReceived = null)
        {
            var def = new SearchOptions();

            return new SearchOptions(
                searchTimeout: SearchTimeout ?? def.SearchTimeout,
                responseLimit: ResponseLimit ?? def.ResponseLimit,
                fileLimit: FileLimit ?? def.FileLimit,
                filterResponses: FilterResponses ?? def.FilterResponses,
                minimumResponseFileCount: MinimumResponseFileCount ?? def.MinimumResponseFileCount,
                maximumPeerQueueLength: MaximumPeerQueueLength ?? def.MaximumPeerQueueLength,
                minimumPeerUploadSpeed: MinimumPeerUploadSpeed ?? def.MinimumPeerUploadSpeed,
                responseFilter: responseFilter,
                fileFilter: fileFilter,
                responseReceived: responseReceived,
                stateChanged: stateChanged);
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // todo: adjust this when additional search inputs are made available
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                yield return new ValidationResult("SearchText can not be null, empty, or consist of only whitespace");
            }
        }
    }
}