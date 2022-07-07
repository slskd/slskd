// <copyright file="TransferTracker.cs" company="slskd Team">
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
    using System.Linq;
    using System.Threading;
    using Soulseek;

    /// <summary>
    ///     Transfer extensions.
    /// </summary>
    public static class TransferTrackerExtensions
    {
        /// <summary>
        ///     Filters a Transfer collection by user.
        /// </summary>
        /// <param name="directedTransfers"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        public static ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> FromUser(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>> directedTransfers,
            string username)
        {
            directedTransfers.TryGetValue(username, out var transfers);
            return transfers ?? new ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>();
        }

        /// <summary>
        ///     Maps a Transfer collection to a serializable object.
        /// </summary>
        /// <param name="directedTransfers"></param>
        /// <returns></returns>
        public static object ToMap(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>> directedTransfers)
        {
            return directedTransfers.Select(u => new
            {
                Username = u.Key,
                Directories = u.Value.Values
                     .GroupBy(f => f.Transfer.Filename.DirectoryName())
                     .Select(d => new { Directory = d.Key, Files = d.Select(r => r.Transfer) }),
            });
        }

        /// <summary>
        ///     Maps a Transfer collection to a serializable object.
        /// </summary>
        /// <param name="userTransfers"></param>
        /// <returns></returns>
        public static object ToMap(
            this ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> userTransfers)
        {
            return userTransfers.Values
                .GroupBy(f => f.Transfer.Filename.DirectoryName())
                .Select(d => new { Directory = d.Key, Files = d.Select(r => r.Transfer) });
        }

        /// <summary>
        ///     Filters a Transfer collection by direction.
        /// </summary>
        /// <param name="allTransfers"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>> WithDirection(
            this ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>> allTransfers,
            TransferDirection direction)
        {
            allTransfers.TryGetValue(direction, out var transfers);
            return transfers ?? new ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>();
        }

        /// <summary>
        ///     Retrieves a Transfer from a Transfer collection by id.
        /// </summary>
        /// <param name="userTransfers"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource) WithId(
            this ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> userTransfers,
            string id)
        {
            userTransfers.TryGetValue(id, out var transfer);
            return transfer;
        }
    }

    /// <summary>
    ///     Tracks transfers.
    /// </summary>
    public class TransferTracker : ITransferTracker
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransferTracker"/> class.
        /// </summary>
        public TransferTracker()
        {
            Transfers.TryAdd(TransferDirection.Download, new ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>());
            Transfers.TryAdd(TransferDirection.Upload, new ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>());
        }

        /// <summary>
        ///     Gets tracked transfers.
        /// </summary>
        public ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>> Transfers { get; private set; } =
            new ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.Transfer, CancellationTokenSource)>>>();

        /// <summary>
        ///     Adds or updates a tracked transfer.
        /// </summary>
        /// <param name="transfer"></param>
        /// <param name="cancellationTokenSource"></param>
        public void AddOrUpdate(Transfer transfer, CancellationTokenSource cancellationTokenSource)
        {
            Transfers.TryGetValue(transfer.Direction, out var direction);

            direction.AddOrUpdate(transfer.Username, GetNewDictionaryForUser(transfer, cancellationTokenSource), (user, dict) =>
            {
                var xfer = API.Transfer.FromSoulseekTransfer(transfer);
                dict.AddOrUpdate(xfer.Id, (xfer, cancellationTokenSource), (id, record) => (xfer, cancellationTokenSource));
                return dict;
            });
        }

        /// <summary>
        ///     Gets the specified transfer.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="username"></param>
        /// <param name="id"></param>
        /// <param name="transfer"></param>
        /// <returns></returns>
        public bool TryGet(TransferDirection direction, string username, string id, out (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource) transfer)
        {
            transfer = default;

            if (Transfers.TryGetValue(direction, out var transfers) && transfers.TryGetValue(username, out var user) && user.TryGetValue(id, out transfer))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Removes a tracked transfer.
        /// </summary>
        /// <remarks>Omitting an id will remove ALL transfers associated with the specified username.</remarks>
        public void TryRemove(TransferDirection direction, string username, string id = null)
        {
            Transfers.TryGetValue(direction, out var directionDict);

            if (string.IsNullOrEmpty(id))
            {
                directionDict.TryRemove(username, out _);
            }
            else
            {
                directionDict.TryGetValue(username, out var userDict);
                userDict.TryRemove(id, out _);

                if (userDict.IsEmpty)
                {
                    directionDict.TryRemove(username, out _);
                }
            }
        }

        /// <summary>
        ///     Gets a value indicating whether a transfer matching the specified information is tracked.
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="username"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public bool Contains(TransferDirection direction, string username, string filename)
        {
            if (Transfers.TryGetValue(direction, out var directionDict))
            {
                if (directionDict.TryGetValue(username, out var userDict))
                {
                    return userDict.Values.Any(record => record.Transfer.Filename == filename);
                }
            }

            return false;
        }

        private ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> GetNewDictionaryForUser(Transfer transfer, CancellationTokenSource cancellationTokenSource)
        {
            var r = new ConcurrentDictionary<string, (API.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>();
            var xfer = API.Transfer.FromSoulseekTransfer(transfer);
            r.AddOrUpdate(xfer.Id, (xfer, cancellationTokenSource), (id, record) => (xfer, record.CancellationTokenSource));
            return r;
        }
    }
}