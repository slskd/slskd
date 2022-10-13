// <copyright file="Host.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     A share host.
    /// </summary>
    public class Host
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Host"/> class.
        /// </summary>
        /// <param name="name">The name of the host.</param>
        /// <param name="shares">The collection of hosted shares.</param>
        /// <param name="state">The initial state of the host.</param>
        public Host(string name, IEnumerable<Share> shares = null, HostState state = HostState.Offline)
        {
            Name = name;
            Shares = shares ?? Enumerable.Empty<Share>();
            State = state;
        }

        /// <summary>
        ///     Gets the name of the host.
        /// </summary>
        /// <remarks>Corresponds to the configured <see cref="Options.InstanceName"/> of the host.</remarks>
        public string Name { get; }

        /// <summary>
        ///     Gets the collection of hosted shares.
        /// </summary>
        public IEnumerable<Share> Shares { get; private set; }

        /// <summary>
        ///     Gets the host state.
        /// </summary>
        public HostState State { get; private set; }

        /// <summary>
        ///     Sets the hosted shares for the host.
        /// </summary>
        /// <param name="shares">The collection of shares.</param>
        public void SetShares(IEnumerable<Share> shares) => Shares = shares;

        /// <summary>
        ///     Sets the state of the host.
        /// </summary>
        /// <param name="state">The new state.</param>
        public void SetState(HostState state) => State = state;
    }
}