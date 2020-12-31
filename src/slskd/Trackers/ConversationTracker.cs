namespace slskd.Trackers
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using slskd.Entities;

    /// <summary>
    ///     Tracks private message conversations.
    /// </summary>
    public class ConversationTracker : IConversationTracker
    {
        /// <summary>
        ///     Tracked private message conversations.
        /// </summary>
        public ConcurrentDictionary<string, IList<PrivateMessage>> Conversations { get; } = new ConcurrentDictionary<string, IList<PrivateMessage>>();

        /// <summary>
        ///     Adds a private message conversation and appends the specified <paramref name="message"/>, or just appends the
        ///     message if the conversation exists.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        public void AddOrUpdate(string username, PrivateMessage message)
        {
            Conversations.AddOrUpdate(username, new List<PrivateMessage>() { message }, (_, messageList) =>
            {
                messageList.Add(message);
                return messageList;
            });
        }

        /// <summary>
        ///     Returns the list of private messages for the specified <paramref name="username"/>, if any exist.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="messages"></param>
        /// <returns></returns>
        public bool TryGet(string username, out IList<PrivateMessage> messages) => Conversations.TryGetValue(username, out messages);

        /// <summary>
        ///     Removes a tracked private message conversation for the specified user.
        /// </summary>
        /// <param name="username"></param>
        public void TryRemove(string username) => Conversations.TryRemove(username, out _);
    }
}