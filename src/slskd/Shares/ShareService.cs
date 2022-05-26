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
        public ShareService(ISharedFileCache sharedFileCache)
        {
            Cache = sharedFileCache;
        }

        public ISharedFileCache Cache { get; }

        public IReadOnlyList<Share> List(Func<Share, bool> expression = null)
        {
            return Cache.Shares.Where(expression).ToList().AsReadOnly();
        }
    }
}