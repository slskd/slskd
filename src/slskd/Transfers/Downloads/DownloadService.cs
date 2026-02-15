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
        Task<(List<Transfer> Enqueued, List<string> Failed)> EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files, CancellationToken cancellationToken = default);

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
        /// <param name="states">One or more states by which downloads are filtered for pruning.</param>
        /// <returns>The number of pruned downloads.</returns>
        int Prune(int age, params TransferStates[] states);

        /// <summary>
        ///     Removes the completed download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the download.</param>
        /// <returns>A value indicating whether the record was removed.</returns>
        bool Remove(Guid id);

        /// <summary>
        ///     Removes all completed downloads matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="expression">The expression used to match downloads.</param>
        /// <returns>The number of records removed.</returns>
        int Remove(Expression<Func<Transfer, bool>> expression);

        /// <summary>
        ///     Cancels the download matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the download.</param>
        /// <returns>A value indicating whether the download was successfully cancelled.</returns>
        bool TryCancel(Guid id);

        /// <summary>
        ///     Fails the download matching the specified <paramref name="id"/> with the specified <paramref name="exception"/>,
        ///     and sets the final state accordingly.
        /// </summary>
        /// <remarks>
        ///     This method is designed to be idempotent, meaning subsequent calls for a given transfer shouldn't change
        ///     the EndedAt or Exception properties if they have already been set. If the transfer State already includes
        ///     the terminal Completed flag, it is unchanged.
        /// </remarks>
        /// <param name="id">The unique identifier for the download.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A value indicating whether the download was successfully failed.</returns>
        bool TryFail(Guid id, Exception exception);

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

            Clock.EveryMinute += (_, _) => Task.Run(() => CleanupEnqueueSemaphoresAsync());
        }

        /// <summary>
        ///     These tokens give users the ability to cancel transfers via the UI (or API).
        ///
        ///     The lifecycle is:
        ///
        ///     1. in EnqueueAsync(), just before transfer records are added to the database, these CTS are created and
        ///        added to the dictionary
        ///     2. in EnqueueAsync(), if there was an error or any condition which caused the transfer to not proceed was met,
        ///        the CTS is removed from the dictionary and disposed
        ///     3. in DownloadAsync(), in the finally block after all post-download logic has been executed, the CTS
        ///        is removed and disposed.
        ///
        ///     The CTS should be in the dictionary the entire time the transfer is "in flight", from the first moment it
        ///     comes into being (via database insert) until the eventual download process has completed or failed.
        ///
        ///     Every cancellable operation in this flow needs to use this CTS token either directly or by linking it with another,
        ///     or we risk transfers getting stuck with no way for users to get rid of them.
        /// </summary>
        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; } = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private ISoulseekClient Client { get; }
        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private IFTPService FTP { get; }
        private FileService Files { get; }
        private IRelayService Relay { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<DownloadService>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private EventBus EventBus { get; }

        /// <summary>
        ///     Allow only one enqueue operation for a given user at a time. Entries are added on the fly if they
        ///     don't already exist, and a timer asynchronously cleans the dictionary up.
        /// </summary>
        private ConcurrentDictionary<string, SemaphoreSlim> EnqueueSemaphores { get; } = [];

        /// <summary>
        ///     Synchronizes the cleanup process; after gaining exclusive access over the dictionary, checks each entry
        ///     to see if the containing semaphore can be obtained immediately.  If so, it's not in use and is therefore
        ///     removed from the dictionary and discarded.
        /// </summary>
        private SemaphoreSlim EnqueueSemaphoreSyncRoot { get; } = new SemaphoreSlim(initialCount: 1, maxCount: 1);

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
        /// <param name="username">The username of the remote user.</param>
        /// <param name="files">The list of files to enqueue.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="ArgumentException">Thrown when the username is null or an empty string.</exception>
        /// <exception cref="ArgumentException">Thrown when no files are requested.</exception>
        /// <exception cref="AggregateException">Thrown when at least one of the requested files throws.</exception>
        public async Task<(List<Transfer> Enqueued, List<string> Failed)> EnqueueAsync(string username, IEnumerable<(string Filename, long Size)> files, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("Username is required", nameof(username));
            }

            var fileList = files?.ToList() ?? [];

            if (fileList.Count == 0)
            {
                throw new ArgumentException("At least one file is required", nameof(files));
            }

            if (fileList.Any(f => string.IsNullOrWhiteSpace(f.Filename)))
            {
                throw new ArgumentException("At least one filename is null, empty, or consists of only whitespace", nameof(files));
            }

            if (fileList.Count != fileList.Distinct().Count())
            {
                throw new ArgumentException("Two or more files in request are duplicated", nameof(files));
            }

            IPEndPoint endpoint;

            Log.Information("Requested enqueue of {Count} files from user {Username}", fileList.Count, username);

            // get the user's ip and port. this will throw if they are offline.
            try
            {
                endpoint = await Client.GetUserEndPointAsync(username, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue {Count} files from {Username}: {Message}", fileList.Count, username, ex.Message);
                throw;
            }

            // prime the cache for this user, to 1) make sure we can connect, and 2) avoid needing to race for it in the loop
            try
            {
                Log.Debug("Priming message connection to {Username}", username);
                await Client.ConnectToUserAsync(username, invalidateCache: false, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to {Username}: {Message}", username, ex.Message);
                throw;
            }

            List<Transfer> enqueued = [];
            List<string> failed = [];

            SemaphoreSlim userSemaphore;
            Task userSemaphoreWaitTask;

            Log.Debug("Awaiting enqueue semaphore sync root");
            await EnqueueSemaphoreSyncRoot.WaitAsync(cancellationToken);

            try
            {
                Log.Debug("Acquired enqueue semaphore sync root");
                userSemaphore = EnqueueSemaphores.GetOrAdd(username, (username) => new SemaphoreSlim(initialCount: 1, maxCount: 1));
                userSemaphoreWaitTask = userSemaphore.WaitAsync(cancellationToken);
            }
            finally
            {
                EnqueueSemaphoreSyncRoot.Release();
                Log.Debug("Released enqueue semaphore sync root");
            }

            Log.Debug("Awaiting enqueue semaphore for user {Username}", username);
            await userSemaphoreWaitTask;

            try
            {
                Log.Debug("Acquired enqueue semaphore for user {Username}", username);

                using var context = ContextFactory.CreateDbContext();

                /*
                    get existing downloads from this user.  this list will remain stable throughout this process because
                    we have exclusive access for this user.  we'll be adding new records, but we've already deduplicated
                    them so we don't have to worry about duplicate records being created in the critical section

                    we are looking for:
                    1. anything that's not yet complete (we need to disallow the enqueue)
                    2. anything complete, but not yet removed from the UI (we need to supersede it)
                */
                var existingRecordsNotYetRemoved = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => t.Username == username)
                    .Where(t => !t.Removed || !TransferStateCategories.Completed.Contains(t.State))
                    .AsNoTracking() // note: AI wants to remove this, dont.
                    .ToList();

                var existingInProgressRecords = existingRecordsNotYetRemoved
                    .Where(t => t.EndedAt == null || !t.State.HasFlag(TransferStates.Completed))
                    .ToDictionary(t => t.Filename, t => t);

                /*
                    determine how many concurrent enqueue requests we want to send to the remote client.

                    sending a ton of them can bog the client down and fail transfers due to resource contention on both sides,
                    but both clients should be able to handle momentary 'bursts'.

                    if the request contains 30 files or fewer, send all of the requests at the same time. the average number
                    of tracks in a single album is 15, so this is 2x as many as most enqueue requests will need.

                    if that number is more than 30, send only 5 at a time; transfers will be starting as we are still
                    enqueueing files, and this raises the risk of errors considerably.
                */
                var concurrentEnqueueRequests = 5;

                if (fileList.Count <= 30)
                {
                    concurrentEnqueueRequests = fileList.Count;
                }

                var maxTimeToWaitForEnqueueRequestAck = TimeSpan.FromMinutes(3);
                var enqueueSemaphore = new SemaphoreSlim(initialCount: concurrentEnqueueRequests, maxCount: concurrentEnqueueRequests);

                /*
                    iterate over each of the files in the request.  each file must be dispositioned as one of the following:

                    1. enqueued: passed duplicate checks and successfully kicked off a Task to enqueue the download
                    2. failed: download is already in progress, or something went wrong kicking off the Task

                    if we fail to enqueue a file, we must call TryFail() to ensure that if a record was inserted, it is
                    given a final disposition that will keep it from getting 'stuck'
                */
                foreach (var file in fileList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Guid transferId = Guid.NewGuid();

                    try
                    {
                        Log.Debug("Checking whether download of {Filename} from {Username} is already in progress", file.Filename, username);

                        /*
                            if there are any that haven't ended yet (checking a few ways out of paranoia), then there's already
                            an existing transfer record covering this file, and we're already enqueued. nothing more to do!
                        */
                        existingInProgressRecords.TryGetValue(file.Filename, out var existingInProgressRecord);

                        if (existingInProgressRecord is not null)
                        {
                            Log.Debug("Ignoring concurrent download enqueue attempt; transfer for {Filename} from {Username} already in progress (id: {Id})", file.Filename, username, existingInProgressRecord.Id);
                            failed.Add(file.Filename);
                            continue;
                        }

                        /*
                            check the tracked download dictionary in Soulseek.NET to see if it knows about this already
                            it shouldn't, if the slskd database doesn't. but things could get desynced, which is likely a bug
                            and we'd like to know about it
                        */
                        if (Client.Downloads.Any(u => u.Username == username && u.Filename == file.Filename))
                        {
                            Log.Warning("Ignoring concurrent download enqueue attempt; transfer for {Filename} from {Username} is tracked by the Soulseek client but not slskd", file.Filename, username);
                            failed.Add(file.Filename);
                            continue;
                        }

                        /*
                            add the transfer record to the database in the Queued | Locally state, and set the 'Removed'
                            property of any existing transfer to 'true', allowing the new record to supersede any old record(s)
                            on the UI

                            we have to persist these changes to the database at this time so the record shows up on the UI,
                            otherwise the transfers only show up as they are enqueued.
                        */
                        var transfer = new Transfer()
                        {
                            Id = transferId,
                            Username = username,
                            Direction = TransferDirection.Download,
                            Filename = file.Filename, // important! use the remote filename
                            Size = file.Size,
                            StartOffset = 0, // todo: maybe implement resumeable downloads?
                            RequestedAt = DateTime.UtcNow,
                            State = TransferStates.Queued | TransferStates.Locally,
                        };

                        context.Add(transfer);

                        Log.Debug("Added Transfer record for download of {Filename} from {Username} (id: {Id})", transfer.Filename, transfer.Username, transfer.Id);

                        foreach (var record in existingRecordsNotYetRemoved.Where(t => t.Filename == file.Filename && !t.Removed))
                        {
                            record.Removed = true;
                            context.Update(record);
                            Log.Debug("Marked existing download record of {Filename} from {Username} removed (id: {Id})", file.Filename, username, record.Id);
                        }

                        /*
                            create a TaskCompletionSource that we can await for one of the following:

                            1. the transfer enters the Queued | Remotely state
                            2. the transfer enters a state containing Completed
                            3. the linked CancellationTokenSource we add to CancellationTokens is cancelled
                            4. the 'max wait time' CancellationTokenSource is cancelled

                            add this to the dictionary before inserting the record, so we are guaranteed to have it
                            in the right place once the transfer hits the UI
                        */
                        var enqueuedTcs = new TaskCompletionSource<Transfer>();

                        // satisfies condition #3; CancellationTokenSource set cancelled by the user (via API call)
                        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        CancellationTokens.TryAdd(transfer.Id, cts);
                        cts.Token.Register(() => enqueuedTcs.TrySetCanceled());

                        /*
                            DANGER ZONE! this record is in the database now; we're on the hook for making sure it ends up
                            with a State that includes the Completed flag, else it remain "stuck" on the UI
                        */
                        context.SaveChanges();

                        Log.Debug("Scheduling Task for enqueue of {Filename} from {Username}", file.Filename, username);

                        var downloadEnqueueTask = Task.Run(async () =>
                        {
                            Log.Debug("Awaiting download enqueue semaphore for {Filename} from {Username}", transfer.Filename, transfer.Username);
                            await enqueueSemaphore.WaitAsync(cts.Token);

                            try
                            {
                                Log.Debug("Acquired download enqueue semaphore for {Filename} from {Username}", transfer.Filename, transfer.Username);

                                List<string> transitions = [];
                                TransferStates state = TransferStates.None;

                                // satisfies condition #4; remote user doesn't respond after a really long time
                                using var timeoutCts = new CancellationTokenSource(maxTimeToWaitForEnqueueRequestAck);
                                timeoutCts.Token.Register(() =>
                                {
                                    Log.Warning("Download of {Filename} from {Username} failed to enqueue remotely after hard time limit of {Duration} seconds. State transition history: {History}", transfer.Filename, username, maxTimeToWaitForEnqueueRequestAck.TotalSeconds, string.Join(", ", transitions));
                                    enqueuedTcs.TrySetException(new TimeoutException($"Download failed to enqueue remotely after hard time limit of {maxTimeToWaitForEnqueueRequestAck.TotalSeconds} secs"));
                                });

                                // satisfies conditions #1 and #2
                                void stateChanged(Transfer transfer)
                                {
                                    state = transfer.State;
                                    transitions.Add(transfer.State.ToString());

                                    if (transfer.State.HasFlag(TransferStates.Queued) && transfer.State.HasFlag(TransferStates.Remotely))
                                    {
                                        // satisfies condition #1; transfer is now remotely queued
                                        enqueuedTcs.TrySetResult(transfer);
                                    }

                                    // satisfies condition #2; transfer advanced to a terminal state before being enqueued remotely (probably failed)
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

                                Log.Debug("Scheduling Task for download of {Filename} from {Username}", transfer.Filename, transfer.Username);

                                var downloadTask = Task.Run(() => DownloadAsync(transfer, stateChanged, cancellationToken: cts.Token)).ContinueWith(task =>
                                {
                                    if (task.IsCompletedSuccessfully)
                                    {
                                        Log.Information("Task for download of {Filename} from {Username} completed successfully", transfer.Filename, transfer.Username);
                                        return;
                                    }

                                    Log.Error(task.Exception, "Task for download of {Filename} from {Username} did not complete successfully: {Error}", file.Filename, username, task.Exception.InnerException?.Message ?? task.Exception.Message);

                                    if (!TryFail(transferId, exception: task.Exception))
                                    {
                                        Log.Error(task.Exception, "Failed to clean up transfer {Id} after failed enqueue: {Message}", transfer.Id, task.Exception.Message);
                                    }
                                }, cancellationToken: CancellationToken.None); // end downloadTask.Run();

                                Log.Debug("Download Task status for {Filename} from {Username}: {Status}", file.Filename, username, downloadTask.Status);

                                Log.Debug("Waiting for download of {Filename} from {Username} to transition into {State}", transfer.Filename, transfer.Username, TransferStates.Queued | TransferStates.Remotely);

                                /*
                                    wait for one of the four conditions to be true:

                                    1. the transfer enters the Queued | Remotely state
                                    2. the transfer enters a state containing Completed
                                    3. the linked CancellationTokenSource we add to CancellationTokens is cancelled
                                    4. the 'max wait time' CancellationTokenSource is cancelled
                                */
                                await enqueuedTcs.Task;

                                Log.Debug("Download of {Filename} from {Username} successfully entered state {State}", transfer.Filename, transfer.Username, state);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Download of {File} from {Username} failed: {Message}", transfer.Filename, transfer.Username, ex.Message);
                                if (!TryFail(transferId, exception: ex))
                                {
                                    Log.Error(ex, "Failed to clean up transfer {Id} after failed execution: {Message}", transfer.Id, ex.Message);
                                }
                            }
                            finally
                            {
                                enqueueSemaphore.Release();
                            }
                        }, cancellationToken: cts.Token).ContinueWith(task =>
                        {
                            if (task.IsCompletedSuccessfully)
                            {
                                Log.Information("Task for enqueue of {Filename} from {Username} completed successfully", file.Filename, username);
                                return;
                            }

                            Log.Error(task.Exception, "Task for enqueue of {Filename} from {Username} did not complete successfully: {Error}", file.Filename, username, task.Exception.InnerException?.Message ?? task.Exception.Message);

                            if (!TryFail(transferId, exception: task.Exception))
                            {
                                Log.Error(task.Exception, "Failed to clean up transfer {Id} after failed enqueue: {Message}", transfer.Id, task.Exception.Message);
                            }
                        }, cancellationToken: CancellationToken.None); // end downloadEnqueueTask.Run();

                        Log.Debug("Download enqueue Task status for {Filename} from {Username}: {Status}", file.Filename, username, downloadEnqueueTask.Status);
                        Log.Information("Successfully locally enqueued download of {Filename} from {Username} (id: {Id})", file.Filename, username, transfer.Id);
                        enqueued.Add(transfer);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to enqueue download of {Filename} from {Username}: {Message}", file.Filename, username, ex.Message);
                        TryFail(transferId, exception: ex);
                        failed.Add(file.Filename);

                        if (CancellationTokens.TryRemove(transferId, out var cts))
                        {
                            cts.Dispose();
                        }

                        continue;
                    }
                } // end foreach()

                Log.Information("Successfully enqueued {Count} files from {Username}", enqueued.Count, username);
                return (enqueued, failed);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue one or more of {Count} files from {Username}: {Message}", fileList.Count, username, ex.Message);
                throw;
            }
            finally
            {
                userSemaphore.Release();
                Log.Debug("Released enqueue semaphore for user {Username}", username);
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
        /// <param name="states">One or more states by which downloads are filtered for pruning.</param>
        /// <returns>The number of pruned downloads.</returns>
        public int Prune(int age, params TransferStates[] states)
        {
            var statesSet = new HashSet<int>(states.Select(s => (int)s));

            if (!statesSet.All(s => ((TransferStates)s).HasFlag(TransferStates.Completed)))
            {
                throw new ArgumentException($"Each state must include {TransferStates.Completed}", nameof(states));
            }

            try
            {
                using var context = ContextFactory.CreateDbContext();

                var cutoffDateTime = DateTime.UtcNow.AddMinutes(-age);

                var pruned = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => !t.Removed)
                    .Where(t => t.EndedAt.HasValue && t.EndedAt.Value < cutoffDateTime)
                    .Where(t => statesSet.Contains((int)t.State))
                    .ExecuteUpdate(r => r.SetProperty(c => c.Removed, true));

                if (pruned > 0)
                {
                    Log.Debug("Pruned {Count} expired downloads with states {States}", pruned, statesSet);
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
        ///     Removes the completed download matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the download.</param>
        /// <returns>A value indicating whether the record was removed.</returns>
        public bool Remove(Guid id)
        {
            return Remove(t => t.Id == id) > 0;
        }

        /// <summary>
        ///     Removes all completed downloads matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="expression">The expression used to match downloads.</param>
        /// <returns>The number of records removed.</returns>
        public int Remove(Expression<Func<Transfer, bool>> expression)
        {
            try
            {
                using var context = ContextFactory.CreateDbContext();

                var count = context.Transfers
                    .Where(t => t.Direction == TransferDirection.Download)
                    .Where(t => TransferStateCategories.Completed.Contains(t.State))
                    .Where(expression)
                    .ExecuteUpdate(r => r.SetProperty(c => c.Removed, true));

                Log.Debug("Removed {Count} downloads by expression", count);
                return count;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove downloads by expression: {Message}", ex.Message);
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
            // if the cancellation token is still in the dictionary, calling Cancel() on it _should_
            // cause the transfer operation to fail with an OperationCancelled exception and set the state
            // of the transfer to Cancelled.  _should_!
            if (CancellationTokens.TryRemove(id, out var cts))
            {
                cts.Cancel();
                return true;
            }

            /*
                if the cancellation token couldn't be found in the dictionary, either:

                1. the transfer completed already
                2. something went wrong and the record is 'stuck'

                see if we can find the record, and if it's *NOT* completed, cancel it. otherwise,
                leave it alone.
            */
            var t = Find(t => t.Id == id);

            if (t is not null && !t.State.HasFlag(TransferStates.Completed))
            {
                t.EndedAt = DateTime.UtcNow;
                t.State = TransferStates.Completed | TransferStates.Cancelled;
                t.Exception = new OperationCanceledException().Message;
                Update(t);
                return true;
            }

            // this transfer id didn't have a cancellation token registered, and we couldn't find it in the dictionary
            return false;
        }

        /// <summary>
        ///     Fails the download matching the specified <paramref name="id"/> with the specified <paramref name="exception"/>,
        ///     and sets the final state accordingly.
        /// </summary>
        /// <remarks>
        ///     This method is designed to be idempotent, meaning subsequent calls for a given transfer shouldn't change
        ///     the EndedAt or Exception properties if they have already been set. If the transfer State already includes
        ///     the terminal Completed flag, it is unchanged.
        /// </remarks>
        /// <param name="id">The unique identifier for the download.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A value indicating whether the download was successfully failed.</returns>
        public bool TryFail(Guid id, Exception exception)
        {
            var t = Find(t => t.Id == id);

            if (t is null)
            {
                return false;
            }

            t.EndedAt ??= DateTime.UtcNow;

            // Soulseek.NET will include the filename and username in some messages; this is useful for many things but not tracking
            // exceptions in a database. when we encounter one of these, drop the first segment.
            var m = exception.Message;
            t.Exception ??= m.Contains(':') ? m.Substring(m.IndexOf(':') + 1).Trim() : m;

            if (!t.State.HasFlag(TransferStates.Completed))
            {
                t.State = TransferStates.Completed | exception switch
                {
                    OperationCanceledException => TransferStates.Cancelled,
                    TimeoutException => TransferStates.TimedOut,
                    _ => TransferStates.Errored,
                };
            }

            try
            {
                Update(t);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update database: {Message}", ex.Message);
                return false;
            }
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
        private async Task<Transfer> DownloadAsync(Transfer transfer, Action<Transfer> stateChanged = null, CancellationToken cancellationToken = default)
        {
            if (transfer is null)
            {
                throw new ArgumentNullException(nameof(transfer), "A valid, enqueued Transfer is required");
            }

            // grab the latest from the database; this may have been sitting behind a semaphore or something
            transfer = Find(t => t.Id == transfer.Id)
                ?? throw new TransferNotFoundException($"Transfer with ID {transfer.Id} not found");

            if (transfer.State != (TransferStates.Queued | TransferStates.Locally))
            {
                throw new InvalidOperationException($"Invalid starting state for download; expected {TransferStates.Queued | TransferStates.Locally}, encountered {transfer.State}");
            }

            if (Client.Downloads.Any(u => u.Username == transfer.Username && u.Filename == transfer.Filename))
            {
                throw new DuplicateTransferException("A duplicate download of the same file to the same user is already registered");
            }

            // there _should_ be a cancellation token for this transfer in the dictionary; if so link it with the one we are given
            // (which is probably the same one, harmless if so) to make sure we don't get any wires crossed
            CancellationTokens.TryGetValue(transfer.Id, out var cancellationTokenSource);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource?.Token ?? CancellationToken.None);
            cancellationToken = cts.Token;

            var updateSyncRoot = new SemaphoreSlim(1, 1);

            try
            {
                using var rateLimiter = new RateLimiter(250, concurrencyLimit: 1, flushOnDispose: true);

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
                                Log.Debug("Download update of {Filename} from {Username} not persisted; app is shutting down", transfer.Filename, transfer.Username);
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
                            SynchronizedUpdate(transfer, semaphore: updateSyncRoot, cancellationToken: cancellationToken);
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
                            // don't wait for the semaphore; if a previous progress update is still hanging, don't make
                            // the problem worse. this will result in fewer/jumpy updates on systems with slow filesystems
                            // but the alternative is to continue to stack slow writes on top of one another
                            if (updateSyncRoot.Wait(millisecondsTimeout: 0, cancellationToken: cancellationToken))
                            {
                                try
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
                                finally
                                {
                                    updateSyncRoot.Release();
                                }
                            }
                            else
                            {
                                Log.Debug("Skipped progress update of {Filename} from {Username} {BytesTransferred}/{TotalBytes}; previous update still pending", transfer.Filename, transfer.Username, args.Transfer.BytesTransferred, args.Transfer.Size);
                            }
                        }
                    }),
                    disposeOutputStreamOnCompletion: true);

                System.IO.UnixFileMode? unixFileMode = !string.IsNullOrEmpty(OptionsMonitor.CurrentValue.Permissions.File.Mode)
                    ? OptionsMonitor.CurrentValue.Permissions.File.Mode.ToUnixFileMode()
                    : null;

                Log.Debug("Invoking Soulseek DownloadAsync() for {Filename} from {Username}", transfer.Filename, transfer.Username);

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
                                UnixCreateMode = unixFileMode,
                            })),
                    size: transfer.Size,
                    startOffset: 0,
                    token: null,
                    cancellationToken: cancellationToken,
                    options: topts);

                Log.Debug("Invocation of Soulseek DownloadAsync() for {Filename} from user {Username} completed successfully", transfer.Filename, transfer.Username);

                // explicitly dispose the rate limiter to prevent updates from it beyond this point, and in doing so we
                // flush any pending update, _probably_ pushing the state of the transfer back to InProgress
                rateLimiter.Dispose();

                // copy the completed transfer that was returned from Soulseek.NET in a terminal, fully updated state
                // over the top of the transfer record, then persist it
                transfer = transfer.WithSoulseekTransfer(completedTransfer);

                // todo: broadcast to signalr hub
                SynchronizedUpdate(transfer, semaphore: updateSyncRoot, cancellationToken: CancellationToken.None);

                Log.Debug("Successfully updated Transfer for {Filename} from {Username} (state: {State}, progress: {Progress})", transfer.Filename, transfer.Username, transfer.State, transfer.PercentComplete);

                // move the file from incomplete to complete
                var destinationDirectory = System.IO.Path.GetDirectoryName(transfer.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Downloads));

                var finalFilename = Files.MoveFile(
                    sourceFilename: transfer.Filename.ToLocalFilename(baseDirectory: OptionsMonitor.CurrentValue.Directories.Incomplete),
                    destinationDirectory: destinationDirectory,
                    unixFileMode: unixFileMode,
                    overwrite: false,
                    deleteSourceDirectoryIfEmptyAfterMove: true);

                Log.Debug("Moved file {Filename} to {Destination}", transfer.Filename, finalFilename);

                if (OptionsMonitor.CurrentValue.Relay.Enabled)
                {
                    _ = Relay.NotifyFileDownloadCompleteAsync(finalFilename);
                }

                try
                {
                    // begin post-processing tasks; the file is downloaded, it has been removed from the client's download dictionary,
                    // and the file has been moved from the incomplete directory to the downloads directory
                    Log.Debug("Running post-download logic for {Filename} from {Username}", transfer.Filename, transfer.Username);

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

                    Log.Debug("Completed post-download logic for {Filename} from {Username} successfully", transfer.Filename, transfer.Username);
                }
                catch (Exception ex)
                {
                    // log, but don't throw. the file ended up in the download folder and is complete; if we throw it looks like it didn't complete
                    // todo: add a visual indicator/new state for Transfers that indicate this state.  or move all of this logic out and handle it via events
                    Log.Error(ex, "Failed to run post-download processes for {Filename} from {Username}: {Message}", transfer.Filename, transfer.Username, ex.Message);
                }

                Log.Information("Download of {Filename} from user {Username} completed successfully", transfer.Filename, transfer.Username);

                return transfer;
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is TimeoutException)
            {
                Log.Error(ex, "Download of {Filename} from user {Username} failed: {Message}", transfer.Filename, transfer.Username, ex.Message);

                TryFail(transfer.Id, exception: ex);

                // todo: broadcast
                SynchronizedUpdate(transfer, semaphore: updateSyncRoot, cancellationToken: CancellationToken.None);

                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Download of {Filename} from user {Username} failed: {Message}", transfer.Filename, transfer.Username, ex.Message);

                TryFail(transfer.Id, exception: ex);

                // todo: broadcast
                SynchronizedUpdate(transfer, semaphore: updateSyncRoot, cancellationToken: CancellationToken.None);

                throw;
            }
            finally
            {
                if (CancellationTokens.TryRemove(transfer.Id, out var storedCancellationTokenSource))
                {
                    storedCancellationTokenSource?.Dispose();
                }
            }
        }

        private void SynchronizedUpdate(Transfer transfer, SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
        {
            semaphore.Wait(cancellationToken);

            try
            {
                Update(transfer);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task CleanupEnqueueSemaphoresAsync()
        {
            if (await EnqueueSemaphoreSyncRoot.WaitAsync(0).ConfigureAwait(false))
            {
                try
                {
                    foreach (var kvp in EnqueueSemaphores)
                    {
                        if (await kvp.Value.WaitAsync(0).ConfigureAwait(false))
                        {
                            EnqueueSemaphores.TryRemove(kvp.Key, out var removed);
                            removed.Dispose();
                            Log.Debug("Cleaned up enqueue semaphore for {Key}", kvp.Key);
                        }
                    }
                }
                finally
                {
                    EnqueueSemaphoreSyncRoot.Release();
                }
            }
        }
    }
}