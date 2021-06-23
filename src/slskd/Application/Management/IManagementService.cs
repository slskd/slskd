// <copyright file="IManagementService.cs" company="slskd Team">
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

namespace slskd.Management
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    ///     Application and Soulseek client management.
    /// </summary>
    public interface IManagementService
    {
        /// <summary>
        ///     Disconnects the Soulseek client from the server.
        /// </summary>
        /// <param name="message">An optional message containing the reason for the disconnect.</param>
        /// <param name="exception">An optional Exception to associate with the disconnect.</param>
        void DisconnectServer(string message = null, Exception exception = null);

        /// <summary>
        ///     Connects the Soulseek client to the server using the configured username and password.
        /// </summary>
        /// <returns>The operation context.</returns>
        Task ConnectServerAsync();

        /// <summary>
        ///     Gets the current state of the connection to the Soulseek server.
        /// </summary>
        /// <returns>The current server state.</returns>
        ServerState GetServerState();
    }
}
