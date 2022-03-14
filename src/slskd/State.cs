// <copyright file="State.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text.Json.Serialization;
    using slskd.Users;
    using Soulseek;

    /// <summary>
    ///     Application service state.
    /// </summary>
    public record State
    {
        public VersionState Version { get; init; } = new VersionState();
        public bool PendingReconnect { get; init; }
        public bool PendingRestart { get; init; }
        public bool PendingShareRescan { get; init; }
        public ServerState Server { get; init; } = new ServerState();
        public DistributedNetworkState DistributedNetwork { get; init; } = new DistributedNetworkState();
        public SharedFileCacheState SharedFileCache { get; init; } = new SharedFileCacheState();
        public string[] Rooms { get; init; } = Array.Empty<string>();
        public User[] Users { get; init; } = Array.Empty<User>();
    }

    public record VersionState
    {
        public string Full { get; init; } = Program.FullVersion;
        public string Current { get; init; } = Program.SemanticVersion;
        public string Latest { get; init; } = null;
        public bool? IsUpdateAvailable { get; init; } = null;
        public bool IsCanary { get; init; } = Program.IsCanary;
        public bool IsDevelopment { get; init; } = Program.IsDevelopment;
    }

    public record ServerState
    {
        public string Address { get; init; }

        [JsonConverter(typeof(IPEndPointConverter))]
        public IPEndPoint IPEndPoint { get; init; }
        public SoulseekClientStates State { get; init; }
        public string Username { get; init; }
        public bool IsConnected => State.HasFlag(SoulseekClientStates.Connected);
        public bool IsLoggedIn => State.HasFlag(SoulseekClientStates.LoggedIn);
        public bool IsTransitioning => State.HasFlag(SoulseekClientStates.Connecting) || State.HasFlag(SoulseekClientStates.Disconnecting) || State.HasFlag(SoulseekClientStates.LoggingIn);
    }

    public record DistributedNetworkState
    {
        public int BranchLevel { get; init; }
        public string BranchRoot { get; init; }
        public bool CanAcceptChildren { get; init; }
        public int ChildLimit { get; init; }
        public IReadOnlyCollection<string> Children { get; init; }
        public bool HasParent { get; init; }
        public bool IsBranchRoot { get; init; }
        public string Parent { get; init; }
    }

    /// <summary>
    ///     Share cache state.
    /// </summary>
    public record SharedFileCacheState
    {
        /// <summary>
        ///     Gets a value indicating whether the cache is being filled.
        /// </summary>
        public bool Filling { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether the cache is filled.
        /// </summary>
        public bool Filled { get; init; } = false;

        /// <summary>
        ///     Gets a value indicating whether the cache is faulted.
        /// </summary>
        public bool Faulted { get; init; } = false;

        /// <summary>
        ///     Gets the current fill progress.
        /// </summary>
        public double FillProgress { get; init; }

        /// <summary>
        ///     Gets the number of cached directories.
        /// </summary>
        public int Directories { get; init; }

        /// <summary>
        ///     Gets the number of cached files.
        /// </summary>
        public int Files { get; init; }

        /// <summary>
        ///     Gets the number of directories excluded by filters.
        /// </summary>
        public int ExcludedDirectories { get; init; }
    }
}