namespace slskd.Trackers
{
    using Soulseek;
    using System.Collections.Concurrent;

    /// <summary>
    ///     Tracks browse operations.
    /// </summary>
    public interface IBrowseTracker
    {
        /// <summary>
        ///     Tracked browse operations.
        /// </summary>
        ConcurrentDictionary<string, BrowseProgressUpdatedEventArgs> Browses { get; }

        /// <summary>
        ///     Adds or updates a tracked browse operation.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="progress"></param>
        void AddOrUpdate(string username, BrowseProgressUpdatedEventArgs progress);

        /// <summary>
        ///     Removes a tracked browse operation for the specified user.
        /// </summary>
        /// <param name="username"></param>
        void TryRemove(string username);

        /// <summary>
        ///     Gets the browse progress for the specified user.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        bool TryGet(string username, out BrowseProgressUpdatedEventArgs progress);
    }
}
