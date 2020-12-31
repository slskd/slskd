namespace slskd.Trackers
{
    using Soulseek;
    using System.Collections.Concurrent;

    /// <summary>
    ///     Tracks browse operations.
    /// </summary>
    public class BrowseTracker : IBrowseTracker
    {
        /// <summary>
        ///     Tracked browse operations.
        /// </summary>
        public ConcurrentDictionary<string, BrowseProgressUpdatedEventArgs> Browses { get; } = new ConcurrentDictionary<string, BrowseProgressUpdatedEventArgs>();

        /// <summary>
        ///     Adds or updates a tracked browse operation.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="progress"></param>
        public void AddOrUpdate(string username, BrowseProgressUpdatedEventArgs progress)
            => Browses.AddOrUpdate(username, progress, (user, oldprogress) => progress);

        /// <summary>
        ///     Removes a tracked browse operation for the specified user.
        /// </summary>
        /// <param name="username"></param>
        public void TryRemove(string username)
            => Browses.TryRemove(username, out _);

        /// <summary>
        ///     Gets the browse progress for the specified user.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public bool TryGet(string username, out BrowseProgressUpdatedEventArgs progress)
            => Browses.TryGetValue(username, out progress);
    }
}
