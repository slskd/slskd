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

namespace slskd.Transfer
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
        public static ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> FromUser(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>> directedTransfers,
            string username)
        {
            directedTransfers.TryGetValue(username, out var transfers);
            return transfers ?? new ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>();
        }

        /// <summary>
        ///     Maps a Transfer collection to a serializable object.
        /// </summary>
        /// <param name="directedTransfers"></param>
        /// <returns></returns>
        public static object ToMap(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>> directedTransfers)
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
            this ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> userTransfers)
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
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>> WithDirection(
            this ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>> allTransfers,
            TransferDirection direction)
        {
            allTransfers.TryGetValue(direction, out var transfers);
            return transfers ?? new ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>();
        }

        /// <summary>
        ///     Retrieves a Transfer from a Transfer collection by id.
        /// </summary>
        /// <param name="userTransfers"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource) WithId(
            this ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> userTransfers,
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
            Transfers.TryAdd(TransferDirection.Download, new ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>());
            Transfers.TryAdd(TransferDirection.Upload, new ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>());
        }

        /// <summary>
        ///     Gets tracked transfers.
        /// </summary>
        public ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>> Transfers { get; private set; } =
            new ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (API.DTO.Transfer, CancellationTokenSource)>>>();

        /// <summary>
        ///     Adds or updates a tracked transfer.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="cancellationTokenSource"></param>
        public void AddOrUpdate(TransferEventArgs args, CancellationTokenSource cancellationTokenSource)
        {
            Transfers.TryGetValue(args.Transfer.Direction, out var direction);

            direction.AddOrUpdate(args.Transfer.Username, GetNewDictionaryForUser(args, cancellationTokenSource), (user, dict) =>
            {
                var transfer = API.DTO.Transfer.FromSoulseekTransfer(args.Transfer);
                dict.AddOrUpdate(transfer.Id, (transfer, cancellationTokenSource), (id, record) => (transfer, cancellationTokenSource));
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
        public bool TryGet(TransferDirection direction, string username, string id, out (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource) transfer)
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

        private ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)> GetNewDictionaryForUser(TransferEventArgs args, CancellationTokenSource cancellationTokenSource)
        {
            var r = new ConcurrentDictionary<string, (API.DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>();
            var transfer = API.DTO.Transfer.FromSoulseekTransfer(args.Transfer);
            r.AddOrUpdate(transfer.Id, (transfer, cancellationTokenSource), (id, record) => (transfer, record.CancellationTokenSource));
            return r;
        }
    }
}