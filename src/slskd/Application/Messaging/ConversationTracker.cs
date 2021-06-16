// <copyright file="ConversationTracker.cs" company="slskd Team">
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

namespace slskd.Messaging
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

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