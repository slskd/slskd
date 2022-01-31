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

namespace slskd
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Users;
    using Soulseek;

    public interface IGovernor
    {
        Task<int> GetBytes(Transfer transfer, int requestedBytes, CancellationToken cancellationToken);
        public void ReturnBytes(Transfer transfer, int attemptedBytes, int grantedBytes, int actualBytes);
    }

    public class Governor : IGovernor
    {
        public Governor(
            IUserService userService,
            IOptionsMonitor<Options> optionsMonitor)
        {
            UserService = userService;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(Configure);

            Configure(OptionsMonitor.CurrentValue);
        }

        private IUserService UserService { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<Governor>();
        private ITokenBucket DefaultTokenBucket { get; set; }
        private Dictionary<string, ITokenBucket> TokenBuckets { get; set; } = new Dictionary<string, ITokenBucket>();

        public Task<int> GetBytes(Transfer transfer, int requestedBytes, CancellationToken cancellationToken)
        {
            var group = UserService.GetGroup(transfer.Username);
            var bucket = TokenBuckets.GetValueOrDefault(group, DefaultTokenBucket);

            return bucket.GetAsync(requestedBytes, cancellationToken);
        }

        public void ReturnBytes(Transfer transfer, int attemptedBytes, int grantedBytes, int actualBytes)
        {
            var group = UserService.GetGroup(transfer.Username);
            var bucket = TokenBuckets.GetValueOrDefault(group, DefaultTokenBucket);

            // we don't have enough information to tell whether grantedBytes was reduced by the
            // global limiter within Soulseek.NET, so we just return the bytes that we know for sure
            // that were wasted, which is grantedBytes - actualBytes.
            // example: we grant 1000 bytes.  Soulseek.NET grants only 500. 250 bytes are written.
            // ideally we would return 750 bytes, but instead we return 250. this discrepancy doesn't
            // really matter because Soulseek.NET is the constraint in this scenario and the additional
            // tokens we would return would never be used.
            bucket.Return(grantedBytes - actualBytes);
        }

        private void Configure(Options options)
        {
            DefaultTokenBucket = new TokenBucket((options.Groups.Default.Upload.SpeedLimit * 1024L) / 10, 100);

            var tokenBuckets = new Dictionary<string, ITokenBucket>();

            foreach (var group in options.Groups.UserDefined)
            {
                tokenBuckets.Add(group.Key, new TokenBucket((group.Value.Upload.SpeedLimit * 1024L) / 10, 100));
            }

            TokenBuckets = tokenBuckets;
            Log.Debug("Reconfigured governor for {Count} groups", TokenBuckets.Count);
        }
    }
}
