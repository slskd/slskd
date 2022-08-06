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
    using slskd.Shares;
    using slskd.Users;

    /// <summary>
    ///     Manages uploads.
    /// </summary>
    public class UploadService : IUploadService
    {
        public UploadService(
            IUserService userService,
            ISoulseekClient soulseekClient,
            IOptionsMonitor<Options> optionsMonitor,
            IShareService shareService,
            IDbContextFactory<TransfersDbContext> contextFactory)
        {
            Users = userService;
            Client = soulseekClient;
            Shares = shareService;
            ContextFactory = contextFactory;

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

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; } = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private ISoulseekClient Client { get; set; }
        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<UploadService>();
        private IShareService Shares { get; set; }
        private IUserService Users { get; set; }

        /// <summary>
        ///     Adds the specified <paramref name="transfer"/>. Supersedes any existing record for the same file and username.
        /// </summary>
        /// <remarks>This should generally not be called; use <see cref="EnqueueAsync(string, string)"/> instead.</remarks>
        /// <param name="transfer"></param>
        /// <returns></returns>
        public async Task AddOrSupersedeAsync(Transfer transfer)
        {
            var existing = await FindAsync(t => t.Username == transfer.Username && t.Filename == transfer.Filename);

            using var context = await ContextFactory.CreateDbContextAsync();

            if (existing != default)
            {
                existing.Removed = true;
                context.Update(existing);
            }

            context.Add(transfer);
            await context.SaveChangesAsync();
        }

        /// <summary>
        ///     Enqueues the requested file.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="filename">The local filename of the requested file.</param>
        /// <returns>The operation context.</returns>
        public async Task EnqueueAsync(string username, string filename)
        {
            // remote clients might sometimes re-request downloads to check the status. don't try to add the download again if it
            // is already tracked.
            if (await ExistsAsync(t => t.Username == username && t.Filename == filename))
            {
                return;
            }

            string localFilename;
            FileInfo fileInfo = default;

            Log.Information("[{Context}] {Username} requested {Filename}]", "UPLOAD REQUESTED", username, filename);

            try
            {
                localFilename = (await Shares.ResolveFilenameAsync(filename)).ToLocalOSPath();

                fileInfo = new FileInfo(localFilename);

                if (!fileInfo.Exists)
                {
                    throw new NotFoundException();
                }
            }
            catch (NotFoundException)
            {
                Log.Information("[{Context}] {Filename} for {Username}: file not found", "UPLOAD REJECTED", username, filename);
                throw new DownloadEnqueueException($"File not shared.");
            }

            var id = Guid.NewGuid();

            var transfer = new Transfer()
            {
                Id = id,
                Username = username,
                Direction = TransferDirection.Upload,
                Filename = localFilename,
                Size = fileInfo.Length,
                StartOffset = 0, // potentially updated later during handshaking
                RequestedAt = DateTime.UtcNow,
                EnqueuedAt = DateTime.UtcNow,
            };

            // persist the transfer to the database so we have a record that it was attempted
            await AddOrSupersedeAsync(transfer);

            // create a new cancellation token source so that we can cancel the upload from the UI.
            var cts = new CancellationTokenSource();
            CancellationTokens.TryAdd(id, cts);

            // accept all download requests, and begin the upload immediately. normally there would be an internal queue, and
            // uploads would be handled separately.
            _ = Task.Run(async () =>
            {
                using var rateLimiter = new RateLimiter(250);

                var topts = new TransferOptions(
                    stateChanged: (args) =>
                    {
                        Log.Debug("Upload of {Filename} to user {Username} changed state from {Previous} to {New}", localFilename, username, args.PreviousState, args.Transfer.State);

                        transfer = transfer.WithSoulseekTransfer(args.Transfer);
                        // todo: broadcast
                        _ = UpdateAsync(transfer);

                        if (args.Transfer.State.HasFlag(TransferStates.Queued))
                        {
                            Queue.Enqueue(args.Transfer.Username, args.Transfer.Filename);
                        }
                    },
                    progressUpdated: (args) => rateLimiter.Invoke(() =>
                    {
                        transfer = transfer.WithSoulseekTransfer(args.Transfer);
                        // todo: broadcast
                        _ = UpdateAsync(transfer);
                    }),
                    governor: (tx, req, ct) => Governor.GetBytesAsync(tx.Username, req, ct),
                    reporter: (tx, att, grant, act) => Governor.ReturnBytes(tx.Username, att, grant, act),
                    slotAwaiter: (tx, ct) => Queue.AwaitStartAsync(tx.Username, tx.Filename),
                    slotReleased: (tx) => Queue.Complete(tx.Username, tx.Filename));

                // users with uploads must be watched so that we can keep informed of their online status, privileges, and
                // statistics. this is so that we can accurately determine their effective group.
                if (!Users.IsWatched(username))
                {
                    await Users.WatchAsync(username);
                }

                using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                var completedTransfer = await Client.UploadAsync(username, filename, fileInfo.FullName, options: topts, cancellationToken: cts.Token);

                transfer = transfer.WithSoulseekTransfer(completedTransfer);
                //todo: broadcast
                await UpdateAsync(transfer);
            }).ContinueWith(t =>
            {
                Log.Information("[{Context}] {Filename} for {Username}: {Message}", "UPLOAD FAILED", username, filename, t.Exception?.Message);
            }, TaskContinuationOptions.NotOnRanToCompletion); // fire and forget
        }

        /// <summary>
        ///     Returns a value indicating whether an upload matching the specified <paramref name="expression"/> exists.
        /// </summary>
        /// <param name="expression">The expression used to match uploads.</param>
        /// <returns>A value indicating whether an upload matching the specified expression exists.</returns>
        public async Task<bool> ExistsAsync(Expression<Func<Transfer, bool>> expression)
        {
            return (await FindAsync(expression)) != default;
        }

        /// <summary>
        ///     Finds a single upload matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match uploads.</param>
        /// <returns>The found transfer, or default if not found.</returns>
        public async Task<Transfer> FindAsync(Expression<Func<Transfer, bool>> expression)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(expression).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to find upload: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Returns a list of all uploads matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match uploads.</param>
        /// <param name="includeRemoved">Optionally include uploads that have been removed previously.</param>
        /// <returns>The list of uploads matching the specified expression, or all uploads if no expression is specified.</returns>
        public async Task<List<Transfer>> ListAsync(Expression<Func<Transfer, bool>> expression = null, bool includeRemoved = false)
        {
            expression ??= t => true;

            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                return await context.Transfers
                    .AsNoTracking()
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(t => !t.Removed || includeRemoved)
                    .Where(expression)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list uploads: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        ///     Removes the upload matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the upload.</param>
        /// <returns></returns>
        public async Task RemoveAsync(Guid id)
        {
            try
            {
                using var context = await ContextFactory.CreateDbContextAsync();
                var transfer = await context.Transfers
                    .Where(t => t.Direction == TransferDirection.Upload)
                    .Where(t => t.Id == id)
                    .FirstOrDefaultAsync();

                if (transfer == default)
                {
                    throw new NotFoundException($"No upload matching id ${id}");
                }

                if (!transfer.State.HasFlag(TransferStates.Completed))
                {
                    throw new InvalidOperationException($"Invalid attempt to remove an upload before it is complete");
                }

                transfer.Removed = true;

                await context.SaveChangesAsync();
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
    }
}