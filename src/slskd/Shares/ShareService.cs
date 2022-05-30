// <copyright file="ShareService.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Soulseek;

    public interface IShareService
    {
        IReadOnlyList<Share> Shares { get; }

        Task<IEnumerable<Soulseek.Directory>> BrowseAsync();

        Task<Directory> ListAsync(string directory);

        Task<IEnumerable<File>> SearchAsync(SearchQuery query);

        Task StartScanAsync();

        Task<string> ResolveAsync(string remoteFilename);

        public IStateMonitor<ShareState> StateMonitor { get; }
    }

    public class ShareService : IShareService
    {
        public ShareService(
            IOptionsMonitor<Options> optionsMonitor,
            IShareCache sharesCache = null)
        {
            OptionsMonitor = optionsMonitor;

            // todo: when options change, fill SharesList with configured shares
            // and set ScanPending = true

            Cache = sharesCache ?? new ShareCache(OptionsMonitor);
            Cache.StateMonitor.OnChange(cacheState
                => State.SetValue(state
                    => state with { Cache = cacheState.Current }));
        }

        public IReadOnlyList<Share> Shares => SharesList.AsReadOnly();
        public IStateMonitor<ShareState> StateMonitor => State;

        private IShareCache Cache { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private List<Share> SharesList { get; set; } = new List<Share>();
        private IManagedState<ShareState> State { get; } = new ManagedState<ShareState>();

        public Task<IEnumerable<Soulseek.Directory>> BrowseAsync()
            => Task.FromResult(Cache.Browse());

        public Task<Soulseek.Directory> ListAsync(string directory)
            => Task.FromResult(Cache.List(directory));

        public Task<IEnumerable<Soulseek.File>> SearchAsync(SearchQuery query)
            => Cache.SearchAsync(query);

        public Task StartScanAsync()
            => Cache.FillAsync();

        public Task<string> ResolveAsync(string remoteFilename)
            => Task.FromResult(Cache.Resolve(remoteFilename));
    }
}