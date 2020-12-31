namespace slskd.Trackers
{
    using Soulseek;
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    ///     Tracks active searches.
    /// </summary>
    public interface ISearchTracker
    {
        /// <summary>
        ///     Gets active searches.
        /// </summary>
        ConcurrentDictionary<Guid, Search> Searches { get; }

        /// <summary>
        ///     Adds or updates a tracked search.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="args"></param>
        void AddOrUpdate(Guid id, SearchEventArgs args);

        /// <summary>
        ///     Removes all tracked searches.
        /// </summary>
        void Clear();

        /// <summary>
        ///     Removes a tracked search.
        /// </summary>
        /// <param name="id"></param>
        void TryRemove(Guid id);
    }
}