// <copyright file="Governor.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd.Transfers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Users;
    using Soulseek;

    /// <summary>
    ///     Governs transfer speed.
    /// </summary>
    public interface IGovernor
    {
        /// <summary>
        ///     Asynchronously obtains a grant of <paramref name="requestedBytes"/> for the specified <paramref name="transfer"/>.
        /// </summary>
        /// <remarks>
        ///     This operation completes when any number of bytes can be granted. The amount returned may be smaller than the
        ///     requested amount.
        /// </remarks>
        /// <param name="transfer">The transfer for which the grant is requested.</param>
        /// <param name="requestedBytes">The number of requested bytes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context, including the number of bytes granted.</returns>
        Task<int> GetBytesAsync(Transfer transfer, int requestedBytes, CancellationToken cancellationToken);

        /// <summary>
        ///     Returns wasted bytes for redistribution.
        /// </summary>
        /// <param name="transfer">The transfer which generated the waste.</param>
        /// <param name="attemptedBytes">The number of bytes that were attempted to be transferred.</param>
        /// <param name="grantedBytes">The number of bytes granted by all governors in the system.</param>
        /// <param name="actualBytes">The actual number of bytes transferred.</param>
        public void ReturnBytes(Transfer transfer, int attemptedBytes, int grantedBytes, int actualBytes);
    }

    /// <summary>
    ///     Governs transfer speed.
    /// </summary>
    public class Governor : IGovernor
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Governor"/> class.
        /// </summary>
        /// <param name="userService">The UserService instance to use.</param>
        /// <param name="optionsMonitor">The OptionsMonitor instance to use.</param>
        public Governor(
            IUserService userService,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Users = userService;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private ITokenBucket DefaultTokenBucket { get; set; } = new TokenBucket((int.MaxValue * 1024L) / 10, 100);
        private ILogger Log { get; } = Serilog.Log.ForContext<Governor>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private Dictionary<string, ITokenBucket> TokenBuckets { get; set; } = new Dictionary<string, ITokenBucket>();
        private IUserService Users { get; }

        /// <summary>
        ///     Asynchronously obtains a grant of <paramref name="requestedBytes"/> for the specified <paramref name="transfer"/>.
        /// </summary>
        /// <remarks>
        ///     This operation completes when any number of bytes can be granted. The amount returned may be smaller than the
        ///     requested amount.
        /// </remarks>
        /// <param name="transfer">The transfer for which the grant is requested.</param>
        /// <param name="requestedBytes">The number of requested bytes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context, including the number of bytes granted.</returns>
        public Task<int> GetBytesAsync(Transfer transfer, int requestedBytes, CancellationToken cancellationToken)
        {
            var group = Users.GetGroup(transfer.Username);
            var bucket = TokenBuckets.GetValueOrDefault(group, DefaultTokenBucket);

            return bucket.GetAsync(requestedBytes, cancellationToken);
        }

        /// <summary>
        ///     Returns wasted bytes for redistribution.
        /// </summary>
        /// <param name="transfer">The transfer which generated the waste.</param>
        /// <param name="attemptedBytes">The number of bytes that were attempted to be transferred.</param>
        /// <param name="grantedBytes">The number of bytes granted by all governors in the system.</param>
        /// <param name="actualBytes">The actual number of bytes transferred.</param>
        public void ReturnBytes(Transfer transfer, int attemptedBytes, int grantedBytes, int actualBytes)
        {
            var waste = Math.Min(0, grantedBytes - actualBytes);

            if (waste == 0)
            {
                return;
            }

            var group = Users.GetGroup(transfer.Username);
            var bucket = TokenBuckets.GetValueOrDefault(group, DefaultTokenBucket);

            // we don't have enough information to tell whether grantedBytes was reduced by the global limiter within
            // Soulseek.NET, so we just return the bytes that we know for sure that were wasted, which is grantedBytes - actualBytes.
            // example: we grant 1000 bytes. Soulseek.NET grants only 500. 250 bytes are written. ideally we would return 750
            // bytes, but instead we return 250. this discrepancy doesn't really matter because Soulseek.NET is the constraint in
            // this scenario and the additional tokens we would return would never be used.
            bucket.Return(waste);
        }

        private void Configure(Options options)
        {
            // todo: only do this if the speed changed
            DefaultTokenBucket = new TokenBucket((options.Groups.Default.Upload.SpeedLimit * 1024L) / 10, 100);

            var tokenBuckets = new Dictionary<string, ITokenBucket>();

            // todo: diff the existing dictionary and:
            // todo: remove groups that no longer exist
            // todo: add new groups
            // todo: update existing groups with a new token bucket, but only if the speed for that group changed
            foreach (var group in options.Groups.UserDefined)
            {
                tokenBuckets.Add(group.Key, new TokenBucket((group.Value.Upload.SpeedLimit * 1024L) / 10, 100));
            }

            TokenBuckets = tokenBuckets;
            Log.Debug("Reconfigured governor for {Count} groups", TokenBuckets.Count);
        }
    }
}