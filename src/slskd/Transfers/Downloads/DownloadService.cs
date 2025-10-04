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
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown when the username is null or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown when no files are requested.</exception>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        Task<List<Transfer>> EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files);

        /// <summary>
        ///     Enqueues the specified <paramref name="filename"/> from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of remote user.</param>
        /// <param name="filename">The remote filename to download.</param>
        /// <param name="size">The size of the file.</param>
        /// <returns>The operation context.</returns>
        Task<Transfer> EnqueueAsync(string username, string filename, long size);

        /// <summary>
        ///     Downloads the specified enqueued <paramref name="transfer"/> from the remote user.
        /// </summary>
        /// <param name="transfer">The Transfer to download.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified Transfer is null.</exception>
        /// <exception cref="TransferNotFoundException">Thrown if the specified Transfer ID can't be found in the database.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the specified Transfer is not in the Queued | Locally state.</exception>
        /// <exception cref="DuplicateTransferException">Thrown if a download matching the username and filename is already tracked by Soulseek.NET.</exception>
        Task<Transfer> DownloadAsync(Transfer transfer);

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
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the specified Transfer is null.</exception>
        /// <exception cref="TransferNotFoundException">Thrown if the specified Transfer ID can't be found in the database.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the specified Transfer is not in the Queued | Locally state.</exception>
        /// <exception cref="DuplicateTransferException">Thrown if a download matching the username and filename is already tracked by Soulseek.NET.</exception>
        public async Task<Transfer> DownloadAsync(Transfer transfer)
        {
            var cts = new CancellationTokenSource();
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
                        if (transfer.EnqueuedAt is null && args.Transfer.State.HasFlag(TransferStates.Queued) && args.Transfer.State.HasFlag(TransferStates.Remotely))
                        {
                            transfer.EnqueuedAt = DateTime.UtcNow;
                        }

                        // todo: broadcast
                        SynchronizedUpdate(transfer);
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
        ///     Enqueues the specified <paramref name="filename"/> from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of remote user.</param>
        /// <param name="filename">The remote filename to download.</param>
        /// <param name="size">The size of the file.</param>
        /// <returns>The operation context.</returns>
        public Task<Transfer> EnqueueAsync(string username, string filename, long size)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username), "Username is required");
            }

            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException(nameof(filename), "Filename is required");
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than zero");
            }

            var lockName = $"{nameof(EnqueueAsync)}:{username}:{filename}";

            if (!Locks.TryAdd(lockName, true))
            {
                Log.Debug("Ignoring concurrent download enqueue attempt; lock {LockName} already held", lockName);
                return null;
            }

            Guid id = Guid.NewGuid();

            try
            {
                Log.Debug("Acquired lock {LockName}", lockName);

                Log.Information("Download of {Filename} from {Username} requested", filename, username);

                using var context = ContextFactory.CreateDbContext();

                /*
                    first, get all past downloads from this user for this filename
                */
                var existingRecords = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => t.Username == username)
                    .Where(t => t.Filename == filename)
                    .AsNoTracking()
                    .ToList();

                var existingInProgressRecords = existingRecords
                    .Where(t => t.EndedAt == null || !t.State.HasFlag(TransferStates.Completed))
                    .ToList();

                /*
                    if there are any that haven't ended yet (checking a few ways out of paranoia), then there's already
                    an existing transfer record covering this file, and we're already enqueued. nothing more to do!
                */
                if (existingInProgressRecords.Count != 0)
                {
                    Log.Information("Download of {Filename} from {Username} is already queued or is in progress (ids: {Ids})", filename, username, string.Join(", ", existingInProgressRecords.Select(t => t.Id)));
                    return null;
                }

                /*
                    check the tracked download dictionary in Soulseek.NET to see if it knows about this already
                    it shouldn't, if the slskd database doesn't. but things could get desynced
                */
                if (Client.Downloads.Any(u => u.Username == username && u.Filename == filename))
                {
                    throw new DuplicateTransferException("A duplicate download of the same file from the same user is already registered");
                }

                /*
                    we're cleared to enqueue! create a new transfer record, and automatically mark any existing records
                    we found as 'removed' to clean up the UI
                */
                var transfer = new Transfer()
                {
                    Id = id,
                    Username = username,
                    Direction = TransferDirection.Download,
                    Filename = filename, // important! use the remote filename
                    Size = size,
                    StartOffset = 0, // todo: maybe implement resumeable downloads?
                    RequestedAt = DateTime.UtcNow,
                    State = TransferStates.Queued | TransferStates.Locally,
                };

                context.Add(transfer);

                foreach (var record in existingRecords.Where(t => !t.Removed))
                {
                    record.Removed = true;
                    context.Update(record);
                    Log.Debug("Marked existing download record of {Filename} from {Username} removed (id: {Id})", filename, username, record.Id);
                }

                context.SaveChanges();

                Log.Information("Successfully enqueued download of {Filename} from {Username} (id: {Id})", filename, username, id);

                /*
                    schedule the download immediately

                    Task.Run can fail due to thread pool exhaustion or OOM, and this *SHOULD* throw up the chain and
                    fail the enqueue request
                */
                _ = Task.Run(() => DownloadAsync(transfer)).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        Log.Information("Task for download of {Filename} from {Username} completed successfully", filename, username);
                        return;
                    }

                    /*
                        things that can cause us to arrive here:

                        * transfer record deleted somehow
                        * transfer record updated so that it's no longer in Queued | Locally
                        * Soulseek.NET already tracking an identical download (slskd <> Soulseek.NET desync)
                    */
                    Log.Error(task.Exception, "Task for download of {Filename} from {Username} did not complete successfully: {Error}", filename, username, task.Exception.Message);

                    try
                    {
                        var transfer = Find(t => t.Id == id);

                        if (transfer is not null)
                        {
                            transfer.EndedAt ??= DateTime.UtcNow;
                            transfer.Exception ??= task.Exception.Message;
                            transfer.State = TransferStates.Completed | transfer.State;

                            Update(transfer);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to clean up transfer {Id} after failed execution: {Message}", id, ex.Message);
                        throw;
                    }
                });

                return Task.FromResult(transfer);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue download of {Filename} from {Username}: {Message}", filename, username, ex.Message);

                try
                {
                    var transfer = Find(t => t.Id == id);

                    if (transfer is not null)
                    {
                        transfer.EndedAt ??= DateTime.UtcNow;
                        transfer.Exception ??= ex.Message;
                        transfer.State = TransferStates.Completed | transfer.State;

                        Update(transfer);
                    }
                }
                catch (Exception innerEx)
                {
                    Log.Error(innerEx, "Failed to clean up transfer {Id} after failed enqueue: {Message}", id, innerEx.Message);
                    throw;
                }

                throw;
            }
            finally
            {
                if (Locks.TryRemove(lockName, out _))
                {
                    Log.Debug("Released lock {LockName}", lockName);
                }
            }
        }

        /// <summary>
        ///     Enqueues the requested list of <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        ///     If one file in the specified collection fails, the rest will continue. An <see cref="AggregateException"/> will be
        ///     thrown after all files are dispositioned if any throws.
        /// </remarks>
        /// <param name="username">The username of the remote user.</param>
        /// <param name="files">The list of files to enqueue.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown when the username is null or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown when no files are requested.</exception>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        public async Task<List<Transfer>> EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username is required", nameof(username));
            }

            if (!files.Any())
            {
                throw new ArgumentException("At least one file is required", nameof(files));
            }

            var tasks = files.Select(file => EnqueueAsync(username, file.Filename, file.Size));

            var transfers = await Task.WhenAll(tasks);

            return transfers.ToList();
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