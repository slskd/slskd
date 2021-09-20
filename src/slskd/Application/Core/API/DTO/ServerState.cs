// <copyright file="ServerState.cs" company="slskd Team">
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

namespace slskd.Core
{
    using System.Net;
    using Soulseek;

    public class ServerState
    {
        public string Address { get; set; }
        public IPEndPoint IPEndPoint { get; set; }
        public SoulseekClientStates State { get; set; }
        public string Username { get; set; }
        public bool IsConnected => State.HasFlag(SoulseekClientStates.Connected);
        public bool IsLoggedIn => State.HasFlag(SoulseekClientStates.LoggedIn);
        public bool IsTransitioning => State.HasFlag(SoulseekClientStates.Connecting) || State.HasFlag(SoulseekClientStates.Disconnecting) || State.HasFlag(SoulseekClientStates.LoggingIn);
    }
}
