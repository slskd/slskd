// <copyright file="ITransferTracker.cs" company="slskd Team">
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

namespace slskd.Transfers
{
    using System.Collections.Concurrent;
    using System.Threading;
    using Soulseek;

    /// <summary>
    ///     Tracks transfers.
    /// </summary>
    public interface ITransferTracker
    {
        /// <summary>
        ///     Tracked transfers.
        /// </summary>
        ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>> Transfers { get; }

        /// <summary>
        ///     Adds or updates a tracked transfer.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="cancellationTokenSource"></param>
        void AddOrUpdate(TransferEventArgs args, CancellationTokenSource cancellationTokenSource);

        /// <summary>
        ///     Removes a tracked transfer.
        /// </summary>
        /// <remarks>Omitting an id will remove ALL transfers associated with the specified username.</remarks>
        void TryRemove(TransferDirection direction, string username, string id = null);

        /// <summary>
        ///     Gets the specified transfer.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="username"></param>
        /// <param name="id"></param>
        /// <param name="transfer"></param>
        /// <returns></returns>
        bool TryGet(TransferDirection direction, string username, string id, out (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource) transfer);
    }
}