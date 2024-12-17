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
        Task EnqueueAsync(string username, string filename);

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
        /// <param name="state">An optional, additional state by which uploads are filtered for pruning.</param>
        /// <returns>The number of pruned uploads.</returns>
        int Prune(int age, TransferStates state = TransferStates.Completed);

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
        ///     Enqueues the requested file.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="filename">The local filename of the requested file.</param>
        /// <returns>The operation context.</returns>
        public async Task EnqueueAsync(string username, string filename)
        {
            string host = default;
            string localFilename = default;
            long resolvedFileLength = default;
            long localFileLength = default;

            Log.Information("[{Context}] {Username} requested {Filename}", "UPLOAD REQUESTED", username, filename);

            try
            {
                (host, localFilename, resolvedFileLength) = await Shares.ResolveFileAsync(filename);

                Log.Debug("Resolved file {RemoteFilename} to host {Host} and file {LocalFilename}", filename, host, localFilename);

                if (host == Program.LocalHostName)
                {
                    // if it's local, do a quick check to see if it exists to spare the caller from queueing up if the transfer is
                    // doomed to fail. for remote files, take a leap of faith.
                    var info = Files.ResolveFileInfo(localFilename);

                    if (!info.Exists)
                    {
                        Shares.RequestScan();
                        throw new NotFoundException($"The file '{localFilename}' could not be located on disk. A share scan should be performed.");
                    }

                    localFileLength = info.Length;
                }
                else
                {
                    var (exists, length) = await Relay.GetFileInfoAsync(agentName: host, filename);

                    if (!exists || length <= 0)
                    {
                        // todo: force a remote scan
                        throw new NotFoundException($"The file '{localFilename}' could not be located on Agent {host}. A share scan should be performed.");
                    }

                    localFileLength = length;
                }
            }
            catch (NotFoundException)
            {
                Log.Information("[{Context}] {Filename} for {Username}: file not found", "UPLOAD REJECTED", username, filename);
                throw new DownloadEnqueueException($"File not shared.");
            }

            Log.Information("Resolved {Remote} to physical file {Physical} on host '{Host}'", filename, localFilename, host);

            if (localFileLength != resolvedFileLength)
            {
                // todo: should we fail the transfer? the file changed on disk since the last scan. i guess not? since the caller doesn't provide a size
                Shares.RequestScan();
                Log.Warning("Resolved size for {Remote} of {Resolved} doesn't match actual size {Actual}", filename, resolvedFileLength, localFileLength);
            }

            // find existing records for this username and file that haven't been removed from the UI
            var existingRecords = List(t => t.Username == username && t.Filename == localFilename, includeRemoved: false);

            // check whether any of these records is in a non-complete state and bail out if so
            if (existingRecords.Any(t => !t.State.HasFlag(TransferStates.Completed)))
            {
                Log.Information("Upload {Filename} to {Username} is already queued or in progress", localFilename, username);
                return;
            }

            var id = Guid.NewGuid();

            var transfer = new Transfer()
            {
                Id = id,
                Username = username,
                Direction = TransferDirection.Upload,
                Filename = localFilename,
                Size = localFileLength,
                StartOffset = 0, // potentially updated later during handshaking
                RequestedAt = DateTime.UtcNow,
            };

            // persist the transfer to the database so we have a record that it was attempted
            AddOrSupersede(transfer);

            // create a new cancellation token source so that we can cancel the upload from the UI.
            var cts = new CancellationTokenSource();
            CancellationTokens.TryAdd(id, cts);

            // accept all download requests, and begin the upload immediately. normally there would be an internal queue, and
            // uploads would be handled separately.
            _ = Task.Run(async () =>
            {
                using var rateLimiter = new RateLimiter(250, flushOnDispose: true);
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

                try
                {
                    // users with uploads must be watched so that we can keep informed of their online status, privileges, and
                    // statistics. this is so that we can accurately determine their effective group.
                    if (!Users.IsWatched(username))
                    {
                        await Users.WatchAsync(username);
                    }

                    var topts = new TransferOptions(
                        stateChanged: (args) =>
                        {
                            Log.Debug("Upload of {Filename} to user {Username} changed state from {Previous} to {New}", localFilename, username, args.PreviousState, args.Transfer.State);

                            if (Application.IsShuttingDown)
                            {
                                Log.Debug("Upload update of {Filename} to {Username} not persisted; app is shutting down", filename, username);
                                return;
                            }

                            transfer = transfer.WithSoulseekTransfer(args.Transfer);

                            if (args.Transfer.State.HasFlag(TransferStates.Queued))
                            {
                                Queue.Enqueue(args.Transfer.Username, args.Transfer.Filename);
                                transfer.EnqueuedAt = DateTime.UtcNow;
                            }

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

                    if (host == Program.LocalHostName)
                    {
                        var completedTransfer = await Client.UploadAsync(
                            username,
                            filename,
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

                        transfer = transfer.WithSoulseekTransfer(completedTransfer);
                    }
                    else
                    {
                        var completedTransfer = await Client.UploadAsync(
                            username,
                            filename,
                            size: localFileLength,
                            inputStreamFactory: (startOffset) => Relay.GetFileStreamAsync(agentName: host, filename, startOffset, id),
                            options: topts,
                            cancellationToken: cts.Token);

                        Relay.TryCloseFileStream(host, id);

                        transfer = transfer.WithSoulseekTransfer(completedTransfer);
                    }

                    // explicitly dispose the rate limiter to prevent updates from it beyond this point, which may overwrite the
                    // final state
                    rateLimiter.Dispose();

                    // todo: broadcast
                    SynchronizedUpdate(transfer, cancellable: false);

                    EventBus.Raise(new UploadFileCompleteEvent
                    {
                        Timestamp = transfer.EndedAt.Value,
                        LocalFilename = localFilename,
                        RemoteFilename = filename,
                        Transfer = transfer,
                    });
                }
                catch (OperationCanceledException ex)
                {
                    transfer.EndedAt = DateTime.UtcNow;
                    transfer.Exception = ex.Message;
                    transfer.State = TransferStates.Completed | TransferStates.Cancelled;

                    // todo: broadcast
                    SynchronizedUpdate(transfer, cancellable: false);

                    Relay.TryCloseFileStream(host, id, ex);

                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Upload of {Filename} to user {Username} failed: {Message}", filename, username, ex.Message);

                    transfer.EndedAt = DateTime.UtcNow;
                    transfer.Exception = ex.Message;
                    transfer.State = TransferStates.Completed | TransferStates.Errored;

                    // todo: broadcast
                    SynchronizedUpdate(transfer, cancellable: false);

                    Relay.TryCloseFileStream(host, id, ex);
                    throw;
                }
                finally
                {
                    CancellationTokens.TryRemove(id, out _);
                }
            });
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
                    .GroupBy(t => true) // https://stackoverflow.com/a/25489456
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
        /// <param name="state">An optional, additional state by which uploads are filtered for pruning.</param>
        /// <returns>The number of pruned uploads.</returns>
        public int Prune(int age, TransferStates state = TransferStates.Completed)
        {
            if (!state.HasFlag(TransferStates.Completed))
            {
                throw new ArgumentException($"State must include {TransferStates.Completed}", nameof(state));
            }

            try
            {
                using var context = ContextFactory.CreateDbContext();

                var cutoffDateTime = DateTime.UtcNow.AddMinutes(-age);

                var expired = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(t => !t.Removed)
                    .Where(t => t.EndedAt.HasValue && t.EndedAt.Value < cutoffDateTime)

                    // note: don't try HasFlag() here: https://github.com/dotnet/efcore/issues/20094
                    // this won't work because the state is stored as a comma separated string (which we've done deliberately)
                    // and EF won't do the necessary work to generate the required SQL
                    .Where(t => t.State == state)
                    .ToList();

                foreach (var tx in expired)
                {
                    tx.Removed = true;
                }

                var pruned = context.SaveChanges();

                if (pruned > 0)
                {
                    Log.Debug("Pruned {Count} expired uploads with state {State}", pruned, state);
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
    }
}