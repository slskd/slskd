// <copyright file="HostType.cs" company="slskd Team">
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
    ///     The type of host.
    /// </summary>
    public enum HostType
    {
        /// <summary>
        ///     Local.
        /// </summary>
        Local = 0,

        /// <summary>
        ///     Remote; shares are hosted by an Agent.
        /// </summary>
        Remote = 1,
    }
}
