// <copyright file="MessagingService.cs" company="slskd Team">
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
    /// <summary>
    ///     Manages private and room messages.
    /// </summary>
    public interface IMessagingService
    {
        /// <summary>
        ///     Gets the <see cref="ConversationService"/>.
        /// </summary>
        IConversationService Conversations { get; }
    }

    /// <summary>
    ///     Manages private and room messages.
    /// </summary>
    public class MessagingService : IMessagingService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MessagingService"/> class.
        /// </summary>
        /// <param name="conversations"></param>
        public MessagingService(IConversationService conversations)
        {
            Conversations = conversations;
        }

        /// <summary>
        ///     Gets the <see cref="ConversationService"/>.
        /// </summary>
        public IConversationService Conversations { get; }
    }
}