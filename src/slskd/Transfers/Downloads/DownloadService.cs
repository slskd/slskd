// <copyright file="DownloadService.cs" company="slskd Team">
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
using Soulseek;

namespace slskd.Transfers.Downloads
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using slskd.Events;
    using slskd.Files;
    using slskd.Integrations.FTP;
    using slskd.Relay;

    /// <summary>
    ///     Manages downloads.
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        ///     Adds the specified <paramref name="transfer"/>. Supersedes any existing record for the same file and username.
        /// </summary>
        /// <remarks>This should generally not be called; use EnqueueAsync() instead.</remarks>
        /// <param name="transfer"></param>
        void AddOrSupersede(Transfer transfer);

        /// <summary>
        ///     Enqueues the requested list of <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        ///     If one file in the specified collection fails, the rest will continue. An <see cref="AggregateException"/> will be
        ///     thrown after all files are dispositioned if any throws.
        /// </remarks>
        /// <param name="username">The username of remote user.</param>
        /// <param name="files">The list of files to enqueue.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown when the username is null or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown when no files are requested.</exception>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        Task<(List<Transfer> Enqueued, List<Transfer> Failed)> EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Downloads the specified enqueued <paramref name="transfer"/> from the remote user.
        /// </summary>
        /// <param name="transfer">The Transfer to download.</param>
        /// <param name="stateChanged">An optional delegate to invoke the transfer state changes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified Transfer is null.</exception>
        /// <exception cref="TransferNotFoundException">Thrown if the specified Transfer ID can't be found in the database.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the specified Transfer is not in the Queued | Locally state.</exception>
        /// <exception cref="DuplicateTransferException">Thrown if a download matching the username and filename is already tracked by Soulseek.NET.</exception>
        Task<Transfer> DownloadAsync(Transfer transfer, Action<Transfer> stateChanged = null, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Finds a single download matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match downloads.</param>
        /// <returns>The found transfer, or default if not found.</returns>
        Transfer Find(Expression<Func<Transfer, bool>> expression);

        /// <summary>
        ///     Retrieves the place in the remote queue for the download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique identifier for the download.</param>
        /// <returns>The retrieved place in queue.</returns>
        Task<int> GetPlaceInQueueAsync(Guid id);

        /// <summary>
        ///     Returns a list of all downloads matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match downloads.</param>
        /// <param name="includeRemoved">Optionally include downloads that have been removed previously.</param>
        /// <returns>The list of downloads matching the specified expression, or all downloads if no expression is specified.</returns>
        List<Transfer> List(Expression<Func<Transfer, bool>> expression = null, bool includeRemoved = false);

        /// <summary>
        ///     Removes <see cref="TransferStates.Completed"/> downloads older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which downloads are eligible for pruning, in hours.</param>
        /// <param name="stateHasFlag">An optional, additional state by which downloads are filtered for pruning.</param>
        /// <returns>The number of pruned downloads.</returns>
        int Prune(int age, TransferStates stateHasFlag = TransferStates.Completed);

        /// <summary>
        ///     Removes the download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the download.</param>
        void Remove(Guid id);

        /// <summary>
        ///     Cancels the download matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the download.</param>
        /// <returns>A value indicating whether the download was successfully cancelled.</returns>
        bool TryCancel(Guid id);

        /// <summary>
        ///     Updates the specified <paramref name="transfer"/>.
        /// </summary>
        /// <param name="transfer">The transfer to update.</param>
        void Update(Transfer transfer);
    }

    /// <summary>
    ///     Manages downloads.
    /// </summary>
    public class DownloadService : IDownloadService
    {
        public DownloadService(
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient,
            IDbContextFactory<TransfersDbContext> contextFactory,
            FileService fileService,
            IRelayService relayService,
            IFTPService ftpClient,
            EventBus eventBus)
        {
            Client = soulseekClient;
            OptionsMonitor = optionsMonitor;
            ContextFactory = contextFactory;
            Files = fileService;
            FTP = ftpClient;
            Relay = relayService;
            EventBus = eventBus;
        }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; } = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private ISoulseekClient Client { get; }
        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private IFTPService FTP { get; }
        private FileService Files { get; }
        private IRelayService Relay { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<DownloadService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private EventBus EventBus { get; }
        private ConcurrentDictionary<string, bool> Locks { get; } = new();

        /// <summary>
        ///     Adds the specified <paramref name="transfer"/>. Supersedes any existing record for the same file and username.
        /// </summary>
        /// <remarks>This should generally not be called; use EnqueueAsync() instead.</remarks>
        /// <param name="transfer"></param>
        public void AddOrSupersede(Transfer transfer)
        {
            using var context = ContextFactory.CreateDbContext();

            var existing = context.Transfers
                .Where(t => t.Direction == TransferDirection.Download)
                .Where(t => t.Username == transfer.Username)
                .Where(t => t.Filename == transfer.Filename)
                .Where(t => !t.Removed)
                .FirstOrDefault();

            if (existing != default)
            {
                Log.Debug("Superseding transfer record for {Filename} from {Username}", transfer.Filename, transfer.Username);
                existing.Removed = true;
            }

            context.Add(transfer);
            context.SaveChanges();
        }

        /// <summary>
        ///     Downloads the specified enqueued <paramref name="transfer"/> from the remote user.
        /// </summary>
        /// <param name="transfer">The Transfer to download.</param>
        /// <param name="stateChanged">An optional delegate to invoke the transfer state changes.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified Transfer is null.</exception>
        /// <exception cref="TransferNotFoundException">Thrown if the specified Transfer ID can't be found in the database.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the specified Transfer is not in the Queued | Locally state.</exception>
        /// <exception cref="DuplicateTransferException">Thrown if a download matching the username and filename is already tracked by Soulseek.NET.</exception>
        public async Task<Transfer> DownloadAsync(Transfer transfer, Action<Transfer> stateChanged = null, CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var syncRoot = new SemaphoreSlim(1, 1);

            void SynchronizedUpdate(Transfer transfer, bool cancellable = true)
            {
                syncRoot.Wait(cancellable ? cts.Token : CancellationToken.None);

                try
                {
                    Update(transfer);
                }
                finally
                {
                    syncRoot.Release();
                }
            }

            var lockName = $"{nameof(DownloadAsync)}:{transfer.Username}:{transfer.Filename}";

            if (!Locks.TryAdd(lockName, true))
            {
                Log.Debug("Ignoring concurrent invocation; lock {LockName} already held", lockName);
                return null;
            }

            /*
                from this point forward, any exit from this method MUST result in an update to the Transfer record
                in the database that sets EndedAt and ensures that the State property includes the Completed flag

                failure to do so means records will get "stuck" in the database/on the UI

                additionally, any acquired locks/semaphores must be released, including (and most importantly) the queue slot
            */
            try
            {
                Log.Debug("Acquired lock {LockName}", lockName);

                /*
                    fetch an updated copy of the transfer record from the database; now that we are locked, we *should*
                    have exclusive access to this record

                    we're not using DbContext and tracked changes here because we don't want to hold a connection
                    open for the duration of the download
                */
                transfer = Find(t => t.Id == transfer.Id)
                    ?? throw new TransferNotFoundException($"Transfer with ID {transfer.Id} not found");

                if (transfer.State != (TransferStates.Queued | TransferStates.Locally))
                {
                    throw new InvalidOperationException($"Invalid starting state for download; expected {TransferStates.Queued | TransferStates.Locally}, encountered {transfer.State}");
                }

                /*
                    Soulseek.NET keeps an internal dictionary of all transfers for the duration of the transfer logic;
                    check that list to see if there's already an instance of this download in it, just in case we've gotten
                    out of sync somehow
                */
                if (Client.Downloads.Any(u => u.Username == transfer.Username && u.Filename == transfer.Filename))
                {
                    throw new DuplicateTransferException("A duplicate download of the same file to the same user is already registered");
                }

                Log.Information("Initializing download of {Filename} from {Username}", transfer.Filename, transfer.Username);

                using var rateLimiter = new RateLimiter(250, flushOnDispose: true);

                var topts = new TransferOptions(
                    stateChanged: (args) =>
                    {
                        try
                        {
                            Log.Debug("Download of {Filename} from user {Username} changed state from {Previous} to {New}", transfer.Filename, transfer.Username, args.PreviousState, args.Transfer.State);

                            // prevent Exceptions thrown during shutdown from updating the transfer record with related Exceptions;
                            // instead, allow these to be left "hanging" so that they are properly cleaned up at the next startup
                            if (Application.IsShuttingDown)
                            {
                                Log.Debug("Download update of {Filename} to {Username} not persisted; app is shutting down", transfer.Filename, transfer.Username);
                                return;
                            }

                            transfer = transfer.WithSoulseekTransfer(args.Transfer);

                            // we don't know when the download is actually enqueued (remotely) until it switches into that state
                            // when it is enqueued locally is irrelevant, due to internal throttling etc
                            if (args.Transfer.State.HasFlag(TransferStates.Queued) && args.Transfer.State.HasFlag(TransferStates.Remotely))
                            {
                                transfer.EnqueuedAt ??= DateTime.UtcNow;
                            }

                            // todo: broadcast
                            SynchronizedUpdate(transfer);
                        }
                        finally
                        {
                            stateChanged?.Invoke(transfer);
                        }
                    },
                    progressUpdated: (args) => rateLimiter.Invoke(() =>
                    {
                        // don't do anything unless the `transfer` within the outer scope is in the
                        // InProgress state; this will help prevent out-of-band updates that are sometimes
                        // being made after the transfer is completed, causing them to appear 'stuck'
                        if (transfer.State == TransferStates.InProgress)
                        {
                            syncRoot.Wait(cts.Token);

                            try
                            {
                                // check again to see if the state changed while we were waiting to obtain the lock
                                if (transfer.State == TransferStates.InProgress)
                                {
                                    // update only the properties that we expect to change between progress updates
                                    // this helps prevent this update from 'stepping' on other updates
                                    transfer.BytesTransferred = args.Transfer.BytesTransferred;
                                    transfer.AverageSpeed = args.Transfer.AverageSpeed;

                                    using var context = ContextFactory.CreateDbContext();

                                    context.Transfers.Where(t => t.Id == transfer.Id).ExecuteUpdate(setter => setter
                                        .SetProperty(t => t.BytesTransferred, transfer.BytesTransferred)
                                        .SetProperty(t => t.AverageSpeed, transfer.AverageSpeed));
                                }
                            }
                            finally
                            {
                                syncRoot.Release();
                            }
                        }
                    }),
                    disposeOutputStreamOnCompletion: true);

                // register the cancellation token
                CancellationTokens.TryAdd(transfer.Id, cts);

                var completedTransfer = await Client.DownloadAsync(
                    username: transfer.Username,
                    remoteFilename: transfer.Filename,
                    outputStreamFactory: () => Task.FromResult(
                        Files.CreateFile(
                            filename: transfer.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Incomplete),
                            options: new CreateFileOptions
                            {
                                Access = System.IO.FileAccess.Write,
                                Mode = System.IO.FileMode.Create, // overwrites file if it exists
                                Share = System.IO.FileShare.None, // exclusive access for the duration of the download
                                UnixCreateMode = !string.IsNullOrEmpty(OptionsMonitor.CurrentValue.Permissions.File.Mode)
                                    ? OptionsMonitor.CurrentValue.Permissions.File.Mode.ToUnixFileMode()
                                    : null,
                            })),
                    size: transfer.Size,
                    startOffset: 0,
                    token: null,
                    cancellationToken: cts.Token,
                    options: topts);

                // explicitly dispose the rate limiter to prevent updates from it beyond this point, and in doing so we
                // flushe any pending update, _probably_ pushing the state of the transfer back to InProgress
                rateLimiter.Dispose();

                // copy the completed transfer that was returned from Soulseek.NET in a terminal, fully updated state
                // over the top of the transfer record, then persist it
                transfer = transfer.WithSoulseekTransfer(completedTransfer);

                // todo: broadcast to signalr hub
                SynchronizedUpdate(transfer, cancellable: false);

                // move the file from incomplete to complete
                var destinationDirectory = System.IO.Path.GetDirectoryName(transfer.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Downloads));

                var finalFilename = Files.MoveFile(
                    sourceFilename: transfer.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Incomplete),
                    destinationDirectory: destinationDirectory,
                    overwrite: false,
                    deleteSourceDirectoryIfEmptyAfterMove: true);

                Log.Debug("Moved file to {Destination}", finalFilename);

                // begin post-processing tasks; the file is downloaded, it has been removed from the client's download dictionary,
                // and the file has been moved from the incomplete directory to the downloads directory
                if (OptionsMonitor.CurrentValue.Relay.Enabled)
                {
                    _ = Relay.NotifyFileDownloadCompleteAsync(finalFilename);
                }

                EventBus.Raise(new DownloadFileCompleteEvent
                {
                    Timestamp = transfer.EndedAt.Value,
                    LocalFilename = finalFilename,
                    RemoteFilename = transfer.Filename,
                    Transfer = transfer,
                });

                // try to figure out if this file is the last of a directory, and if so, raise the associated
                // event. this can be tricky because we want to be sure that this is the last file in this specific
                // directory, excluding any pending downloads in a subdirectory.
                var remoteDirectorySeparator = transfer.Filename.GuessDirectorySeparator();
                var remoteDirectoryName = transfer.Filename.GetDirectoryName(directorySeparator: remoteDirectorySeparator);
                var pendingDownloadsInDirectory = Client.Downloads
                    .Where(t => t.Username == transfer.Username)
                    .Where(t => t.Filename.GetDirectoryName(directorySeparator: remoteDirectorySeparator) == remoteDirectoryName);

                if (!pendingDownloadsInDirectory.Any())
                {
                    EventBus.Raise(new DownloadDirectoryCompleteEvent
                    {
                        Timestamp = transfer.EndedAt.Value,
                        Username = transfer.Username,
                        LocalDirectoryName = destinationDirectory,
                        RemoteDirectoryName = remoteDirectoryName,
                    });
                }

                if (OptionsMonitor.CurrentValue.Integration.Ftp.Enabled)
                {
                    _ = FTP.UploadAsync(finalFilename);
                }

                return transfer;
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is TimeoutException)
            {
                transfer.EndedAt = DateTime.UtcNow;
                transfer.Exception = ex.Message;
                transfer.State = TransferStates.Completed;

                transfer.State |= ex switch
                {
                    OperationCanceledException => TransferStates.Cancelled,
                    TimeoutException => TransferStates.TimedOut,
                    _ => TransferStates.Errored,
                };

                // todo: broadcast
                SynchronizedUpdate(transfer, cancellable: false);

                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Download of {Filename} from user {Username} failed: {Message}", transfer.Filename, transfer.Username, ex.Message);

                transfer.EndedAt = DateTime.UtcNow;
                transfer.Exception = ex.Message;
                transfer.State = TransferStates.Completed | TransferStates.Errored;

                // todo: broadcast
                SynchronizedUpdate(transfer, cancellable: false);

                throw;
            }
            finally
            {
                try
                {
                    Locks.TryRemove(lockName, out _);
                    Log.Debug("Released lock {LockName}", lockName);

                    CancellationTokens.TryRemove(transfer.Id, out _);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to finalize download of {Filename} from {Username}: {Message}", transfer.Filename, transfer.Username, ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        ///     Enqueues the requested list of <paramref name="files"/>.
        /// </summary>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="files">The list of files to enqueue.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown when the username is null or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown when no files are requested.</exception>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        public async Task<(List<Transfer> Enqueued, List<Transfer> Failed)> EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username is required", nameof(username));
            }

            if (!files.Any())
            {
                throw new ArgumentException("At least one file is required", nameof(files));
            }

            IPEndPoint endpoint;

            try
            {
                // get the user's ip and port. this will throw if they are offline.
                endpoint = await Client.GetUserEndPointAsync(username, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue {Count} files from {Username}: {Message}", files.Count(), username, ex.Message);
                throw;
            }

            List<string> acquiredLocks = [];

            try
            {
                List<Transfer> enqueued = [];
                List<Transfer> failed = [];

                /*
                    first, check inputs and do an _exhaustive_ check for all of the files provided, separating files out
                    into good/bad

                    acquire locks for each, and track them so we can release them later (in the finally block)
                */
                foreach (var file in files)
                {
                    var transfer = new Transfer()
                    {
                        Id = Guid.NewGuid(),
                        Username = username,
                        Direction = TransferDirection.Download,
                        Filename = file.Filename, // important! use the remote filename
                        Size = file.Size,
                        StartOffset = 0, // todo: maybe implement resumeable downloads?
                        RequestedAt = DateTime.UtcNow,
                        State = TransferStates.Queued | TransferStates.Locally,
                    };

                    var lockName = $"{nameof(EnqueueAsync)}:{username}:{file.Filename}";

                    if (!Locks.TryAdd(lockName, true))
                    {
                        Log.Debug("Ignoring concurrent download enqueue attempt; lock {LockName} already held", lockName);
                        transfer.Exception = "A download for this file from this user is already underway";
                        transfer.State = TransferStates.Aborted | TransferStates.Completed;
                        failed.Add(transfer);
                        continue;
                    }

                    try
                    {
                        acquiredLocks.Add(lockName);
                        Log.Debug("Acquired lock {LockName}", lockName);

                        Log.Information("Download of {Filename} from {Username} requested", file.Filename, username);

                        using var context = ContextFactory.CreateDbContext();

                        /*
                            first, get all past downloads from this user for this filename
                        */
                        var existingRecords = context.Transfers
                            .Where(t => t.Direction == TransferDirection.Download)
                            .Where(t => t.Username == username)
                            .Where(t => t.Filename == file.Filename)
                            .AsNoTracking()
                            .ToList();

                        var existingInProgressRecords = existingRecords
                            .Where(t => t.EndedAt == null || !t.State.HasFlag(TransferStates.Completed))
                            .ToList();

                        /*
                            if there are any that haven't ended yet (checking a few ways out of paranoia), then there's already
                            an existing transfer record covering this file, and we're already enqueued. nothing more to do!

                            check the tracked download dictionary in Soulseek.NET to see if it knows about this already
                            it shouldn't, if the slskd database doesn't. but things could get desynced
                        */
                        if (existingInProgressRecords.Count != 0 || Client.Downloads.Any(u => u.Username == username && u.Filename == file.Filename))
                        {
                            Log.Debug("Ignoring concurrent download enqueue attempt; lock {LockName} already held", lockName);
                            transfer.Exception = "A download for this file from this user is already underway";
                            transfer.State = TransferStates.Aborted | TransferStates.Completed;
                            failed.Add(transfer);
                            continue;
                        }

                        /*
                            we've passed 3 different checks to ensure that this file is not already being downloaded, so
                            add it to the database and remove any existing (past) records of it from the UI
                        */
                        context.Add(transfer);

                        foreach (var record in existingRecords.Where(t => !t.Removed))
                        {
                            record.Removed = true;
                            context.Update(record);
                            Log.Debug("Marked existing download record of {Filename} from {Username} removed (id: {Id})", file.Filename, username, record.Id);
                        }

                        context.SaveChanges();
                        enqueued.Add(transfer);

                        Log.Information("Successfully enqueued download of {Filename} from {Username} (id: {Id})", file.Filename, username, transfer.Id);
                    }
                    catch (Exception ex)
                    {
                        // note: there's nothing to cancel or download yet, so any failure to execute the above is an error
                        Log.Error(ex, "Failed to enqueue download of {Filename} from {Username}: {Message}", file.Filename, username, ex.Message);
                        transfer.Exception = ex.Message;
                        transfer.State = TransferStates.Errored | TransferStates.Completed;
                        failed.Add(transfer);
                        continue;
                    }
                }

                if (enqueued.Count == 0)
                {
                    return (enqueued, failed);
                }

                // prime the cache for this user, to 1) make sure we can connect, and 2) avoid needing to race for it in the loop
                try
                {
                    await Client.ConnectToUserAsync(username, invalidateCache: false, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to connect to {Username}: {Message}", username, ex.Message);
                    throw;
                }

                /*
                    the files are all now in the database in the Queued | Locally state.

                    we'll proceed with downloading them, but in a separate task run in the background
                */
                _ = Task.Run(async () =>
                {
                    /*
                        enqueue each of the specified files, waiting to ensure that the remote client has responded to our
                        request to enqueue before moving on to the next

                        we do this because Soulseek.NET sends a TransferRequest/40 which expects a TransferResponse/41 in return,
                        telling us that either 1) the transfer can begin immediately 2) the transfer was enqueued remotely
                        or 3) the transfer was rejected

                        if at a later date Soulseek.NET is refactored to use different logic, we'll want to rethink this, particularly
                        to avoid the need to wait for a confirmation from the remote client.

                        note: we can't just fire off all of these requests at the same time, because the remote client will get
                        overwhelmed, responses will be delayed, and the timeout within Soulseek.NET that's waiting for the
                        TransferResponse/41 message will expire and fail the download
                    */
                    var concurrentRequests = 10;
                    var maxTimeToWaitForTransferAck = TimeSpan.FromMinutes(1);

                    var semaphore = new SemaphoreSlim(initialCount: concurrentRequests, maxCount: concurrentRequests);

                    var tasks = enqueued.Select(async transfer =>
                    {
                        await semaphore.WaitAsync();

                        try
                        {
                            Log.Debug("Acquired download semaphore for {Filename} from {Username}", transfer.Filename, transfer.Username);

                            try
                            {
                                var enqueuedTcs = new TaskCompletionSource<Transfer>();
                                List<string> transitions = [];

                                // set a hard limit on the time we are willing to wait for the remote client to confirm or reject
                                // the enqueue of the file. we have to do this so that we don't get stuck indefinitely
                                using var timeoutCts = new CancellationTokenSource(maxTimeToWaitForTransferAck);
                                timeoutCts.Token.Register(() =>
                                {
                                    if (!enqueuedTcs.Task.IsCompleted)
                                    {
                                        Log.Warning("Download of {Filename} from {Username} failed to enqueue remotely after hard time limit. State transition history: {History}", transfer.Filename, username, string.Join(", ", transitions));
                                        enqueuedTcs.TrySetException(new TimeoutException("Download failed to enqueue remotely after hard time limit"));
                                    }
                                });

                                void stateChanged(Transfer transfer)
                                {
                                    transitions.Add(transfer.State.ToString());

                                    // _contractually_ (covered by unit tests!) all downloads will at some point enter the Queued | Remotely state
                                    // there _should be_ no way we can get hung up here, but if we do, also check for Completed as a backstop
                                    if (transfer.State.HasFlag(TransferStates.Queued) && transfer.State.HasFlag(TransferStates.Remotely))
                                    {
                                        enqueuedTcs.TrySetResult(transfer);
                                    }

                                    // if something goes wrong and we never get to enqueued, trip the result when we transition into
                                    // completed, so we don't get stuck. we shouldn't take this to mean we were successful, we're just
                                    // trying to ensure we don't get stuck
                                    if (transfer.State.HasFlag(TransferStates.Completed))
                                    {
                                        if (transfer.State.HasFlag(TransferStates.Succeeded))
                                        {
                                            enqueuedTcs.TrySetResult(transfer);
                                        }
                                        else
                                        {
                                            enqueuedTcs.TrySetException(new TransferException(transfer.Exception));
                                        }
                                    }
                                }

                                // enqueue the file. this call will return when the record has been inserted and the download task
                                // kicked off.  the stateChanged delegate will be passed down to the download task, which will eventually trip it
                                _ = Task.Run(() => DownloadAsync(transfer, stateChanged, cancellationToken)).ContinueWith(task =>
                                {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        Log.Information("Task for download of {Filename} from {Username} completed successfully", transfer.Filename, transfer.Username);
                                        return;
                                    }

                                    /*
                                        things that can cause us to arrive here:

                                        * transfer record deleted somehow
                                        * transfer record updated so that it's no longer in Queued | Locally
                                        * Soulseek.NET already tracking an identical download (slskd <> Soulseek.NET desync)
                                    */
                                    Log.Error(task.Exception, "Task for download of {Filename} from {Username} did not complete successfully: {Error}", transfer.Filename, transfer.Username, task.Exception.Message);

                                    try
                                    {
                                        var foundTransfer = Find(t => t.Id == transfer.Id);

                                        if (transfer is not null)
                                        {
                                            transfer.EndedAt ??= DateTime.UtcNow;
                                            transfer.Exception ??= task.Exception.InnerException.Message;

                                            if (!transfer.State.HasFlag(TransferStates.Completed))
                                            {
                                                transfer.State = TransferStates.Completed | task.Exception.InnerException switch
                                                {
                                                    OperationCanceledException => TransferStates.Cancelled,
                                                    TimeoutException => TransferStates.TimedOut,
                                                    _ => TransferStates.Errored,
                                                };
                                            }

                                            Update(transfer);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Failed to clean up transfer {Id} after failed execution: {Message}", transfer.Id, ex.Message);
                                    }
                                }, cancellationToken);

                                // wait for the download to either enter the Queued | Remotely or Completed states, or for the hard limit
                                // timeout to trip and set an exception
                                await enqueuedTcs.Task;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to download {Filename} from {Username}: {Message}", transfer.Filename, transfer.Username, ex.Message);

                                try
                                {
                                    var transferRecord = Find(t => t.Id == transfer.Id);

                                    if (transfer is not null)
                                    {
                                        transfer.EndedAt ??= DateTime.UtcNow;
                                        transfer.Exception ??= ex.Message;

                                        if (!transfer.State.HasFlag(TransferStates.Completed))
                                        {
                                            transfer.State = TransferStates.Completed | ex switch
                                            {
                                                OperationCanceledException => TransferStates.Cancelled,
                                                TimeoutException => TransferStates.TimedOut,
                                                _ => TransferStates.Errored,
                                            };
                                        }

                                        Update(transfer);
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    Log.Error(innerEx, "Failed to clean up transfer {Id} after failed enqueue: {Message}", transfer.Id, innerEx.Message);
                                }
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                            Log.Debug("Released download semaphore for {Filename} from {Username}", transfer.Filename, transfer.Username);
                        }
                    });

                    await Task.WhenAll(tasks);
                }, cancellationToken).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        Log.Warning("Task for enqueue of {Count} files from {Username} completed successfully", files.Count(), username);
                        return;
                    }

                    Log.Error(task.Exception, "Task for enqueue of {Count} files from {Username} did not complete successfully: {Error}", files.Count(), username, task.Exception.Flatten().Message);
                }, cancellationToken: CancellationToken.None);

                return (enqueued, failed);
            }
            finally
            {
                foreach (var lockName in acquiredLocks)
                {
                    if (Locks.TryRemove(lockName, out _))
                    {
                        Log.Debug("Released lock {LockName}", lockName);
                    }
                }
            }
        }

        /// <summary>
        ///     Finds a single download matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match downloads.</param>
        /// <returns>The found transfer, or default if not found.</returns>
        public Transfer Find(Expression<Func<Transfer, bool>> expression)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                return context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(expression).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to find download: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Retrieves the place in the remote queue for the download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The unique identifier for the download.</param>
        /// <returns>The retrieved place in queue.</returns>
        public async Task<int> GetPlaceInQueueAsync(Guid id)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                var transfer = context.Transfers.Find(id);

                if (transfer == default)
                {
                    throw new NotFoundException($"No download matching id ${id}");
                }

                var place = await Client.GetDownloadPlaceInQueueAsync(transfer.Username, transfer.Filename);

                transfer.PlaceInQueue = place;
                context.SaveChanges();

                return place;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get place in queue for download {Id}: {Message}", id, ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Returns a list of all downloads matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match downloads.</param>
        /// <param name="includeRemoved">Optionally include downloads that have been removed previously.</param>
        /// <returns>The list of downloads matching the specified expression, or all downloads if no expression is specified.</returns>
        public List<Transfer> List(Expression<Func<Transfer, bool>> expression = null, bool includeRemoved = false)
        {
            expression ??= t => true;

            try
            {
                using var context = ContextFactory.CreateDbContext();

                return context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => !t.Removed || includeRemoved)
                    .Where(expression)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list downloads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Removes <see cref="TransferStates.Completed"/> downloads older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which downloads are eligible for pruning, in hours.</param>
        /// <param name="stateHasFlag">An optional, additional state by which downloads are filtered for pruning.</param>
        /// <returns>The number of pruned downloads.</returns>
        public int Prune(int age, TransferStates stateHasFlag = TransferStates.Completed)
        {
            if (!stateHasFlag.HasFlag(TransferStates.Completed))
            {
                throw new ArgumentException($"State must include {TransferStates.Completed}", nameof(stateHasFlag));
            }

            try
            {
                using var context = ContextFactory.CreateDbContext();

                var cutoffDateTime = DateTime.UtcNow.AddMinutes(-age);

                var expired = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => !t.Removed)
                    .Where(t => t.EndedAt.HasValue && t.EndedAt.Value < cutoffDateTime)
                    .Where(t => t.State == stateHasFlag)
                    .ToList();

                foreach (var tx in expired)
                {
                    tx.Removed = true;
                }

                var pruned = context.SaveChanges();

                if (pruned > 0)
                {
                    Log.Debug("Pruned {Count} expired downloads with state {State}", pruned, stateHasFlag);
                }

                return pruned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to prune downloads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Removes the download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the download.</param>
        public void Remove(Guid id)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                var transfer = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => t.Id == id)
                    .FirstOrDefault();

                if (transfer == default)
                {
                    throw new NotFoundException($"No download matching id ${id}");
                }

                if (!transfer.State.HasFlag(TransferStates.Completed))
                {
                    throw new InvalidOperationException($"Invalid attempt to remove a download before it is complete");
                }

                transfer.Removed = true;

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove download {Id}: {Message}", id, ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Cancels the download matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the download.</param>
        /// <returns>A value indicating whether the download was successfully cancelled.</returns>
        public bool TryCancel(Guid id)
        {
            if (CancellationTokens.TryRemove(id, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Synchronously updates the specified <paramref name="transfer"/>.
        /// </summary>
        /// <param name="transfer">The transfer to update.</param>
        public void Update(Transfer transfer)
        {
            using var context = ContextFactory.CreateDbContext();

            context.Update(transfer);
            context.SaveChanges();
        }
    }
}