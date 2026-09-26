// <copyright file="ServerChannel.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Interests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Exchanges messages over the client's server connection that the client library doesn't implement.
    /// </summary>
    /// <remarks>
    ///     Soulseek.NET keeps its server connection internal, so it's reached through reflection. Responses are observed
    ///     alongside the library's own message handler, which ignores codes it doesn't recognize.
    /// </remarks>
    public sealed class ServerChannel : IServerChannel
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServerChannel"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        public ServerChannel(ISoulseekClient soulseekClient)
        {
            Client = soulseekClient;
        }

        private ISoulseekClient Client { get; }
        private object SyncRoot { get; } = new object();
        private List<PendingRequest> PendingRequests { get; } = new List<PendingRequest>();
        private object Connection { get; set; }
        private EventInfo ConnectionMessageRead { get; set; }
        private Delegate ConnectionMessageReadHandler { get; set; }
        private MethodInfo ConnectionWriteAsync { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ServerChannel>();

        /// <inheritdoc/>
        public Task SendAsync(byte[] message, CancellationToken cancellationToken = default)
        {
            var (connection, write) = GetConnection();
            return WriteAsync(connection, write, message, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<byte[]> RequestAsync(
            byte[] message,
            int responseCode,
            string key = null,
            Func<byte[], string> keySelector = null,
            CancellationToken cancellationToken = default)
        {
            var (connection, write) = GetConnection();
            var pending = new PendingRequest(responseCode, key, keySelector);

            lock (SyncRoot)
            {
                PendingRequests.Add(pending);
            }

            try
            {
                await WriteAsync(connection, write, message, cancellationToken);

                return await pending.Completion.Task.WaitAsync(TimeSpan.FromMilliseconds(Client.Options.MessageTimeout), cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("The server didn't respond in time");
            }
            finally
            {
                lock (SyncRoot)
                {
                    PendingRequests.Remove(pending);
                }
            }
        }

        private static Task WriteAsync(object connection, MethodInfo write, byte[] message, CancellationToken cancellationToken)
        {
            return (Task)write.Invoke(
                connection,
                BindingFlags.DoNotWrapExceptions,
                binder: null,
                parameters: new object[] { message, (CancellationToken?)cancellationToken },
                culture: null);
        }

        /// <summary>
        ///     Returns the current server connection, subscribing to its messages if it's new; the client creates a new
        ///     connection each time it connects.
        /// </summary>
        private (object Connection, MethodInfo Write) GetConnection()
        {
            if (!Client.State.HasFlag(SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn))
            {
                throw new InvalidOperationException("The client must be connected and logged in");
            }

            var property = Client.GetType().GetProperty("ServerConnection", InstanceMembers)
                ?? throw new NotSupportedException("This version of Soulseek.NET doesn't expose a server connection");

            var connection = property.GetValue(Client)
                ?? throw new InvalidOperationException("The client isn't connected to the server");

            lock (SyncRoot)
            {
                if (!ReferenceEquals(connection, Connection))
                {
                    var type = connection.GetType();

                    var write = type.GetMethod("WriteAsync", InstanceMembers, new[] { typeof(byte[]), typeof(CancellationToken?) })
                        ?? throw new NotSupportedException("This version of Soulseek.NET can't write raw server messages");

                    var messageRead = type.GetEvent("MessageRead", InstanceMembers)
                        ?? throw new NotSupportedException("This version of Soulseek.NET doesn't raise server message events");

                    // the event's argument type is internal; the handler accepts any EventArgs, which a delegate of the
                    // event's type can bind to
                    var handler = Delegate.CreateDelegate(
                        messageRead.EventHandlerType,
                        this,
                        typeof(ServerChannel).GetMethod(nameof(Connection_MessageRead), InstanceMembers));

                    if (Connection is not null)
                    {
                        ConnectionMessageRead.RemoveEventHandler(Connection, ConnectionMessageReadHandler);
                    }

                    messageRead.AddEventHandler(connection, handler);

                    Connection = connection;
                    ConnectionMessageRead = messageRead;
                    ConnectionMessageReadHandler = handler;
                    ConnectionWriteAsync = write;
                }

                return (Connection, ConnectionWriteAsync);
            }
        }

        private void Connection_MessageRead(object sender, EventArgs args)
        {
            // this may run on the connection's read loop; it has to be quick and must not throw
            try
            {
                // length (4) + code (4) + payload
                if (args.GetType().GetProperty("Message")?.GetValue(args) is not byte[] message || message.Length < 8)
                {
                    return;
                }

                var code = BitConverter.ToInt32(message, 4);
                PendingRequest[] candidates;

                lock (SyncRoot)
                {
                    candidates = PendingRequests.Where(p => p.ResponseCode == code).ToArray();
                }

                if (candidates.Length == 0)
                {
                    return;
                }

                var payload = message[8..];

                foreach (var request in candidates.Where(c => c.Key is null))
                {
                    request.Completion.TrySetResult(payload);
                }

                var keyed = candidates.Where(c => c.Key is not null).ToArray();

                if (keyed.Length == 0)
                {
                    return;
                }

                string key = null;

                try
                {
                    key = keyed[0].KeySelector?.Invoke(payload);
                }
                catch (Exception ex)
                {
                    Log.Debug("Failed to read the key of server message {Code}: {Message}", code, ex.Message);
                }

                var matches = keyed.Where(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)).ToArray();

                foreach (var request in matches.Length > 0 ? matches : keyed.Take(1))
                {
                    request.Completion.TrySetResult(payload);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to handle server message: {Message}", ex.Message);
            }
        }

        private sealed class PendingRequest
        {
            public PendingRequest(int responseCode, string key, Func<byte[], string> keySelector)
            {
                ResponseCode = responseCode;
                Key = key;
                KeySelector = keySelector;
            }

            public int ResponseCode { get; }
            public string Key { get; }
            public Func<byte[], string> KeySelector { get; }
            public TaskCompletionSource<byte[]> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
