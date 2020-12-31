namespace slskd.Trackers
{
    using Soulseek;
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    ///     Tracks active searches.
    /// </summary>
    public class SearchTracker : ISearchTracker
    {
        /// <summary>
        ///     Gets active searches.
        /// </summary>
        public ConcurrentDictionary<Guid, Search> Searches { get; private set; } =
            new ConcurrentDictionary<Guid, Search>();

        /// <summary>
        ///     Adds or updates a tracked search.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="args"></param>
        public void AddOrUpdate(Guid id, SearchEventArgs args)
        {
            Searches.AddOrUpdate(id, args.Search, (token, search) => args.Search);
        }

        /// <summary>
        ///     Removes all tracked searches.
        /// </summary>
        public void Clear()
        {
            Searches.Clear();
        }

        /// <summary>
        ///     Removes a tracked search.
        /// </summary>
        /// <param name="id"></param>
        public void TryRemove(Guid id)
        {
            Searches.TryRemove(id, out _);
        }
    }
}