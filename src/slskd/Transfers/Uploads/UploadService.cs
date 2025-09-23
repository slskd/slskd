// <copyright file="UploadService.cs" company="slskd Team">
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
using slskd.Files;
using Soulseek;

namespace slskd.Transfers.Uploads
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using slskd.Events;
    using slskd.Relay;
    using slskd.Shares;
    using slskd.Users;

    /// <summary>
    ///     Manages uploads.
    /// </summary>
    public interface IUploadService
    {
        /// <summary>
        ///     Gets the upload governor.
        /// </summary>
        IUploadGovernor Governor { get; }

        /// <summary>
        ///     Gets the upload queue.
        /// </summary>
        IUploadQueue Queue { get; }

        /// <summary>
        ///     Adds the specified <paramref name="transfer"/>. Supersedes any existing record for the same file and username.
        /// </summary>
        /// <remarks>This should generally not be called; use <see cref="EnqueueAsync(string, string)"/> instead.</remarks>
        /// <param name="transfer"></param>
        void AddOrSupersede(Transfer transfer);

        /// <summary>
        ///     Enqueues the requested file.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="filename">The local filename of the requested file.</param>
        /// <returns>The operation context.</returns>
        Task<Transfer> EnqueueAsync(string username, string filename);

        /// <summary>
        ///     Uploads the specified enqueued <paramref name="transfer"/> to the requesting user.
        /// </summary>
        /// <param name="transfer">The transfer to upload.</param>
        /// <returns>The operation context.</returns>
        Task<Transfer> UploadAsync(Transfer transfer);

        /// <summary>
        ///     Finds a single upload matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match uploads.</param>
        /// <returns>The found transfer, or default if not found.</returns>
        Transfer Find(Expression<Func<Transfer, bool>> expression);

        /// <summary>
        ///     Returns a summary of the uploads matching the specified <paramref name="expression"/>. This can be expensive;
        ///     consider caching.
        /// </summary>
        /// <param name="expression">The expression used to select uploads for summarization.</param>
        /// <returns>
        ///     The generated summary, including the number of files and total size in bytes.
        /// </returns>
        (int Files, long Bytes) Summarize(Expression<Func<Transfer, bool>> expression);

        /// <summary>
        ///     Returns a list of all uploads matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match uploads.</param>
        /// <param name="includeRemoved">A value indicating whether to include uploads that have been removed previously.</param>
        /// <returns>The list of uploads matching the specified expression, or all uploads if no expression is specified.</returns>
        List<Transfer> List(Expression<Func<Transfer, bool>> expression, bool includeRemoved);

        /// <summary>
        ///     Removes <see cref="TransferStates.Completed"/> uploads older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which uploads are eligible for pruning, in minutes.</param>
        /// <param name="stateHasFlag">An optional, additional state by which uploads are filtered for pruning.</param>
        /// <returns>The number of pruned uploads.</returns>
        int Prune(int age, TransferStates stateHasFlag = TransferStates.Completed);

        /// <summary>
        ///     Removes the upload matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the upload.</param>
        void Remove(Guid id);

        /// <summary>
        ///     Cancels the upload matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the upload.</param>
        /// <returns>A value indicating whether the upload was successfully cancelled.</returns>
        bool TryCancel(Guid id);

        /// <summary>
        ///     Synchronously updates the specified <paramref name="transfer"/>.
        /// </summary>
        /// <param name="transfer">The transfer to update.</param>
        void Update(Transfer transfer);
    }

    /// <summary>
    ///     Manages uploads.
    /// </summary>
    public class UploadService : IUploadService
    {
        public UploadService(
            FileService fileService,
            IUserService userService,
            ISoulseekClient soulseekClient,
            IOptionsMonitor<Options> optionsMonitor,
            IShareService shareService,
            IRelayService relayService,
            IDbContextFactory<TransfersDbContext> contextFactory,
            EventBus eventBus)
        {
            Files = fileService;
            Users = userService;
            Client = soulseekClient;
            Shares = shareService;
            Relay = relayService;
            ContextFactory = contextFactory;
            OptionsMonitor = optionsMonitor;
            EventBus = eventBus;

            Governor = new UploadGovernor(userService, optionsMonitor);
            Queue = new UploadQueue(userService, optionsMonitor);
        }

        /// <summary>
        ///     Gets the upload governor.
        /// </summary>
        public IUploadGovernor Governor { get; init; }

        /// <summary>
        ///     Gets the upload queue.
        /// </summary>
        public IUploadQueue Queue { get; init; }

        private FileService Files { get; }
        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; } = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private ISoulseekClient Client { get; set; }
        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<UploadService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IRelayService Relay { get; }
        private IShareService Shares { get; set; }
        private IUserService Users { get; set; }
        private EventBus EventBus { get; }
        private ConcurrentDictionary<string, bool> Locks { get; } = new();

        /// <summary>
        ///     Adds the specified <paramref name="transfer"/>. Supersedes any existing record for the same file and username.
        /// </summary>
        /// <remarks>This should generally not be called; use <see cref="EnqueueAsync(string, string)"/> instead.</remarks>
        /// <param name="transfer"></param>
        public void AddOrSupersede(Transfer transfer)
        {
            using var context = ContextFactory.CreateDbContext();

            var existing = context.Transfers
                .Where(t => t.Direction == TransferDirection.Upload)
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
        ///     Uploads the specified enqueued <paramref name="transfer"/> to the requesting user.
        /// </summary>
        /// <param name="transfer">The transfer to upload.</param>
        /// <returns>The operation context.</returns>
        public async Task<Transfer> UploadAsync(Transfer transfer)
        {
            var lockName = $"{nameof(UploadAsync)}:{transfer.Username}:{transfer.Filename}";

            if (!Locks.TryAdd(lockName, true))
            {
                Log.Debug("Ignoring concurrent invocation; lock {LockName} already held", lockName);
                return null;
            }

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

            string host = default;
            string localFilename = default;
            long localFileLength = default;

            Transfer t = transfer;

            try
            {
                Log.Debug("Acquired lock {LockName}", lockName);
                Log.Information("Initializing upload {Filename} to {Username}", t.Filename, t.Username);

                // users with uploads must be watched so that we can keep informed of their online status, privileges, and
                // statistics. this is so that we can accurately determine their effective group.
                try
                {
                    if (!Users.IsWatched(t.Username))
                    {
                        await Users.WatchAsync(t.Username);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to watch user {Username}", t.Username);
                }

                /*
                    fetch an updated copy of the transfer record from the database; now that we are locked, we *should*
                    have exclusive access to this record

                    we're not using DbContext and tracked changes here because we don't want to hold a connection
                    open for the duration of the upload
                */
                t = Find(t => t.Id == transfer.Id)
                    ?? throw new TransferNotFoundException($"Transfer with ID {transfer.Id} not found");

                if (t.State != (TransferStates.Queued | TransferStates.Locally))
                {
                    throw new TransferException($"Invalid starting state for upload; expected {TransferStates.Queued | TransferStates.Locally}, encountered {t.State}");
                }

                /*
                    Soulseek.NET keeps an internal dictionary of all transfers for the duration of the transfer logic;
                    check that list to see if there's already an instance of this upload in it, just in case we've gotten
                    out of sync somehow
                */
                if (Client.Uploads.Any(u => u.Username == t.Username && u.Filename == t.Filename))
                {
                    throw new TransferException("A duplicate upload of the same file to the same user is already registered");
                }

                // locate the file on disk. we checked this once already when enqueueing, but it may have moved since
                // can throw NotFoundException
                (host, localFilename, localFileLength) = await ResolveFileInfoAsync(remoteFilename: t.Filename);

                using var rateLimiter = new RateLimiter(250, flushOnDispose: true);

                var topts = new TransferOptions(
                    stateChanged: (args) =>
                    {
                        Log.Debug("Upload of {Filename} to user {Username} changed state from {Previous} to {New}", localFilename, t.Username, args.PreviousState, args.Transfer.State);

                        // prevent Exceptions thrown during shutdown from updating the transfer record with related Exceptions;
                        // instead, allow these to be left "hanging" so that they are properly cleaned up at the next startup
                        if (Application.IsShuttingDown)
                        {
                            Log.Debug("Upload update of {Filename} to {Username} not persisted; app is shutting down", t.Filename, t.Username);
                            return;
                        }

                        transfer = transfer.WithSoulseekTransfer(args.Transfer);

                        // todo: broadcast
                        SynchronizedUpdate(transfer);
                    },
                    progressUpdated: (args) => rateLimiter.Invoke(() =>
                    {
                        transfer = transfer.WithSoulseekTransfer(args.Transfer);

                        // todo: broadcast
                        SynchronizedUpdate(transfer);
                    }),
                    seekInputStreamAutomatically: false,
                    disposeInputStreamOnCompletion: true, // note: don't set this to false!
                    governor: (tx, req, ct) => Governor.GetBytesAsync(tx.Username, req, ct),
                    reporter: (tx, att, grant, act) => Governor.ReturnBytes(tx.Username, att, grant, act),
                    slotAwaiter: (tx, ct) => Queue.AwaitStartAsync(tx.Username, tx.Filename),
                    slotReleased: (tx) => Queue.Complete(tx.Username, tx.Filename));

                // register the cancellation token
                CancellationTokens.TryAdd(t.Id, cts);

                // add the transfer to the UploadQueue so that it can become eligible for selection
                Queue.Enqueue(t.Username, t.Filename);
                transfer.EnqueuedAt = DateTime.UtcNow;
                SynchronizedUpdate(transfer);

                Soulseek.Transfer completedTransfer;

                if (host == Program.LocalHostName)
                {
                    completedTransfer = await Client.UploadAsync(
                        username: t.Username,
                        remoteFilename: t.Filename,
                        size: localFileLength,
                        inputStreamFactory: (startOffset) =>
                        {
#pragma warning disable S2930 // "IDisposables" should be disposed
                            // disposeInputStreamOnCompletion takes care of this
                            var stream = new FileStream(localFilename, FileMode.Open, FileAccess.Read);
#pragma warning restore S2930 // "IDisposables" should be disposed

                            stream.Seek(startOffset, SeekOrigin.Begin);
                            return Task.FromResult((Stream)stream);
                        },
                        options: topts,
                        cancellationToken: cts.Token);
                }
                else
                {
                    completedTransfer = await Client.UploadAsync(
                        username: t.Username,
                        remoteFilename: t.Filename,
                        size: localFileLength,
                        inputStreamFactory: (startOffset) => Relay.GetFileStreamAsync(agentName: host, filename: t.Filename, startOffset, id: t.Id),
                        options: topts,
                        cancellationToken: cts.Token);

                    Relay.TryCloseFileStream(host, id: t.Id);
                }

                // explicitly dispose the rate limiter to prevent updates from it beyond this point, and in doing so we
                // flushe any pending update, _probably_ pushing the state of the transfer back to InProgress
                rateLimiter.Dispose();

                // copy the completed transfer that was returned from Soulseek.NET in a terminal, fully updated state
                // over the top of the transfer record, then persist it
                transfer = transfer.WithSoulseekTransfer(completedTransfer);

                // todo: broadcast
                SynchronizedUpdate(transfer, cancellable: false);

                EventBus.Raise(new UploadFileCompleteEvent
                {
                    Timestamp = transfer.EndedAt.Value,
                    LocalFilename = localFilename,
                    RemoteFilename = t.Filename,
                    Transfer = transfer,
                });

                return transfer;
            }
            catch (TransferNotFoundException ex)
            {
                Log.Error(ex, "Attempted to upload non-existent transfer {Id}", t.Id);
                throw;
            }
            catch (NotFoundException)
            {
                transfer.EndedAt = DateTime.Now;
                transfer.Exception = "File could not be found";
                transfer.State = TransferStates.Completed | TransferStates.Aborted;

                SynchronizedUpdate(transfer, cancellable: false);

                throw;
            }
            catch (OperationCanceledException ex)
            {
                transfer.EndedAt = DateTime.UtcNow;
                transfer.Exception = ex.Message;
                transfer.State = TransferStates.Completed | TransferStates.Cancelled;

                // todo: broadcast
                SynchronizedUpdate(transfer, cancellable: false);

                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Upload of {Filename} to user {Username} failed: {Message}", t.Filename, t.Username, ex.Message);

                transfer.EndedAt = DateTime.UtcNow;
                transfer.Exception = ex.Message;
                transfer.State = TransferStates.Completed | TransferStates.Errored;

                // todo: broadcast
                SynchronizedUpdate(transfer, cancellable: false);

                throw;
            }
            finally
            {
                if (host != Program.LocalHostName)
                {
                    try
                    {
                        Relay.TryCloseFileStream(host, id: t.Id);
                    }
                    catch
                    {
                        // noop
                    }
                }

                try
                {
                    Locks.TryRemove(lockName, out _);
                    Log.Debug("Released lock {LockName}", lockName);

                    CancellationTokens.TryRemove(t.Id, out _);

                    // if for some reason this logic exits without the slotReleased delegate and Complete() being invoked,
                    // the file will get stuck in the queue and prevent any further uploads to the user. be extra cautious
                    // and ensure it gets removed
                    Queue.TryComplete(username: t.Username, filename: t.Filename);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to finalize upload of {Filename} to {Username}: {Message}", t.Filename, t.Username, ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        ///     Enqueues the requested file.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="filename">The local filename of the requested file.</param>
        /// <returns>The operation context.</returns>
        public async Task<Transfer> EnqueueAsync(string username, string filename)
        {
            var lockName = $"{nameof(EnqueueAsync)}:{username}:{filename}";

            if (!Locks.TryAdd(lockName, true))
            {
                Log.Debug("Ignoring concurrent invocation; lock {LockName} already held", nameof(EnqueueAsync), lockName);
                return null;
            }

            Guid id = Guid.NewGuid();

            try
            {
                Log.Debug("Acquired lock {LockName}", lockName);

                Log.Information("Upload of {Filename} to {Username} requested", filename, username);

                using var context = ContextFactory.CreateDbContext();

                /*
                    first, get all past uploads to this user for this filename
                */
                var existingRecords = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Upload)
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
                    Log.Information("Upload of {Filename} to {Username} is already queued or is in progress (ids: {Ids})", filename, username, string.Join(", ", existingInProgressRecords));
                    return null;
                }

                /*
                    no existing transfers. next, check to see if they are requesting a file we are sharing

                    we do this after checking the database because database I/O is "cheaper" than disk and potentially
                    network (if we check a relay)
                */
                string host = default;
                string localFilename = default;
                long localFileLength = default;

                try
                {
                    (host, localFilename, localFileLength) = await ResolveFileInfoAsync(remoteFilename: filename);
                }
                catch (NotFoundException)
                {
                    Log.Information("Upload of {Filename} to {Username} {Rejected}: {Message}", filename, username, "REJECTED", "File not shared.");
                    throw new DownloadEnqueueException($"File not shared.");
                }

                Log.Debug("Resolved {Remote} to physical file {Physical} on host '{Host}'", filename, localFilename, host);

                /*
                    we're cleared to enqueue! create a new transfer record, and automatically mark any existing records
                    we found as 'removed' to clean up the UI
                */
                var transfer = new Transfer()
                {
                    Id = id,
                    Username = username,
                    Direction = TransferDirection.Upload,
                    Filename = filename, // important! use the remote filename
                    Size = localFileLength,
                    StartOffset = 0, // potentially updated later during handshaking
                    RequestedAt = DateTime.UtcNow,
                    State = TransferStates.Queued | TransferStates.Locally,
                };

                context.Add(transfer);

                foreach (var record in existingRecords.Where(t => !t.Removed))
                {
                    record.Removed = true;
                    context.Update(record);
                    Log.Debug("Marked existing upload record {Filename} to {Username} removed (id: {Id})", filename, username, record.Id);
                }

                context.SaveChanges();

                Log.Information("Successfully enqueued upload of {Filename} to {Username} (id: {Id})", filename, username, id);

                // users with uploads must be watched so that we can keep informed of their online status, privileges, and
                // statistics. this is so that we can accurately determine their effective group.
                try
                {
                    if (!Users.IsWatched(username))
                    {
                        await Users.WatchAsync(username);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to watch user {Username}", username);
                }

                /*
                    schedule the upload immediately

                    Task.Run can fail due to thread pool exhaustion or OOM, and this *SHOULD* throw up the chain and
                    fail the enqueue request
                */
                _ = Task.Run(() => UploadAsync(transfer)).ContinueWith(task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        Log.Information("Task for upload of {Filename} to {Username} completed successfully", filename, username);
                        return;
                    }

                    /*
                        things that can cause us to arrive here:

                        * file not found (moved, deleted somehow)
                        * transfer record deleted somehow
                        * transfer record updated so that it's no longer in Queued | Locally
                        * Soulseek.NET already tracking an identical upload (slskd <> Soulseek.NET desync)
                    */
                    Log.Error(task.Exception, "Task for upload of {Filename} to {Username} did not complete successfully: {Error}", filename, username, task.Exception.Message);

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

                return transfer;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue upload of {Filename} to {Username}: {Message}", filename, username, ex.Message);

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
                Locks.TryRemove(lockName, out _);
                Log.Debug("Released lock {LockName}", lockName);
            }
        }

        /// <summary>
        ///     Finds a single upload matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match uploads.</param>
        /// <returns>The found transfer, or default if not found.</returns>
        public Transfer Find(Expression<Func<Transfer, bool>> expression)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                return context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(expression).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to find upload: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Returns a summary of the uploads matching the specified <paramref name="expression"/>. This can be expensive;
        ///     consider caching.
        /// </summary>
        /// <param name="expression">The expression used to select uploads for summarization.</param>
        /// <returns>
        ///     The generated summary, including the number of files and total size in bytes.
        /// </returns>
        public (int Files, long Bytes) Summarize(Expression<Func<Transfer, bool>> expression)
        {
            expression ??= t => true;

            try
            {
                using var context = ContextFactory.CreateDbContext();

                var query = context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(expression)
                    .GroupBy(t => true) // https://stackoverflow.com/a/25489456: The GroupBy(x => true) statement places all items into a single group. The Select statement the allows operations against each group.
                    .Select(t => new
                    {
                        Files = t.Count(),
                        Bytes = t.Sum(x => x.Size),
                    });

                Log.Verbose("{Method} SQL: {@Query}", nameof(Summarize), query.ToQueryString());

                var stats = query.FirstOrDefault();

                return (stats?.Files ?? 0, stats?.Bytes ?? 0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list uploads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Returns a list of all uploads matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match uploads.</param>
        /// <param name="includeRemoved">A value indicating whether to include uploads that have been removed previously.</param>
        /// <returns>The list of uploads matching the specified expression, or all uploads if no expression is specified.</returns>
        public List<Transfer> List(Expression<Func<Transfer, bool>> expression, bool includeRemoved)
        {
            expression ??= t => true;

            try
            {
                using var context = ContextFactory.CreateDbContext();

                return context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(t => !t.Removed || includeRemoved)
                    .Where(expression)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list uploads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Removes <see cref="TransferStates.Completed"/> uploads older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which uploads are eligible for pruning, in minutes.</param>
        /// <param name="stateHasFlag">An optional, additional state by which uploads are filtered for pruning.</param>
        /// <returns>The number of pruned uploads.</returns>
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
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(t => !t.Removed)
                    .Where(t => t.EndedAt.HasValue && t.EndedAt.Value < cutoffDateTime)
                    .Where(t => t.State.HasFlag(stateHasFlag))
                    .ToList();

                foreach (var tx in expired)
                {
                    tx.Removed = true;
                }

                var pruned = context.SaveChanges();

                if (pruned > 0)
                {
                    Log.Debug("Pruned {Count} expired uploads with state {State}", pruned, stateHasFlag);
                }

                return pruned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to prune uploads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Removes the upload matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the upload.</param>
        public void Remove(Guid id)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                var transfer = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(t => t.Id == id)
                    .FirstOrDefault();

                if (transfer == default)
                {
                    throw new NotFoundException($"No upload matching id ${id}");
                }

                if (!transfer.State.HasFlag(TransferStates.Completed))
                {
                    throw new InvalidOperationException($"Invalid attempt to remove an upload before it is complete");
                }

                transfer.Removed = true;

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove upload {Id}: {Message}", id, ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Cancels the upload matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the upload.</param>
        /// <returns>A value indicating whether the upload was successfully cancelled.</returns>
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

        /// <summary>
        ///     Resolves a remote file's location and size from shares and potentially disk.
        /// </summary>
        /// <param name="remoteFilename">The file to resolve.</param>
        /// <returns>The resolved host, filename (on disk), and length.</returns>
        /// <exception cref="NotFoundException">Thrown if the file can't be located, either in a share or on disk.</exception>
        private async Task<(string Host, string Filename, long Length)> ResolveFileInfoAsync(string remoteFilename)
        {
            string host;
            string filename;
            long length;

            /*
                locate the file and the stored details from shares.
                the size returned here will be the size the remote client is expecting.

                throws NotFoundException
            */
            (host, filename, length) = await Shares.ResolveFileAsync(remoteFilename);

            Log.Debug("Resolved shared file {RemoteFilename} to host {Host} and file {ShareFilename} (length: {ShareLength})", remoteFilename, host, filename, length);

            /*
                if the file is hosted locally, do some quick I/O to check to see if the file still exists at the location
                and of the size stored in the scan. the cost is negligible and this will keep transfers that are doomed
                to fail out of our queue.
            */
            if (host == Program.LocalHostName)
            {
                var info = Files.ResolveFileInfo(filename);

                // if the file doesn't exist we can't continue; shares have diverged from disk
                if (!info.Exists)
                {
                    Shares.RequestScan();
                    Log.Warning("The shared file '{File}' could not be located on disk. A share scan should be performed", filename);
                    throw new NotFoundException($"The file '{filename}' could not be located on disk. A share scan should be performed.");
                }

                // shares have diverged from disk, but we *MIGHT* be able to upload this file, if the remote client
                // doesn't care that the size is exact.  we definitely need to re-scan though.
                if (info.Length != length)
                {
                    Log.Warning("The length of shared file '{File}' differs between the share ({ShareSize}) and disk ({DiskSize}). A share scan should be performed", filename, length, info.Length);
                    Shares.RequestScan();
                }

                length = info.Length;
            }
            else if (!OptionsMonitor.CurrentValue.Flags.OptimisticRelayFileInfo)
            {
                /*
                    if the file is hosted on a relay agent and the user has set the pessimistic flag to true,
                    get the file info from the agent.

                    this was, at one time, the default behavior, but users complained that it caused a lot of timeouts
                    so now it is opt-in.
                */
                var (exists, relayLength) = await Relay.GetFileInfoAsync(agentName: host, remoteFilename);

                if (!exists || relayLength <= 0)
                {
                    // todo: force a remote scan
                    throw new NotFoundException($"The file '{remoteFilename}' could not be located on Agent {host}. A share scan should be performed.");
                }

                if (relayLength != length)
                {
                    // todo: force a remote scan
                    Log.Warning("The length of shared file '{File}' on host {Host} differs between the share ({ShareSize}) and disk ({DiskSize}). A share scan should be performed", filename, host, length, relayLength);
                }

                length = relayLength;
            }

            return (host, filename, length);
        }
    }
}