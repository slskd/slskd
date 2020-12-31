namespace slskd.Trackers
{
    using Soulseek;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    ///     Tracks transfers.
    /// </summary>
    public interface ITransferTracker
    {
        /// <summary>
        ///     Tracked transfers.
        /// </summary>
        ConcurrentDictionary<TransferDirection, ConcurrentDictionary<string, ConcurrentDictionary<string, (DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource)>>> Transfers { get; }

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
        bool TryGet(TransferDirection direction, string username, string id, out (DTO.Transfer Transfer, CancellationTokenSource CancellationTokenSource) transfer);
    }
}