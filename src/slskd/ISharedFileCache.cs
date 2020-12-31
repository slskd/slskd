namespace slskd
{
    using Soulseek;
    using System;
    using System.Collections.Generic;

    internal interface ISharedFileCache
    {
        string Directory { get; }
        DateTime? LastFill { get; }
        long TTL { get; }

        void Fill();

        IEnumerable<Soulseek.File> Search(SearchQuery query);
    }
}