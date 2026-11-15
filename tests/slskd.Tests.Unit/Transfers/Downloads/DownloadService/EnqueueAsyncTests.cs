namespace slskd.Tests.Unit.Transfers.Downloads;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.Files;
using slskd.Transfers;
using slskd.Transfers.Downloads;
using Soulseek;
using Xunit;

public partial class DownloadServiceTests
{
    /*
        EnqueueAsync() waits for one of five conditions before releasing the per-file enqueue semaphore:

        1. the transfer enters the Queued | Remotely state
        2. the transfer enters a state containing Completed
        3. the linked CancellationTokenSource we add to CancellationTokens is cancelled
        4. the 'max wait time' CancellationTokenSource is cancelled
        5. the DownloadAsync() method throws

        the 'max wait time' timer is created through the injected TimeProvider; disposal of that timer is how
        these tests observe that the wait ended, and invoking its callback is how they simulate the timeout.
    */
    public class EnqueueAsync
    {
        private const string Username = "alice";
        private const string Filename = "@alice\\Music\\track.flac";
        private const long Size = 12345;

        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task Condition_1_Queued_Remotely_Ends_Wait_Without_Failing_Or_Cancelling_Download()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.RaiseStateChanged(TransferStates.Queued | TransferStates.Remotely);

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = fixture.Service.Find(t => t.Id == transfer.Id);
            Assert.Equal(TransferStates.Queued | TransferStates.Remotely, record.State);
            Assert.Null(record.Exception);
            Assert.False(download.CancellationToken.IsCancellationRequested);
            Assert.False(fixture.TimerFired);
        }

