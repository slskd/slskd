// <copyright file="Agent.cs" company="slskd Team">
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

namespace slskd.Relay
{
    /// <summary>
    ///     Tracking information for a Relay agent.
    /// </summary>
    public record Agent
    {
        /// <summary>
        ///     The name of the agent.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     The IP address associated with the active connection.
        /// </summary>
        public string IPAddress { get; init; }
    }
}
