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
        Task EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files);

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
        /// <param name="state">An optional, additional state by which downloads are filtered for pruning.</param>
        /// <returns>The number of pruned downloads.</returns>
        int Prune(int age, TransferStates state = TransferStates.Completed);

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
            Events = eventBus;
        }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; } = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private ISoulseekClient Client { get; }
        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private IFTPService FTP { get; }
        private FileService Files { get; }
        private IRelayService Relay { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<DownloadService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private EventBus Events { get; }

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
        public Task EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username is required", nameof(username));
            }

            if (!files.Any())
            {
                throw new ArgumentException("At least one file is require", nameof(files));
            }

            return EnqueueAsyncInternal(username, files);

            async Task EnqueueAsyncInternal(string username, IEnumerable<(string Filename, long Size)> files)
            {
                try
                {
                    Log.Information("Downloading {Count} files from user {Username}", files.Count(), username);

                    Log.Debug("Priming connection for user {Username}", username);
                    await Client.ConnectToUserAsync(username, invalidateCache: false);
                    Log.Debug("Connection for user '{Username}' primed", username);

                    var thrownExceptions = new List<Exception>();

                    foreach (var file in files)
                    {
                        Log.Debug("Attempting to enqueue {Filename} from user {Username}", file.Filename, username);

                        var id = Guid.NewGuid();

                        var transfer = new Transfer()
                        {
                            Id = id,
                            Username = username,
                            Direction = TransferDirection.Download,
                            Filename = file.Filename,
                            Size = file.Size,
                            StartOffset = 0,
                            RequestedAt = DateTime.UtcNow,
                        };

                        // persist the transfer to the database so we have a record that it was attempted
                        AddOrSupersede(transfer);

                        var cts = new CancellationTokenSource();
                        CancellationTokens.TryAdd(id, cts);

                        var waitUntilEnqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        // this task does the actual work of downloading the file. if the transfer is successfully queued
                        // remotely, the waitUntilEnqueue completion source is set, which yields execution to below so we can
                        // return and let the caller know.
                        var downloadTask = Task.Run(async () =>
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
                                var topts = new TransferOptions(
                                    disposeOutputStreamOnCompletion: true,
                                    stateChanged: (args) =>
                                    {
                                        Log.Debug("Download of {Filename} from user {Username} changed state from {Previous} to {New}", file.Filename, username, args.PreviousState, args.Transfer.State);

                                        if (Application.IsShuttingDown)
                                        {
                                            Log.Debug("Download update of {Filename} to {Username} not persisted; app is shutting down", file.Filename, username);
                                            return;
                                        }

                                        transfer = transfer.WithSoulseekTransfer(args.Transfer);

                                        if ((args.Transfer.State.HasFlag(TransferStates.Queued) && args.Transfer.State.HasFlag(TransferStates.Remotely)) || args.Transfer.State == TransferStates.Initializing)
                                        {
                                            transfer.EnqueuedAt = DateTime.UtcNow;
                                            waitUntilEnqueue.TrySetResult(true);
                                        }

                                        SynchronizedUpdate(transfer);
                                    },
                                    progressUpdated: (args) => rateLimiter.Invoke(() =>
                                    {
                                        transfer = transfer.WithSoulseekTransfer(args.Transfer);

                                        // todo: broadcast
                                        SynchronizedUpdate(transfer);
                                    }));

                                var completedTransfer = await Client.DownloadAsync(
                                    username: username,
                                    remoteFilename: file.Filename,
                                    outputStreamFactory: () => Task.FromResult(
                                        Files.CreateFile(
                                            filename: file.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Incomplete),
                                            options: new CreateFileOptions
                                            {
                                                Access = System.IO.FileAccess.Write,
                                                Mode = System.IO.FileMode.Create, // overwrites file if it exists
                                                Share = System.IO.FileShare.None, // exclusive access for the duration of the download
                                                UnixCreateMode = !string.IsNullOrEmpty(OptionsMonitor.CurrentValue.Permissions.File.Mode)
                                                    ? OptionsMonitor.CurrentValue.Permissions.File.Mode.ToUnixFileMode()
                                                    : null,
                                            })),
                                    size: file.Size,
                                    startOffset: 0,
                                    token: null,
                                    cancellationToken: cts.Token,
                                    options: topts);

                                // explicitly dispose the rate limiter to prevent updates from it
                                // beyond this point, which may overwrite the final state
                                rateLimiter.Dispose();

                                transfer = transfer.WithSoulseekTransfer(completedTransfer);

                                // todo: broadcast
                                SynchronizedUpdate(transfer, cancellable: false);

                                // this would be the ideal place to hook in a generic post-download task processor for now, we'll
                                // just carry out hard coded behavior. these carry the risk of failing the transfer, and i could
                                // argue both ways for that being the correct behavior. revisit this later.
                                var finalFilename = Files.MoveFile(
                                    sourceFilename: file.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Incomplete),
                                    destinationDirectory: System.IO.Path.GetDirectoryName(file.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Downloads)),
                                    overwrite: false,
                                    deleteSourceDirectoryIfEmptyAfterMove: true);

                                Log.Debug("Moved file to {Destination}", finalFilename);

                                if (OptionsMonitor.CurrentValue.Relay.Enabled)
                                {
                                    _ = Relay.NotifyFileDownloadCompleteAsync(finalFilename);
                                }

                                // todo: raise DOWNLOAD_FILE_COMPLETE
                                Events.Downloads.FileComplete?.Invoke()
                                // todo: determine if there are any pending transfers in this same directory, and if not raise DOWNLOAD_DIRECTORY_COMPLETE

                                if (OptionsMonitor.CurrentValue.Integration.Ftp.Enabled)
                                {
                                    _ = FTP.UploadAsync(finalFilename);
                                }
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
                                Log.Error(ex, "Download of {Filename} from user {Username} failed: {Message}", file.Filename, username, ex.Message);

                                transfer.EndedAt = DateTime.UtcNow;
                                transfer.Exception = ex.Message;
                                transfer.State = TransferStates.Completed | TransferStates.Errored;

                                // todo: broadcast
                                SynchronizedUpdate(transfer, cancellable: false);

                                throw;
                            }
                            finally
                            {
                                CancellationTokens.TryRemove(id, out _);
                            }
                        });

                        // wait until either the waitUntilEnqueue task completes because the download was successfully queued, or
                        // the downloadTask throws due to an error prior to successfully queueing.
                        var task = await Task.WhenAny(waitUntilEnqueue.Task, downloadTask);

                        // if the download task completed first it is a very good indication that it threw an exception or was
                        // otherwise not successful. try to figure out why and update everything to reflect the failure, but
                        // continue processing the batch
                        if (task == downloadTask)
                        {
                            Exception ex = downloadTask.Exception;

                            // todo: figure out why this needs to be unwrapped just for this one case. is this always an aggregate?
                            if (ex is AggregateException aggEx)
                            {
                                var rejected = aggEx.InnerExceptions.Where(e => e is TransferRejectedException) ?? Enumerable.Empty<Exception>();
                                if (rejected.Any())
                                {
                                    ex = rejected.First();
                                }
                            }

                            Log.Error("Failed to download {Filename} from {Username}: {Message}", file.Filename, username, ex.Message);
                            thrownExceptions.Add(ex);
                        }
                        else
                        {
                            Log.Debug("Successfully enqueued {Filename} from user {Username}", file.Filename, username);
                        }
                    }

                    if (thrownExceptions.Any())
                    {
                        throw new AggregateException(thrownExceptions);
                    }

                    Log.Information("Successfully enqueued {Count} file(s) from user {Username}", files.Count(), username);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to download one or more files from user {Username}: {Message}", username, ex.Message);
                    throw;
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
        /// <param name="state">An optional, additional state by which downloads are filtered for pruning.</param>
        /// <returns>The number of pruned downloads.</returns>
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
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => !t.Removed)
                    .Where(t => t.EndedAt.HasValue && t.EndedAt.Value < cutoffDateTime)
                    .Where(t => t.State == state) // https://github.com/dotnet/efcore/issues/20094
                    .ToList();

                foreach (var tx in expired)
                {
                    tx.Removed = true;
                }

                var pruned = context.SaveChanges();

                if (pruned > 0)
                {
                    Log.Debug("Pruned {Count} expired downloads with state {State}", pruned, state);
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