// <copyright file="HostState.cs" company="slskd Team">
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

namespace slskd.Shares
{
    /// <summary>
    ///     The state of a share host.
    /// </summary>
    public enum HostState
    {
        /// <summary>
        ///     Offline; unable to service requests.
        /// </summary>
        Offline = 0,

        /// <summary>
        ///     Online; able to service requests.
        /// </summary>
        Online = 1,
    }
}
