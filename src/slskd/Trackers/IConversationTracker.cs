namespace slskd.Trackers
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using slskd.Entities;

    /// <summary>
    ///     Tracks private message conversations.
    /// </summary>
    public interface IConversationTracker
    {
        /// <summary>
        ///     Tracked private message conversations.
        /// </summary>
        ConcurrentDictionary<string, IList<PrivateMessage>> Conversations { get; }

        /// <summary>
        ///     Adds a private message conversation and appends the specified <paramref name="message"/>, or just appends the
        ///     message if the conversation exists.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        void AddOrUpdate(string username, PrivateMessage message);

        /// <summary>
        ///     Returns the list of private messages for the specified <paramref name="username"/>, if any exist.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="messages"></param>
        /// <returns></returns>
        bool TryGet(string username, out IList<PrivateMessage> messages);

        /// <summary>
        ///     Removes a tracked private message conversation for the specified user.
        /// </summary>
        /// <param name="username"></param>
        void TryRemove(string username);
    }
}