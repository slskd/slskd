using Soulseek;
using System;

namespace slskd.DTO
{
    /// <summary>
    ///     A search request.
    /// </summary>
    public class SearchRequest
    {
        /// <summary>
        ///     Gets or sets the unique search identifier
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