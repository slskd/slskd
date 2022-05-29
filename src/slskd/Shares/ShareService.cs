using Microsoft.Extensions.Options;

namespace slskd.Shares
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public interface IShareService
    {
        ISharedFileCache Cache { get; }
        IReadOnlyList<Share> List(Func<Share, bool> expression = null);
    }

    public class ShareService : IShareService
    {
        public ShareService(
            ISharedFileCache sharedFileCache,
            IOptionsMonitor<Options> optionsMonitor)
        {
            Cache = sharedFileCache;
            OptionsMonitor = optionsMonitor;
        }

        public ISharedFileCache Cache { get; }

        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private List<Share> Shares { get; set; } = new List<Share>();

        public IReadOnlyList<Share> List(Func<Share, bool> expression = null)
        {
            return Shares.Where(expression).ToList().AsReadOnly();
        }
    }
}