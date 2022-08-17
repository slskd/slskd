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
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using slskd.Integrations.FTP;

    /// <summary>
    ///     Manages downloads.
    /// </summary>
    public class DownloadService : IDownloadService
    {
        public DownloadService(
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient,
            IDbContextFactory<TransfersDbContext> contextFactory,
            IFTPService ftpClient)
        {
            Client = soulseekClient;
            OptionsMonitor = optionsMonitor;
            ContextFactory = contextFactory;
            FTP = ftpClient;
        }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; } = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private ISoulseekClient Client { get; }
        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private IFTPService FTP { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<DownloadService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }

        /// <summary>
        ///     Adds the specified <paramref name="transfer"/>. Supersedes any existing record for the same file and username.
        /// </summary>
        /// <remarks>This should generally not be called; use <see cref="EnqueueAsync(string, IEnumerable(string Filename, long Size)})"/> instead.</remarks>
        /// <param name="transfer"></param>
        /// <returns></returns>
        public async Task AddOrSupersedeAsync(Transfer transfer)
        {
            using var context = await ContextFactory.CreateDbContextAsync();

            var existing = await context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => t.Username == transfer.Username)
                    .Where(t => t.Filename == transfer.Filename)
                    .Where(t => !t.Removed)
                    .FirstOrDefaultAsync();

            if (existing != default)
            {
                Log.Debug("Superseding transfer record for {Filename} from {Username}", transfer.Filename, transfer.Username);
                existing.Removed = true;
            }

            context.Add(transfer);
            await context.SaveChangesAsync();
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
                        await AddOrSupersedeAsync(transfer);

                        var cts = new CancellationTokenSource();
                        CancellationTokens.TryAdd(id, cts);

                        var waitUntilEnqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                        // this task does the actual work of downloading the file. if the transfer is successfully queued
                        // remotely, the waitUntilEnqueue completion source is set, which yields execution to below so we can
                        // return and let the caller know.
                        var downloadTask = Task.Run(async () =>
                        {
                            using var rateLimiter = new RateLimiter(250);

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

                                        // todo: broadcast
                                        _ = UpdateAsync(transfer);
                                    },
                                    progressUpdated: (args) => rateLimiter.Invoke(() =>
                                    {
                                        transfer = transfer.WithSoulseekTransfer(args.Transfer);
                                        // todo: broadcast
                                        _ = UpdateAsync(transfer);
                                    }));

                                var completedTransfer = await Client.DownloadAsync(
                                    username: username,
                                    remoteFilename: file.Filename,
                                    outputStreamFactory: () => GetLocalFileStream(file.Filename, OptionsMonitor.CurrentValue.Directories.Incomplete),
                                    size: file.Size,
                                    startOffset: 0,
                                    token: null,
                                    cancellationToken: cts.Token,
                                    options: topts);

                                transfer = transfer.WithSoulseekTransfer(completedTransfer);
                                // todo: broadcast
                                await UpdateAsync(transfer);

                                // this would be the ideal place to hook in a generic post-download task processor for now, we'll
                                // just carry out hard coded behavior. these carry the risk of failing the transfer, and i could
                                // argue both ways for that being the correct behavior. revisit this later.
                                MoveFile(file.Filename, OptionsMonitor.CurrentValue.Directories.Incomplete, OptionsMonitor.CurrentValue.Directories.Downloads);

                                if (OptionsMonitor.CurrentValue.Integration.Ftp.Enabled)
                                {
                                    _ = FTP.UploadAsync(file.Filename.ToLocalFilename(OptionsMonitor.CurrentValue.Directories.Downloads));
                                }
                            }
                            catch (TaskCanceledException ex)
                            {
                                transfer.Exception = ex.Message;
                                transfer.State = TransferStates.Completed | TransferStates.Cancelled;

                                throw;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Download of {Filename} from user {Username} failed: {Message}", file.Filename, username, ex.Message);

                                transfer.Exception = ex.Message;
                                transfer.State = TransferStates.Completed | TransferStates.Errored;

                                throw;
                            }
                            finally
                            {
                                transfer.EndedAt = DateTime.UtcNow;
                                // todo: broadcast
                                await UpdateAsync(transfer);

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

                        // this may be redundant, as the record would have been updated inside of the
                        // state handler, just before the task completion source was signalled. do it anyway.
                        // todo: broadcast
                        await UpdateAsync(transfer);
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
        public async Task<Transfer> FindAsync(Expression<Func<Transfer, bool>> expression)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(expression).FirstOrDefaultAsync();
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
                using var context = await ContextFactory.CreateDbContextAsync();
                var transfer = await context.Transfers.FindAsync(id);

                if (transfer == default)
                {
                    throw new NotFoundException($"No download matching id ${id}");
                }

                var place = await Client.GetDownloadPlaceInQueueAsync(transfer.Username, transfer.Filename);

                transfer.PlaceInQueue = place;
                await context.SaveChangesAsync();

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
        public async Task<List<Transfer>> ListAsync(Expression<Func<Transfer, bool>> expression = null, bool includeRemoved = false)
        {
            expression ??= t => true;

            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => !t.Removed || includeRemoved)
                    .Where(expression)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list downloads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Removes the download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the download.</param>
        /// <returns></returns>
        public async Task RemoveAsync(Guid id)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var transfer = await context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (transfer == default)
                {
                    throw new NotFoundException($"No download matching id ${id}");
                }

                if (!transfer.State.HasFlag(TransferStates.Completed))
                {
                    throw new InvalidOperationException($"Invalid attempt to remove a download before it is complete");
                }

                transfer.Removed = true;

                await context.SaveChangesAsync();
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
        ///     Updates the specified <paramref name="transfer"/>.
        /// </summary>
        /// <param name="transfer">The transfer to update.</param>
        /// <returns>The operation context.</returns>
        public async Task UpdateAsync(Transfer transfer)
        {
            using var context = await ContextFactory.CreateDbContextAsync();
            context.Update(transfer);
            await context.SaveChangesAsync();
        }

        private static FileStream GetLocalFileStream(string remoteFilename, string saveDirectory)
        {
            var localFilename = remoteFilename.ToLocalFilename(baseDirectory: saveDirectory);
            var path = Path.GetDirectoryName(localFilename);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return new FileStream(localFilename, FileMode.Create);
        }

        private static void MoveFile(string filename, string sourceDirectory, string destinationDirectory)
        {
            var sourceFilename = filename.ToLocalFilename(sourceDirectory);
            var destinationFilename = filename.ToLocalFilename(destinationDirectory);

            var destinationPath = Path.GetDirectoryName(destinationFilename);

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            File.Move(sourceFilename, destinationFilename, overwrite: true);

            if (!Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(sourceFilename)).Any())
            {
                Directory.Delete(Path.GetDirectoryName(sourceFilename));
            }
        }
    }
}