        [Fact]
        public async Task Condition_2_Completed_Succeeded_Ends_Wait_Without_Failing()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.RaiseStateChanged(TransferStates.Completed | TransferStates.Succeeded);

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = fixture.Service.Find(t => t.Id == transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Succeeded, record.State);
            Assert.Null(record.Exception);
            Assert.False(fixture.TimerFired);
        }

        [Fact]
        public async Task Condition_2_Completed_Without_Success_Ends_Wait_And_Fails_Transfer()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.RaiseStateChanged(TransferStates.Completed | TransferStates.Rejected);

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Rejected, record.State);
            Assert.False(fixture.TimerFired);
        }

        [Fact]
        public async Task Condition_3_User_Cancellation_Ends_Wait_And_Cancels_Transfer()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            Assert.True(fixture.Service.TryCancel(transfer.Id));

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Cancelled, record.State);
            Assert.True(download.CancellationToken.IsCancellationRequested);
            Assert.False(fixture.TimerFired);
        }

        [Fact]
        public async Task Condition_4_Timeout_Ends_Wait_Times_Out_Transfer_And_Cancels_Download()
        {
            // the download ignores cancellation so that the enqueue timeout is the only thing that fails the transfer
            using var fixture = new Fixture(honorCancellation: false);
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            fixture.FireTimer();

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.TimedOut, record.State);
            Assert.Contains("hard time limit", record.Exception);

            // the download is cancelled so that slskd and Soulseek.NET don't diverge (#1827)
            Assert.True(download.CancellationToken.IsCancellationRequested);
        }

        [Fact]
        public async Task Condition_4_Timeout_Does_Not_Throw_When_Transfer_CancellationTokenSource_Is_Already_Disposed()
        {
            // regression test for #1891; DownloadAsync() disposes the transfer's CancellationTokenSource in its finally block,
            // and if the timeout fires after that, Cancel() threw ObjectDisposedException on a timer thread and crashed the process
            using var fixture = new Fixture(honorCancellation: false);
            var transfer = await fixture.EnqueueAsync();
            await fixture.Download.Task.WaitAsync(WaitLimit);

            fixture.GetTransferCancellationTokenSource(transfer.Id).Dispose();

            var ex = Record.Exception(() => fixture.FireTimer());

            Assert.Null(ex);

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.TimedOut, record.State);
        }

        [Fact]
        public async Task Condition_5_DownloadAsync_Throwing_Ends_Wait_And_Fails_Transfer()
        {
            // the download fails without ever reporting a Completed state, which previously left the wait hanging until the timeout
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.Fail(new Exception("boom"));

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Errored, record.State);
            Assert.Equal("boom", record.Exception);
            Assert.False(fixture.TimerFired);
        }

        /*
            everything below exists to cover the remainder of EnqueueAsync(); statements, branches, and each operand of
            every compound condition (each operand is shown to independently change the outcome).
        */

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Throws_ArgumentException_When_Username_Is_Null_Or_Empty(string username)
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(username, [(Filename, Size)]));

            Assert.Equal("username", Assert.IsType<ArgumentException>(ex).ParamName);
            fixture.Client.Verify(c => c.GetUserEndPointAsync(It.IsAny<string>(), It.IsAny<CancellationToken?>()), Times.Never);
        }

        [Fact]
        public async Task Throws_ArgumentException_When_Files_Is_Null()
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, null));

            Assert.Equal("files", Assert.IsType<ArgumentException>(ex).ParamName);
        }

        [Fact]
        public async Task Throws_ArgumentException_When_Files_Is_Empty()
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, []));

            Assert.Equal("files", Assert.IsType<ArgumentException>(ex).ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task Throws_ArgumentException_When_Any_Filename_Is_Null_Or_Empty(string filename)
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size), (filename, Size)]));

            Assert.Contains("null or empty", Assert.IsType<ArgumentException>(ex).Message);
            Assert.Empty(fixture.Records());
        }

        [Theory]
        [InlineData("..\\track.flac")]
        [InlineData("@alice\\..\\track.flac")]
        [InlineData("@alice/../track.flac")]
        public async Task Throws_ArgumentException_When_Any_Filename_Contains_Traversal(string filename)
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size), (filename, Size)]));

            Assert.Contains("traversal", Assert.IsType<ArgumentException>(ex).Message);
            Assert.Empty(fixture.Records());
        }

        [Fact]
        public async Task Throws_ArgumentException_When_Files_Are_Duplicated()
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size), (Filename, Size)]));

            Assert.Contains("duplicated", Assert.IsType<ArgumentException>(ex).Message);
            Assert.Empty(fixture.Records());
        }

        [Fact]
        public async Task Throws_ArgumentException_When_Filenames_Are_Duplicated_With_Different_Sizes()
        {
            using var fixture = new Fixture();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size), (Filename, Size + 1)]));

            Assert.Contains("duplicated", Assert.IsType<ArgumentException>(ex).Message);
            Assert.Empty(fixture.Records());
        }

        [Fact]
        public async Task Rethrows_When_User_Endpoint_Lookup_Fails()
        {
            using var fixture = new Fixture();
            var expected = new UserOfflineException("offline");
            fixture.Client.Setup(c => c.GetUserEndPointAsync(It.IsAny<string>(), It.IsAny<CancellationToken?>()))
                .ThrowsAsync(expected);

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)]));

            Assert.Same(expected, ex);
            fixture.Client.Verify(c => c.ConnectToUserAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken?>()), Times.Never);
            Assert.Empty(fixture.Records());
        }

        [Fact]
        public async Task Rethrows_When_Connecting_To_User_Fails()
        {
            using var fixture = new Fixture();
            var expected = new ConnectionException("unreachable");
            fixture.Client.Setup(c => c.ConnectToUserAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken?>()))
                .ThrowsAsync(expected);

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)]));

            Assert.Same(expected, ex);
            Assert.Empty(fixture.Records());
        }

        [Fact]
        public async Task Throws_When_Cancelled_Before_Acquiring_Semaphores_And_Leaves_User_Semaphore_Usable()
        {
            using var fixture = new Fixture();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)], cancellationToken: cts.Token));

            Assert.IsAssignableFrom<OperationCanceledException>(ex);
            Assert.Empty(fixture.Records());

            // a subsequent enqueue for the same user must not be blocked
            await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]).WaitAsync(WaitLimit);
        }

        [Fact]
        public async Task Concurrent_Enqueues_For_Same_User_Are_Serialized_And_Second_Sees_First_As_In_Progress()
        {
            using var fixture = new Fixture();
            var (entered, gate) = fixture.BlockFirstDbContext();

            var first = Task.Run(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)]));
            await entered.Task.WaitAsync(WaitLimit);

            var second = Task.Run(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)]));
            await Task.Delay(100);

            Assert.False(second.IsCompleted);

            gate.Set();

            var (firstEnqueued, firstFailed) = await first.WaitAsync(WaitLimit);
            var (secondEnqueued, secondFailed) = await second.WaitAsync(WaitLimit);

            Assert.Single(firstEnqueued);
            Assert.Empty(firstFailed);
            Assert.Empty(secondEnqueued);
            Assert.Equal((Filename, "Skipped: Already in progress"), Assert.Single(secondFailed));
            Assert.Single(fixture.Records());
        }

        [Fact]
        public async Task Cancelling_While_Waiting_For_User_Semaphore_Throws_And_Does_Not_Release_It()
        {
            using var fixture = new Fixture();
            var (entered, gate) = fixture.BlockFirstDbContext();

            var first = Task.Run(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)]));
            await entered.Task.WaitAsync(WaitLimit);

            using var cts = new CancellationTokenSource();
            var second = Task.Run(() => fixture.Service.EnqueueAsync(Username, [(OtherFilename, Size)], cancellationToken: cts.Token));
            await Task.Delay(100);

            cts.Cancel();

            Assert.IsAssignableFrom<OperationCanceledException>(await Record.ExceptionAsync(() => second.WaitAsync(WaitLimit)));

            gate.Set();

            // if the cancelled waiter had released the semaphore, the first enqueue would throw SemaphoreFullException here
            var (firstEnqueued, _) = await first.WaitAsync(WaitLimit);
            Assert.Single(firstEnqueued);

            var (thirdEnqueued, _) = await fixture.Service.EnqueueAsync(Username, [(OtherFilename, Size)]).WaitAsync(WaitLimit);
            Assert.Single(thirdEnqueued);
        }

        [Fact]
        public async Task Cancelling_Mid_Batch_Throws_And_Already_Inserted_Records_End_Up_Cancelled()
        {
            using var fixture = new Fixture();
            using var cts = new CancellationTokenSource();
            var inCriticalSection = false;

            fixture.OnCreateDbContext = () => inCriticalSection = true;
            fixture.ClientDownloads = () =>
            {
                // cancel while the first file is being processed; the second iteration of the loop should throw
                if (inCriticalSection)
                {
                    cts.Cancel();
                }

                return [];
            };

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size), (OtherFilename, Size)], cancellationToken: cts.Token));

            Assert.IsAssignableFrom<OperationCanceledException>(ex);

            var record = Assert.Single(fixture.Records());
            Assert.Equal(Filename, record.Filename);

            record = await fixture.WaitForCompletedAsync(record.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Cancelled, record.State);
            Assert.Empty(fixture.Downloads);

            // the user semaphore was released
            fixture.OnCreateDbContext = null;
            fixture.ClientDownloads = () => [];

            var (enqueued, _) = await fixture.Service.EnqueueAsync(Username, [(OtherFilename, Size)]).WaitAsync(WaitLimit);
            Assert.Single(enqueued);
        }

        [Fact]
        public async Task Rethrows_And_Releases_User_Semaphore_When_Reading_Existing_Records_Fails()
        {
            using var fixture = new Fixture();
            var expected = new Exception("database unavailable");
            fixture.OnCreateDbContext = () => throw expected;

            var ex = await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size)]));

            Assert.Same(expected, ex);

            fixture.OnCreateDbContext = null;

            var (enqueued, _) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]).WaitAsync(WaitLimit);
            Assert.Single(enqueued);
        }

        [Fact]
        public async Task Returns_Locally_Queued_Transfer_With_Requested_Properties()
        {
            using var fixture = new Fixture();
            var batchId = fixture.InsertBatch(Username).Id;

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)], batchId: batchId);

            Assert.Empty(failed);
            var transfer = Assert.Single(enqueued);
            Assert.NotEqual(Guid.Empty, transfer.Id);
            Assert.Equal(batchId, transfer.BatchId);
            Assert.Equal(Username, transfer.Username);
            Assert.Equal(Filename, transfer.Filename);
            Assert.Equal(Size, transfer.Size);
            Assert.Equal(TransferDirection.Download, transfer.Direction);
            Assert.Equal(TransferStates.Queued | TransferStates.Locally, transfer.State);
            Assert.NotEqual(default, transfer.RequestedAt);

            var record = Assert.Single(fixture.Records());
            Assert.Equal(transfer.Id, record.Id);
            Assert.Equal(batchId, record.BatchId);
            Assert.False(record.Removed);
        }

        [Theory]
        [InlineData(false, TransferStates.Queued | TransferStates.Remotely, true)] // not ended, not completed
        [InlineData(false, TransferStates.Completed | TransferStates.Succeeded, true)] // not ended, completed
        [InlineData(true, TransferStates.InProgress, true)] // ended, not completed
        [InlineData(true, TransferStates.Completed | TransferStates.Succeeded, false)] // ended and completed
        public async Task Existing_Record_Is_In_Progress_Unless_Both_Ended_And_Completed(bool ended, TransferStates state, bool expectSkipped)
        {
            using var fixture = new Fixture();
            var existing = fixture.Insert(Username, Filename, state, endedAt: ended ? DateTime.UtcNow : null);

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            if (expectSkipped)
            {
                Assert.Empty(enqueued);
                Assert.Equal((Filename, "Skipped: Already in progress"), Assert.Single(failed));
                Assert.False(Assert.Single(fixture.Records()).Removed);
                Assert.Empty(fixture.Downloads);
            }
            else
            {
                // the existing record is superseded by the new one
                var transfer = Assert.Single(enqueued);
                Assert.Empty(failed);
                Assert.True(fixture.Records().Single(r => r.Id == existing.Id).Removed);
                Assert.False(fixture.Records().Single(r => r.Id == transfer.Id).Removed);
            }
        }

        [Fact]
        public async Task Existing_Removed_And_Completed_Record_Is_Ignored()
        {
            using var fixture = new Fixture();
            var existing = fixture.Insert(Username, Filename, TransferStates.Completed | TransferStates.Errored, endedAt: DateTime.UtcNow, removed: true);

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Single(enqueued);
            Assert.Empty(failed);
            Assert.Equal(2, fixture.Records().Count);
            Assert.True(fixture.Records().Single(r => r.Id == existing.Id).Removed);
        }

        [Fact]
        public async Task Existing_Removed_But_Not_Completed_Record_Is_In_Progress()
        {
            using var fixture = new Fixture();
            fixture.Insert(Username, Filename, TransferStates.InProgress, endedAt: null, removed: true);

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Empty(enqueued);
            Assert.Equal((Filename, "Skipped: Already in progress"), Assert.Single(failed));
        }

        [Fact]
        public async Task Existing_Records_For_Other_Files_Users_And_Directions_Are_Unaffected()
        {
            using var fixture = new Fixture();
            var otherFileInProgress = fixture.Insert(Username, OtherFilename, TransferStates.Queued | TransferStates.Remotely);
            var otherFileCompleted = fixture.Insert(Username, "@alice\\Music\\other.flac", TransferStates.Completed | TransferStates.Succeeded, endedAt: DateTime.UtcNow);
            var otherUser = fixture.Insert("bob", Filename, TransferStates.Queued | TransferStates.Remotely);
            var upload = fixture.Insert(Username, Filename, TransferStates.Queued | TransferStates.Locally, direction: TransferDirection.Upload);

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Single(enqueued);
            Assert.Empty(failed);

            var records = fixture.Records();
            Assert.All(new[] { otherFileInProgress, otherFileCompleted, otherUser, upload }, existing => Assert.False(records.Single(r => r.Id == existing.Id).Removed));
        }

        [Fact]
        public async Task Only_Completed_Records_Matching_Each_File_Are_Superseded()
        {
            using var fixture = new Fixture();
            var completed = fixture.Insert(Username, Filename, TransferStates.Completed | TransferStates.Errored, endedAt: DateTime.UtcNow);
            var otherCompleted = fixture.Insert(Username, "@alice\\Music\\other.flac", TransferStates.Completed | TransferStates.Errored, endedAt: DateTime.UtcNow);

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size), (OtherFilename, Size)]);

            Assert.Equal(2, enqueued.Count);
            Assert.Empty(failed);

            var records = fixture.Records();
            Assert.True(records.Single(r => r.Id == completed.Id).Removed);
            Assert.False(records.Single(r => r.Id == otherCompleted.Id).Removed);
        }

        [Fact]
        public async Task Proceeds_When_Client_Downloads_Is_Null()
        {
            using var fixture = new Fixture();
            fixture.ClientDownloads = () => null;

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Single(enqueued);
            Assert.Empty(failed);
        }

        [Theory]
        [InlineData(Username, Filename, true)] // same user, same file
        [InlineData(Username, OtherFilename, false)] // same user, other file
        [InlineData("bob", Filename, false)] // other user, same file
        public async Task Skips_File_Already_Tracked_By_Soulseek_Client_For_Same_User(string trackedUsername, string trackedFilename, bool expectSkipped)
        {
            using var fixture = new Fixture();
            var tracked = SoulseekTransfer(trackedUsername, trackedFilename, TransferStates.Queued | TransferStates.Remotely);
            fixture.ClientDownloads = () => [tracked];

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            if (expectSkipped)
            {
                Assert.Empty(enqueued);
                Assert.Equal((Filename, "Skipped: Already in progress"), Assert.Single(failed));
                Assert.Empty(fixture.Records());
            }
            else
            {
                Assert.Single(enqueued);
                Assert.Empty(failed);
            }
        }

        [Fact]
        public async Task Fails_File_When_Exception_Is_Thrown_Before_Cancellation_Token_Is_Tracked()
        {
            using var fixture = new Fixture();
            fixture.ClientDownloads = () => throw new Exception("client exploded");

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Empty(enqueued);
            Assert.Equal((Filename, "Error: client exploded"), Assert.Single(failed));
            Assert.Empty(fixture.Records());
            Assert.Empty(fixture.CancellationTokens);
        }

        [Fact]
        public async Task Fails_File_And_Disposes_Cancellation_Token_When_Saving_Record_Fails()
        {
            using var fixture = new Fixture();
            fixture.Execute($"CREATE TRIGGER fail_insert BEFORE INSERT ON Transfers WHEN NEW.Filename = '{Filename}' BEGIN SELECT RAISE(ABORT, 'insert failed'); END;");

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Empty(enqueued);
            var (failedFilename, message) = Assert.Single(failed);
            Assert.Equal(Filename, failedFilename);
            Assert.StartsWith("Error: ", message);
            Assert.Empty(fixture.Records());
            Assert.Empty(fixture.CancellationTokens);
            Assert.Empty(fixture.Downloads);
        }

        [Fact]
        public async Task Sends_All_Enqueue_Requests_Concurrently_When_Batch_Has_30_Files()
        {
            using var fixture = new Fixture();

            var (enqueued, _) = await fixture.Service.EnqueueAsync(Username, Files(30));

            Assert.Equal(30, enqueued.Count);
            await fixture.WaitUntilAsync(() => fixture.Downloads.Count == 30);
        }

        [Fact]
        public async Task Sends_At_Most_5_Enqueue_Requests_Concurrently_When_Batch_Has_More_Than_30_Files()
        {
            using var fixture = new Fixture();

            var (enqueued, _) = await fixture.Service.EnqueueAsync(Username, Files(31));

            Assert.Equal(31, enqueued.Count);
            await fixture.WaitUntilAsync(() => fixture.Downloads.Count == 5);
            await Task.Delay(250);
            Assert.Equal(5, fixture.Downloads.Count);

            // once one is queued remotely, its slot is released to the next waiting file
            fixture.Downloads.Values.First().RaiseStateChanged(TransferStates.Queued | TransferStates.Remotely);

            await fixture.WaitUntilAsync(() => fixture.Downloads.Count == 6);
            await Task.Delay(250);
            Assert.Equal(6, fixture.Downloads.Count);
        }

        [Fact]
        public async Task Cancelling_File_Waiting_For_Enqueue_Slot_Cancels_Transfer_Without_Starting_Download()
        {
            using var fixture = new Fixture();
            var (enqueued, _) = await fixture.Service.EnqueueAsync(Username, Files(31));
            await fixture.WaitUntilAsync(() => fixture.Downloads.Count == 5);

            var waiting = enqueued.First(t => !fixture.Downloads.ContainsKey(t.Filename));

            Assert.True(fixture.Service.TryCancel(waiting.Id));

            var record = await fixture.WaitForCompletedAsync(waiting.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Cancelled, record.State);

            await Task.Delay(250);
            Assert.False(fixture.Downloads.ContainsKey(waiting.Filename));
            Assert.Equal(5, fixture.Downloads.Count);
        }

        [Fact]
        public async Task Cancelling_File_Waiting_For_Enqueue_Slot_Tolerates_Missing_Record()
        {
            using var fixture = new Fixture();
            var (enqueued, _) = await fixture.Service.EnqueueAsync(Username, Files(31));
            await fixture.WaitUntilAsync(() => fixture.Downloads.Count == 5);

            var waiting = enqueued.First(t => !fixture.Downloads.ContainsKey(t.Filename));
            fixture.Delete(waiting.Id);

            Assert.True(fixture.Service.TryCancel(waiting.Id));

            await Task.Delay(250);
            Assert.Null(fixture.Service.Find(t => t.Id == waiting.Id));
            Assert.Equal(5, fixture.Downloads.Count);
        }

        [Theory]
        [InlineData(TransferStates.Queued | TransferStates.Locally)] // queued, but not remotely
        [InlineData(TransferStates.Remotely)] // remotely, but not queued
        [InlineData(TransferStates.Initializing)]
        [InlineData(TransferStates.InProgress)]
        public async Task Non_Terminal_States_Other_Than_Queued_Remotely_Do_Not_End_Wait(TransferStates state)
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.RaiseStateChanged(state);
            await Task.Delay(100);

            Assert.False(fixture.TimerDisposed.Task.IsCompleted);
            Assert.False(fixture.Service.Find(t => t.Id == transfer.Id).State.HasFlag(TransferStates.Completed));

            download.RaiseStateChanged(TransferStates.Queued | TransferStates.Remotely);

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);
        }

        [Fact]
        public async Task Completed_Without_Success_Preserves_Remote_Exception_Message()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.RaiseStateChanged(TransferStates.Completed | TransferStates.Errored, new Exception("remote said no"));

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Errored, record.State);
            Assert.Equal("remote said no", record.Exception);
        }

        [Fact]
        public async Task Successful_Download_Completes_Transfer_And_Releases_Cancellation_Token()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            download.RaiseStateChanged(TransferStates.Queued | TransferStates.Remotely);
            download.Complete();

            await fixture.WaitUntilAsync(() => !fixture.CancellationTokens.ContainsKey(transfer.Id));

            var record = fixture.Service.Find(t => t.Id == transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Succeeded, record.State);
            Assert.Null(record.Exception);
            fixture.Files.Verify(f => f.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UnixFileMode?>(), It.IsAny<bool>(), true), Times.Once);
        }

        [Fact]
        public async Task Download_Cancelled_Before_Transfer_Starts_Cancels_Transfer()
        {
            using var fixture = new Fixture();
            var cancelled = 0;

            fixture.ClientDownloads = () =>
            {
                // the first call is EnqueueAsync() checking for duplicates (no record yet); the next is DownloadAsync()
                // doing the same, at which point we cancel so that DownloadAsync() throws OperationCanceledException
                var record = fixture.Service.Find(t => t.Filename == Filename);

                if (record is not null && Interlocked.Exchange(ref cancelled, 1) == 0)
                {
                    fixture.Service.TryCancel(record.Id);
                }

                return [];
            };

            var transfer = await fixture.EnqueueAsync();

            var result = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Cancelled, result.State);
            Assert.Empty(fixture.Downloads);
        }

        [Fact]
        public async Task Download_Failure_Tolerates_Missing_Record()
        {
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            fixture.Delete(transfer.Id);
            download.Fail(new Exception("boom"));

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);
            await fixture.WaitUntilAsync(() => !fixture.CancellationTokens.ContainsKey(transfer.Id));
            await Task.Delay(100);

            Assert.Null(fixture.Service.Find(t => t.Id == transfer.Id));
        }

        [Fact]
        public async Task Removed_Record_With_Uncategorized_Completed_State_Is_Loaded_But_Not_Superseded_Again()
        {
            // Completed | Locally isn't in TransferStateCategories.Completed, so the query loads this removed record, but it
            // isn't in progress (ended and has the Completed flag), so it reaches the supersede step where !Removed excludes it
            using var fixture = new Fixture();
            var existing = fixture.Insert(Username, Filename, TransferStates.Completed | TransferStates.Locally, endedAt: DateTime.UtcNow, removed: true);

            var (enqueued, failed) = await fixture.Service.EnqueueAsync(Username, [(Filename, Size)]);

            Assert.Single(enqueued);
            Assert.Empty(failed);

            var record = fixture.Records().Single(r => r.Id == existing.Id);
            Assert.True(record.Removed);
            Assert.Equal(TransferStates.Completed | TransferStates.Locally, record.State);
        }

        [Fact]
        public async Task Database_Failure_While_Failing_Transfer_Does_Not_Escape()
        {
            // TryFail() rethrows when it can't read the record; when that happens inside the enqueue catch block,
            // the enqueue Task faults and its continuation tries (and fails) again. none of this may escape.
            using var fixture = new Fixture();
            var transfer = await fixture.EnqueueAsync();
            var download = await fixture.Download.Task.WaitAsync(WaitLimit);

            fixture.OnCreateDbContext = () => throw new Exception("database unavailable");
            download.Fail(new Exception("boom"));

            await fixture.TimerDisposed.Task.WaitAsync(WaitLimit);
            await fixture.WaitUntilAsync(() => !fixture.CancellationTokens.ContainsKey(transfer.Id));
            await Task.Delay(100);
        }

        [Fact]
        public async Task Cancellation_Token_Is_Released_When_DownloadAsync_Throws_Before_Starting()
        {
            using var fixture = new Fixture();

            fixture.ClientDownloads = () =>
            {
                // the first call is EnqueueAsync() checking for duplicates (no record yet); once the record exists, DownloadAsync()
                // makes the same check, and reporting a duplicate makes it throw DuplicateTransferException before its try block
                var record = fixture.Service.Find(t => t.Filename == Filename);
                return record is null ? [] : [SoulseekTransfer(Username, Filename, TransferStates.Queued | TransferStates.Remotely)];
            };

            var transfer = await fixture.EnqueueAsync();

            var record = await fixture.WaitForCompletedAsync(transfer.Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Errored, record.State);
            Assert.Empty(fixture.Downloads);

            await Task.Delay(100);
            Assert.False(fixture.CancellationTokens.ContainsKey(transfer.Id));
        }

        [Fact]
        public async Task Cancellation_Token_Is_Released_When_Batch_Is_Cancelled_Mid_Loop()
        {
            using var fixture = new Fixture();
            using var cts = new CancellationTokenSource();
            var inCriticalSection = false;

            fixture.OnCreateDbContext = () => inCriticalSection = true;
            fixture.ClientDownloads = () =>
            {
                if (inCriticalSection)
                {
                    cts.Cancel();
                }

                return [];
            };

            await Record.ExceptionAsync(() => fixture.Service.EnqueueAsync(Username, [(Filename, Size), (OtherFilename, Size)], cancellationToken: cts.Token));

            var record = await fixture.WaitForCompletedAsync(Assert.Single(fixture.Records()).Id);
            Assert.Equal(TransferStates.Completed | TransferStates.Cancelled, record.State);

            await Task.Delay(100);
            Assert.False(fixture.CancellationTokens.ContainsKey(record.Id));
        }

        private const string OtherFilename = "@alice\\Music\\track2.flac";

        private static List<(string Filename, long Size)> Files(int count)
            => Enumerable.Range(0, count).Select(i => ($"@alice\\Music\\track{i:00}.flac", Size)).ToList();

        private static Soulseek.Transfer SoulseekTransfer(string username, string filename, TransferStates state, Exception exception = null)
            => new(
                Soulseek.TransferDirection.Download,
                username,
                filename,
                1,
                state,
                Size,
                0,
                state.HasFlag(TransferStates.Succeeded) ? Size : 0,
                0,
                state.HasFlag(TransferStates.Completed) ? DateTime.UtcNow : null,
                state.HasFlag(TransferStates.Completed) ? DateTime.UtcNow : null,
                null,
                null,
                exception);

        private sealed class Fixture : IDisposable
        {
            private readonly SqliteConnection _anchor;
            private readonly DbContextOptions<TransfersDbContext> _dbContextOptions;
            private TimerCallback _timerCallback;
            private object _timerState;

            public Fixture(bool honorCancellation = true)
            {
                var connectionString = $"Data Source=enqueue_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

                // keep the shared-cache in-memory database alive for the life of this fixture
                _anchor = new SqliteConnection(connectionString);
                _anchor.Open();

                _dbContextOptions = new DbContextOptionsBuilder<TransfersDbContext>()
                    .UseSqlite(connectionString)
                    .Options;

                using (var context = new TransfersDbContext(_dbContextOptions))
                {
                    context.Database.EnsureCreated();
                }

                var contextFactory = new Mock<IDbContextFactory<TransfersDbContext>>();
                contextFactory.Setup(f => f.CreateDbContext()).Returns(() =>
                {
                    OnCreateDbContext?.Invoke();
                    return new TransfersDbContext(_dbContextOptions);
                });

                var options = new Options
                {
                    Directories = new Options.DirectoriesOptions { Incomplete = Path.GetTempPath(), Downloads = Path.GetTempPath() },
                    Transfers = new Options.TransfersOptions
                    {
                        Download = new Options.TransfersOptions.GlobalDownloadOptions
                        {
                            // one attempt, so that failures are terminal and no retry delays are introduced
                            Retry = new Options.TransfersOptions.GlobalDownloadOptions.RetryOptions { Attempts = 1 },
                        },
                    },
                };

                var optionsMonitor = new TestOptionsMonitor<Options>(options);

                Client.Setup(c => c.GetUserEndPointAsync(It.IsAny<string>(), It.IsAny<CancellationToken?>()))
                    .ReturnsAsync(new IPEndPoint(IPAddress.Loopback, 2234));
                Client.Setup(c => c.ConnectToUserAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken?>()))
                    .Returns(Task.CompletedTask);
                Client.Setup(c => c.Downloads)
                    .Returns(() => ClientDownloads()?.ToList());
                Client.Setup(c => c.DownloadAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Func<Task<Stream>>>(),
                        It.IsAny<long?>(),
                        It.IsAny<long>(),
                        It.IsAny<int?>(),
                        It.IsAny<TransferOptions>(),
                        It.IsAny<CancellationToken?>()))
                    .Returns<string, string, Func<Task<Stream>>, long?, long, int?, TransferOptions, CancellationToken?>((username, filename, _, _, _, _, transferOptions, cancellationToken) =>
                    {
                        var download = new FakeDownload(username, filename, transferOptions, cancellationToken ?? CancellationToken.None, honorCancellation);
                        Downloads.TryAdd(filename, download);
                        Download.TrySetResult(download);
                        return download.Task;
                    });

                Files = new Mock<FileService>(optionsMonitor);
                Files.Setup(f => f.ResolveFileInfo(It.IsAny<string>()))
                    .Returns(new FileInfo(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.incomplete")));

                var timer = new Mock<ITimer>();
                timer.Setup(t => t.Dispose()).Callback(() => TimerDisposed.TrySetResult());

                var timeProvider = new Mock<TimeProvider>();
                timeProvider.Setup(p => p.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
                    .Callback<TimerCallback, object, TimeSpan, TimeSpan>((callback, state, _, _) =>
                    {
                        _timerCallback = callback;
                        _timerState = state;
                    })
                    .Returns(timer.Object);

                Service = new DownloadService(
                    new Mock<IBatchService>().Object,
                    new Mock<slskd.Search.ISearchService>().Object,
                    optionsMonitor,
                    Client.Object,
                    contextFactory.Object,
                    Files.Object,
                    null,
                    null,
                    null,
                    timeProvider.Object);
            }

            public DownloadService Service { get; }
            public Mock<ISoulseekClient> Client { get; } = new();
            public Mock<FileService> Files { get; }
            public Func<IEnumerable<Soulseek.Transfer>> ClientDownloads { get; set; } = () => [];
            public Action OnCreateDbContext { get; set; }
            public ConcurrentDictionary<string, FakeDownload> Downloads { get; } = new();
            public TaskCompletionSource<FakeDownload> Download { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource TimerDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool TimerFired { get; private set; }
            public ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens
                => Service.GetProperty<ConcurrentDictionary<Guid, CancellationTokenSource>>("CancellationTokens");

            public async Task<slskd.Transfers.Transfer> EnqueueAsync()
            {
                var (enqueued, failed) = await Service.EnqueueAsync(Username, [(Filename, Size)]);

                Assert.Empty(failed);
                return Assert.Single(enqueued);
            }

            public void FireTimer()
            {
                TimerFired = true;
                _timerCallback(_timerState);
            }

            public CancellationTokenSource GetTransferCancellationTokenSource(Guid id)
            {
                return CancellationTokens[id];
            }

            /// <summary>
            ///     Blocks the first DbContext creation (made from within the per-user critical section of EnqueueAsync()) until
            ///     the returned gate is set; the returned TaskCompletionSource completes once the block is entered.
            /// </summary>
            public (TaskCompletionSource Entered, ManualResetEventSlim Gate) BlockFirstDbContext()
            {
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var gate = new ManualResetEventSlim(false);
                var first = 1;

                OnCreateDbContext = () =>
                {
                    if (Interlocked.Exchange(ref first, 0) == 1)
                    {
                        entered.TrySetResult();
                        gate.Wait(WaitLimit);
                    }
                };

                return (entered, gate);
            }

            public slskd.Transfers.Transfer Insert(
                string username,
                string filename,
                TransferStates state,
                DateTime? endedAt = null,
                bool removed = false,
                TransferDirection direction = TransferDirection.Download)
            {
                var transfer = new slskd.Transfers.Transfer
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    Direction = direction,
                    Filename = filename,
                    Size = Size,
                    State = state,
                    RequestedAt = DateTime.UtcNow,
                    EndedAt = endedAt,
                    Removed = removed,
                };

                using var context = new TransfersDbContext(_dbContextOptions);
                context.Transfers.Add(transfer);
                context.SaveChanges();

                return transfer;
            }

            public Batch InsertBatch(string username)
            {
                var batch = new Batch { Id = Guid.NewGuid(), Username = username };

                using var context = new TransfersDbContext(_dbContextOptions);
                context.Batches.Add(batch);
                context.SaveChanges();

                return batch;
            }

            public void Delete(Guid id)
            {
                using var context = new TransfersDbContext(_dbContextOptions);
                context.Transfers.Where(t => t.Id == id).ExecuteDelete();
            }

            public void Execute(string sql)
            {
                using var command = _anchor.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }

            public List<slskd.Transfers.Transfer> Records()
            {
                using var context = new TransfersDbContext(_dbContextOptions);
                return context.Transfers.AsNoTracking().ToList();
            }

            public async Task WaitUntilAsync(Func<bool> condition)
            {
                var deadline = DateTime.UtcNow + WaitLimit;

                while (!condition())
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException("Condition was not met before the wait limit");
                    }

                    await Task.Delay(25);
                }
            }

            public async Task<slskd.Transfers.Transfer> WaitForCompletedAsync(Guid id)
            {
                var deadline = DateTime.UtcNow + WaitLimit;

                while (true)
                {
                    var record = Service.Find(t => t.Id == id);

                    if (record.State.HasFlag(TransferStates.Completed) || DateTime.UtcNow > deadline)
                    {
                        return record;
                    }

                    await Task.Delay(25);
                }
            }

            public void Dispose()
            {
                _anchor.Dispose();
            }
        }

        private sealed class FakeDownload
        {
            private readonly string _username;
            private readonly string _filename;
            private readonly TransferOptions _options;
            private readonly TaskCompletionSource<Soulseek.Transfer> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private TransferStates _state = TransferStates.Queued | TransferStates.Locally;

            public FakeDownload(string username, string filename, TransferOptions options, CancellationToken cancellationToken, bool honorCancellation)
            {
                _username = username;
                _filename = filename;
                _options = options;
                CancellationToken = cancellationToken;

                if (honorCancellation)
                {
                    cancellationToken.Register(() => _tcs.TrySetCanceled(cancellationToken));
                }
            }

            public CancellationToken CancellationToken { get; }
            public Task<Soulseek.Transfer> Task => _tcs.Task;

            public void RaiseStateChanged(TransferStates state, Exception exception = null)
            {
                var previous = _state;
                _state = state;

                _options.StateChanged((previous, SoulseekTransfer(_username, _filename, state, exception)));
            }

            public void Complete()
            {
                RaiseStateChanged(TransferStates.Completed | TransferStates.Succeeded);
                _tcs.TrySetResult(SoulseekTransfer(_username, _filename, TransferStates.Completed | TransferStates.Succeeded));
            }

            public void Fail(Exception exception)
            {
                _tcs.TrySetException(exception);
            }
        }
    }
}
