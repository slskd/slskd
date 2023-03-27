// <copyright file="RelayMode.cs" company="slskd Team">
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
    ///     The mode the Relay is operating under.
    /// </summary>
    public enum RelayMode
    {
        /// <summary>
        ///     Controller mode.
        /// </summary>
        Controller = 0,

        /// <summary>
        ///     Agent mode.
        /// </summary>
        Agent = 1,

        /// <summary>
        ///     Debug mode; enables operation as both a controller and agent.
        /// </summary>
        Debug = 2,
    }
}
