// <copyright file="ApplicationState.cs" company="slskd Team">
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
    /// <summary>
    ///     Application service state.
    /// </summary>
    public record ApplicationState()
    {
        public string Version { get; init; } = Program.InformationalVersion;
        public string LatestVersion { get; init; } = Program.InformationalVersion;
        public bool? UpdateAvailable { get; init; } = null;
        public bool IsCanary { get; init; } = Program.IsCanary;
        public bool PendingReconnect { get; init; }
        public bool PendingRestart { get; init; }
        public bool PendingShareRescan { get; init; }
        public SharedFileCacheState SharedFileCache { get; init; } = new SharedFileCacheState();
    }
}