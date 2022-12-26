// <copyright file="ConversationService.cs" company="slskd Team">
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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Manages private messages.
    /// </summary>
    public interface IConversationService
    {
        /// <summary>
        ///     Acknowledges all unacknowledged <see cref="PrivateMessage"/> records from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        Task AcknowledgeAsync(string username);

        /// <summary>
        ///     Acknowledges the <see cref="PrivateMessage"/> record associated with the specified <paramref name="username"/> and <paramref name="id"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="id">The ID of the message.</param>
        /// <returns>The operation context.</returns>
        Task AcknowledgeMessageAsync(string username, int id);

        /// <summary>
        ///     Creates a new, or activates an existing, conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        Task CreateAsync(string username);

        /// <summary>
        ///     Returns the <see cref="Conversation"/> matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An expression used to locate the conversation.</param>
        /// <returns>The operation context, including the located conversation, if one was found.</returns>
        Task<Conversation> FindAsync(Expression<Func<Conversation, bool>> expression);

        /// <summary>
        ///     Returns the list of all <see cref="Conversation"/> records matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to locate conversations.</param>
        /// <returns>The operation context, including the list of found conversations.</returns>
        Task<IEnumerable<Conversation>> ListAsync(Expression<Func<Conversation, bool>> expression = null);

        /// <summary>
        ///     Handles the receipt of an inbound <see cref="PrivateMessage"/>.
        /// </summary>
        /// <param name="username">The username associated with the message.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        Task HandleMessageAsync(string username, PrivateMessage message);

        /// <summary>
        ///     Removes (marks inactive) the conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        Task RemoveAsync(string username);

        /// <summary>
        ///     Sends the specified <paramref name="message"/> to the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the recipient.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        Task SendMessageAsync(string username, string message);
    }

    public class ConversationService : IConversationService
    {
        public ConversationService(
            ISoulseekClient soulseekClient,
            IDbContextFactory<MessagingDbContext> contextFactory)
        {
            SoulseekClient = soulseekClient;
            ContextFactory = contextFactory;
        }

        private IDbContextFactory<MessagingDbContext> ContextFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ConversationService>();
        private ISoulseekClient SoulseekClient { get; }

        /// <summary>
        ///     Acknowledges all unacknowledged <see cref="PrivateMessage"/> records from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        public async Task AcknowledgeAsync(string username)
        {
            using var context = ContextFactory.CreateDbContext();

            var unacked = context.PrivateMessages
                .Where(m => m.Username == username && !m.Acknowledged)
                .Select(m => AcknowledgeMessageAsync(username, m.Id));

            await Task.WhenAll(unacked);
        }

        /// <summary>
        ///     Acknowledges the <see cref="PrivateMessage"/> record associated with the specified <paramref name="username"/> and <paramref name="id"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="id">The ID of the message.</param>
        /// <returns>The operation context.</returns>
        public async Task AcknowledgeMessageAsync(string username, int id)
        {
            using var context = ContextFactory.CreateDbContext();
            var message = context.PrivateMessages.FirstOrDefault(m => m.Username == username && m.Id == id);

            if (message != default)
            {
                await SoulseekClient.AcknowledgePrivateMessageAsync(id);
                message.Acknowledged = true;
                context.SaveChanges();
            }
            else
            {
                Log.Warning("Attempted to acknowledge an unknown private message from {Username} with ID {Id}", username, id);
            }
        }

        /// <summary>
        ///     Creates a new, or activates an existing, conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        public Task CreateAsync(string username)
        {
            ActivateConversation(username);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Returns the <see cref="Conversation"/> matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An expression used to locate the conversation.</param>
        /// <returns>The operation context, including the located conversation, if one was found.</returns>
        public Task<Conversation> FindAsync(Expression<Func<Conversation, bool>> expression)
        {
            using var context = ContextFactory.CreateDbContext();

            var conversation = context.Conversations
                .AsNoTracking()
                .Where(expression)
                .FirstOrDefault();

            if (conversation != default)
            {
                // add an option to limit this, or figure out pagination.
                conversation.Messages = context.PrivateMessages.Where(m => m.Username == conversation.Username);
            }

            return Task.FromResult(conversation);
        }

        /// <summary>
        ///     Returns the list of all <see cref="Conversation"/> records matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to locate conversations.</param>
        /// <returns>The operation context, including the list of found conversations.</returns>
        public Task<IEnumerable<Conversation>> ListAsync(Expression<Func<Conversation, bool>> expression = null)
        {
            using var context = ContextFactory.CreateDbContext();

            return Task.FromResult(context.Conversations.Where(expression).AsEnumerable());
        }

        /// <summary>
        ///     Handles the receipt of an inbound <see cref="PrivateMessage"/>.
        /// </summary>
        /// <param name="username">The username associated with the message.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        public Task HandleMessageAsync(string username, PrivateMessage message)
        {
            ActivateConversation(username);

            using var context = ContextFactory.CreateDbContext();

            // the server replays unacked messages when we log in. figure out if we've seen this message before, and if so sync it.
            var existing = context.PrivateMessages.FirstOrDefault(m =>
                m.Username == message.Username &&
                m.Message == message.Message &&
                m.Id == message.Id);

            if (existing != null)
            {
                // the message was replayed. i'm not sure what is updated, so just update anything that might have updated
                existing.Timestamp = message.Timestamp;
                existing.Replayed = message.Replayed;
                existing.Acknowledged = message.Acknowledged;
            }
            else
            {
                // this is a new message, so append it
                context.PrivateMessages.Add(message);
            }

            context.SaveChanges();

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Removes (marks inactive) the conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        public async Task RemoveAsync(string username)
        {
            await AcknowledgeAsync(username);

            using var context = ContextFactory.CreateDbContext();

            var conversation = context.Conversations.FirstOrDefault(c => c.Username == username);

            if (conversation != default)
            {
                conversation.Active = false;
                context.SaveChanges();
            }
            else
            {
                Log.Warning("Attempted to remove an unknown conversation with {Username}", username);
            }
        }

        /// <summary>
        ///     Sends the specified <paramref name="message"/> to the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the recipient.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        public async Task SendMessageAsync(string username, string message)
        {
            ActivateConversation(username);

            // send the message over the network, then persist this should *probably* use a transaction but i'm scared of locking
            // with SQLite so i won't
            await SoulseekClient.SendPrivateMessageAsync(username, message);

            using var context = ContextFactory.CreateDbContext();

            context.PrivateMessages.Add(new PrivateMessage
            {
                Acknowledged = true,
                Id = 0,
                Message = message,
                Replayed = false,
                Timestamp = DateTime.UtcNow,
                Username = username,
                Direction = MessageDirection.Out,
            });

            context.SaveChanges();
        }

        private void ActivateConversation(string username)
        {
            using var context = ContextFactory.CreateDbContext();

            var conversation = context.Conversations.FirstOrDefault(c => c.Username == username);

            if (conversation != default)
            {
                conversation.Active = true;
            }
            else
            {
                context.Conversations.Add(new Conversation { Username = username, Active = true });
            }

            context.SaveChanges();
        }
    }
}
