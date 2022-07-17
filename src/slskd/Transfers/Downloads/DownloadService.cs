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
    public interface IDownloadService
    {
        /// <summary>
        ///     Enqueues the requested list of <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        ///     If one file in the specified collection fails, the rest will continue.  An <see cref="AggregateException"/> will be thrown after all files are dispositioned if any throws.
        /// </remarks>
        /// <param name="id"></param>
        /// <param name="username"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        Task EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files);
        Task<int> GetPlaceInQueueAsync(Guid id);
        Task CancelAsync(Guid id);
        Task RemoveAsync(Guid id, bool cancel = false);
        Task<Transfer> FindAsync(Expression<Func<Transfer, bool>> expression);
        Task<List<Transfer>> ListAsync(Expression<Func<Transfer, bool>> expression = null);
    }

    /// <summary>
    ///     Manages downloads.
    /// </summary>
    public class DownloadService : IDownloadService
    {
        public DownloadService(
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient,
            ITransferTracker tracker,
            IDbContextFactory<TransfersDbContext> contextFactory,
            IFTPService ftpClient)
        {
            Client = soulseekClient;
            Tracker = tracker;
            OptionsMonitor = optionsMonitor;
            ContextFactory = contextFactory;
            FTP = ftpClient;
        }

        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private ISoulseekClient Client { get; }
        private ITransferTracker Tracker { get; }
        private IFTPService FTP { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<DownloadService>();

        public Task CancelAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Enqueues the requested list of <paramref name="files"/>.
        /// </summary>
        /// <remarks>
        ///     If one file in the specified collection fails, the rest will continue.  An <see cref="AggregateException"/> will be thrown after all files are dispositioned if any throws.
        /// </remarks>
        /// <param name="username"></param>
        /// <param name="files"></param>
        /// <returns></returns>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        public async Task EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files)
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

                    var transfer = new Transfer()
                    {
                        Id = Guid.NewGuid(),
                        Username = username,
                        Direction = TransferDirection.Download,
                        Filename = file.Filename,
                        Size = file.Size,
                        StartOffset = 0,
                        RequestedAt = DateTime.UtcNow,
                    };

                    // persist the transfer to the database so we have a record that it was attempted
                    using var context = ContextFactory.CreateDbContext();
                    context.Add(transfer);
                    await context.SaveChangesAsync();

                    var waitUntilEnqueue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var cts = new CancellationTokenSource();

                    async Task UpdateAndSaveChangesAsync(Transfer transfer)
                    {
                        using var context = ContextFactory.CreateDbContext();
                        context.Update(transfer);
                        await context.SaveChangesAsync();
                    }

                    // this task does the actual work of downloading the file. if the transfer is successfully queued remotely,
                    // the waitUntilEnqueue completion source is set, which yields execution to below so we can return and let
                    // the caller know.
                    var downloadTask = Task.Run(async () =>
                    {
                        using var rateLimiter = new RateLimiter(250);

                        var completedTransfer = await Client.DownloadAsync(
                            username: username,
                            remoteFilename: file.Filename,
                            outputStreamFactory: () => GetLocalFileStream(file.Filename, OptionsMonitor.CurrentValue.Directories.Incomplete),
                            size: file.Size,
                            startOffset: 0,
                            token: null,
                            cancellationToken: cts.Token,
                            options: new TransferOptions(
                                disposeOutputStreamOnCompletion: true,
                                stateChanged: (args) =>
                                {
                                    Log.Debug("Download of {Filename} from user {Username} changed state from {Previous} to {New}", file.Filename, username, args.PreviousState, args.Transfer.State);

                                    Tracker.AddOrUpdate(args.Transfer, cts);

                                    transfer = transfer.WithSoulseekTransfer(args.Transfer);
                                    // todo: broadcast
                                    _ = UpdateAndSaveChangesAsync(transfer);

                                    if (args.Transfer.State.HasFlag(TransferStates.Queued) || args.Transfer.State == TransferStates.Initializing)
                                    {
                                        waitUntilEnqueue.TrySetResult(true);
                                    }
                                },
                                progressUpdated: (args) => rateLimiter.Invoke(() =>
                                {
                                    transfer = transfer.WithSoulseekTransfer(args.Transfer);
                                    // todo: broadcast
                                    _ = UpdateAndSaveChangesAsync(transfer);

                                    Tracker.AddOrUpdate(args.Transfer, cts);
                                })));

                        transfer = transfer.WithSoulseekTransfer(completedTransfer);
                        // todo: broadcast
                        await UpdateAndSaveChangesAsync(transfer);

                        // this would be the ideal place to hook in a generic post-download task processor
                        // for now, we'll just carry out hard coded behavior
                        MoveFile(file.Filename, OptionsMonitor.CurrentValue.Directories.Incomplete, OptionsMonitor.CurrentValue.Directories.Downloads);

                        if (OptionsMonitor.CurrentValue.Integration.Ftp.Enabled)
                        {
                            _ = FTP.UploadAsync(file.Filename.ToLocalFilename(OptionsMonitor.CurrentValue.Directories.Downloads));
                        }
                    });

                    // wait until either the waitUntilEnqueue task completes because the download was successfully queued, or the
                    // downloadTask throws due to an error prior to successfully queueing.
                    var task = await Task.WhenAny(waitUntilEnqueue.Task, downloadTask);

                    // if the download task completed first it is a very good indication that it threw an exception or was otherwise
                    // not successful. try to figure out why and update everything to reflect the failure, but continue processing the batch
                    if (task == downloadTask)
                    {
                        Exception ex = downloadTask.Exception;

                        // todo: figure out why this needs to be unwrapped just for this one case.  is this always an aggregate?
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
                        transfer.Exception = ex.Message;
                        transfer.State = TransferStates.Completed | TransferStates.Errored;
                        transfer.EndedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        transfer.EnqueuedAt = DateTime.UtcNow;
                        Log.Debug("Successfully enqueued {Filename} from user {Username}", file.Filename, username);
                    }

                    await context.SaveChangesAsync();
                }

                if (thrownExceptions.Any())
                {
                    throw new AggregateException(thrownExceptions);
                }

                Log.Information("Successfully enqueued {Count} files from user {Username}", files.Count(), username);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download one or more files from user {Username}: {Message}", username, ex.Message);
                throw;
            }
        }

        public Task<Transfer> FindAsync(Expression<Func<Transfer, bool>> expression)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetPlaceInQueueAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<List<Transfer>> ListAsync(Expression<Func<Transfer, bool>> expression = null)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAsync(Guid id, bool cancel = false)
        {
            throw new NotImplementedException();
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
