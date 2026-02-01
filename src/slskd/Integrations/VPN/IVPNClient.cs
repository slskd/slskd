// <copyright file="IVPNClient.cs" company="slskd Team">
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

using System.Threading.Tasks;

namespace slskd.Integrations.VPN;

/// <summary>
///     A lightweight interface for monitoring the status of a VPN provider.
/// </summary>
public interface IVPNClient
{
    /// <summary>
    ///     Fetch the VPN connection status from the provider.
    /// </summary>
    /// <returns>A value indicating whether the VPN is connected.</returns>
    public Task<bool> GetConnectionStatusAsync();

    /// <summary>
    ///     Fetch the forwarded port configured with the provider, if enabled.
    /// </summary>
    /// <returns>The forwarded port.</returns>
    public Task<int> GetForwardedPortAsync();
}