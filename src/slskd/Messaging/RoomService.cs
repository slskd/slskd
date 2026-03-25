// <copyright file="RoomService.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ https://slskd.org
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, either version 3 of the License, or
//   │ (at your option) any later version.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to
//   │ Section 7 of the GNU Affero General Public License.  See the
//   │ LICENSE file in the root directory of this project for the
//   │ complete terms and conditions.
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Events;
    using slskd.Users;

    using Soulseek;

    /// <summary>
    ///     Chat room management and event handling.
    /// </summary>
    public interface IRoomService
    {
        /// <summary>
        ///     Joins the specified <paramref name="roomName"/>.
        /// </summary>
        /// <param name="roomName">The name of the room to join.</param>
        /// <returns>The operation context, including information about the room.</returns>
        Task<RoomData> JoinAsync(string roomName);

        /// <summary>
        ///     Leaves the specified <paramref name="roomName"/>.
        /// </summary>
        /// <param name="roomName">The name of the room to leave.</param>
        /// <returns>The operation context.</returns>
        Task LeaveAsync(string roomName);

        /// <summary>
        ///     Attempts to join the specified <paramref name="roomNames"/>.
        /// </summary>
        /// <remarks>
        ///     Failures are logged but not thrown. Use JoinAsync() to trap Exceptions.
        /// </remarks>
        /// <param name="roomNames">The list of room names to join.</param>
        /// <returns>The operation context.</returns>
        Task TryJoinAsync(params string[] roomNames);
    }

    /// <summary>
    ///     Chat room management and event handling.
    /// </summary>
    public class RoomService : IRoomService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RoomService"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="stateMutator"></param>
        /// <param name="roomTracker"></param>
        /// <param name="userService"></param>
        /// <param name="eventBus"></param>
        public RoomService(
            ISoulseekClient soulseekClient,
            IOptionsMonitor<Options> optionsMonitor,
            IStateMutator<State> stateMutator,
            IRoomTracker roomTracker,
            IUserService userService,
            EventBus eventBus)
        {
            Client = soulseekClient;

            StateMutator = stateMutator;
            OptionsMonitor = optionsMonitor;

            RoomTracker = roomTracker;

            Users = userService;
            EventBus = eventBus;

            Client.LoggedIn += Client_LoggedIn;

            Client.RoomJoined += Client_RoomJoined;
            Client.RoomLeft += Client_RoomLeft;
            Client.RoomMessageReceived += Client_RoomMessageReceived;
        }

        private ISoulseekClient Client { get; }
        private ILogger Logger { get; set; } = Log.ForContext<RoomService>();
        private IStateMutator<State> StateMutator { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IRoomTracker RoomTracker { get; set; }
        private IUserService Users { get; set; }
        private EventBus EventBus { get; }

        /// <summary>
        ///     Joins the specified <paramref name="roomName"/>.
        /// </summary>
        /// <param name="roomName">The name of the room to join.</param>
        /// <returns>The operation context, including information about the room.</returns>
        public async Task<RoomData> JoinAsync(string roomName)
        {
            Logger.Debug("Joining room {Room}", roomName);

            try
            {
                var data = await Client.JoinRoomAsync(roomName);
                var room = Room.FromRoomData(data);
                RoomTracker.TryAdd(roomName, room);

                Logger.Debug("Room data for {Room}: {Info}", roomName, data.ToJson());
                return data;
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to join room {Room}: {Message}", roomName, ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Leaves the specified <paramref name="roomName"/>.
        /// </summary>
        /// <param name="roomName">The name of the room to leave.</param>
        /// <returns>The operation context.</returns>
        public async Task LeaveAsync(string roomName)
        {
            Logger.Debug("Leaving room {Room}", roomName);

            try
            {
                await Client.LeaveRoomAsync(roomName);
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to leave room {Room}: {Message}", roomName, ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Attempts to join the specified <paramref name="roomNames"/>.
        /// </summary>
        /// <remarks>
        ///     Failures are logged but not thrown. Use JoinAsync() to trap Exceptions.
        /// </remarks>
        /// <param name="roomNames">The list of room names to join.</param>
        /// <returns>The operation context.</returns>
        public async Task TryJoinAsync(params string[] roomNames)
        {
            var tasks = new List<Task>();

            foreach (var room in roomNames)
            {
                tasks.Add(JoinAsync(room));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "Caught Exception in JoinAsync");
            }
        }

        private async void Client_LoggedIn(object sender, EventArgs e)
        {
            var autoJoinRooms = OptionsMonitor.CurrentValue.Rooms;

            if (autoJoinRooms.Any())
            {
                Logger.Information("Auto-joining room(s) {Rooms}", string.Join(", ", autoJoinRooms));
                await TryJoinAsync(autoJoinRooms);
            }

            var previouslyJoinedRooms = RoomTracker.Rooms.Keys.Except(autoJoinRooms);

            if (previouslyJoinedRooms.Any())
            {
                Logger.Information("Attempting to rejoin room(s) {Rooms}", string.Join(", ", previouslyJoinedRooms));
                await TryJoinAsync(previouslyJoinedRooms.ToArray());
            }
        }

        private void Client_RoomJoined(object sender, RoomJoinedEventArgs args)
        {
            if (args.Username == Client.Username)
            {
                Logger.Information("Joined room {Room}", args.RoomName);
                StateMutator.SetValue(state => state with { Rooms = state.Rooms.Concat(new[] { args.RoomName }).Distinct().ToArray() });
            }
            else
            {
                RoomTracker.TryAddUser(args.RoomName, args.UserData);
            }
        }

        private void Client_RoomLeft(object sender, RoomLeftEventArgs args)
        {
            if (args.Username == Client.Username)
            {
                Logger.Information("Left room {Room}", args.RoomName);
                StateMutator.SetValue(state => state with { Rooms = state.Rooms.Where(room => room != args.RoomName).ToArray() });
            }
            else
            {
                RoomTracker.TryRemoveUser(args.RoomName, args.Username);
            }
        }

        private void Client_RoomMessageReceived(object sender, RoomMessageReceivedEventArgs args)
        {
            if (Users.IsBlacklisted(args.Username))
            {
                Logger.Debug("Ignored message from blacklisted user {Username} in {Room}: {Message}", args.Username, args.RoomName, args.Message);
                return;
            }

            var message = RoomMessage.FromEventArgs(args, DateTime.UtcNow);
            RoomTracker.AddOrUpdateMessage(args.RoomName, message);

            // todo: persist these in the database before raising (why are we not??)
            EventBus.Raise<RoomMessageReceivedEvent>(new RoomMessageReceivedEvent
            {
                Message = message,
            });
        }
    }
}