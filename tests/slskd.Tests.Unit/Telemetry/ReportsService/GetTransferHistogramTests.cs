namespace slskd.Tests.Unit.Telemetry;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using slskd.Telemetry;
using slskd.Transfers;
using Soulseek;
using Xunit;

public class GetTransferHistogramTests
{
    // Use 2024-01-01T00:00:00Z as the base start time. Its Unix timestamp (1704067200)
    // is exactly divisible by 3600, so hour-aligned bucket keys snap cleanly to the
    // expected boundaries without off-by-one surprises.
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2024, 1, 1, 3, 0, 0, DateTimeKind.Utc);

    // ── Argument validation ────────────────────────────────────────────────────

    [Fact]
    public void Throws_ArgumentException_When_Start_Is_Default()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(default, End, buckets: 3));
        Assert.IsType<ArgumentException>(ex);
    }

    [Fact]
    public void Throws_ArgumentException_When_End_Is_Default()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, default, buckets: 3));
        Assert.IsType<ArgumentException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_End_Equals_Start()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, Start, buckets: 3));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_End_Is_Before_Start()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(End, Start, buckets: 3));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Throws_ArgumentException_When_Both_Interval_And_Buckets_Are_Provided()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() =>
            sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1), buckets: 3));
        Assert.IsType<ArgumentException>(ex);
    }

    [Fact]
    public void Throws_ArgumentException_When_Neither_Interval_Nor_Buckets_Are_Provided()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End));
        Assert.IsType<ArgumentException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_Buckets_Exceeds_1000()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End, buckets: 1001));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_Buckets_Is_Zero()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End, buckets: 0));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_Buckets_Is_Negative()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End, buckets: -1));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_Interval_Is_Zero()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End, interval: TimeSpan.Zero));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Throws_ArgumentOutOfRangeException_When_Interval_Is_Negative()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() =>
            sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromSeconds(-1)));
        Assert.IsType<ArgumentOutOfRangeException>(ex);
    }

    [Fact]
    public void Does_Not_Throw_When_Buckets_Is_1()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End, buckets: 1));
        Assert.Null(ex);
    }

    [Fact]
    public void Does_Not_Throw_When_Buckets_Is_1000()
    {
        var (_, sut) = GetFixture();
        var ex = Record.Exception(() => sut.GetTransferHistogram(Start, End, buckets: 1000));
        Assert.Null(ex);
    }

    // ── Bucket structure ───────────────────────────────────────────────────────

    [Fact]
    public void Returns_Three_Bucket_Keys_For_Three_Hour_Window_With_Three_Buckets()
    {
        var (_, sut) = GetFixture();
        var histogram = sut.GetTransferHistogram(Start, End, buckets: 3);
        Assert.Equal(3, histogram.Count);
    }

    [Fact]
    public void Returns_Three_Bucket_Keys_For_Three_Hour_Window_With_One_Hour_Interval()
    {
        var (_, sut) = GetFixture();
        var histogram = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));
        Assert.Equal(3, histogram.Count);
    }

    [Fact]
    public void Bucket_Keys_Are_Spaced_By_The_Configured_Interval()
    {
        var (_, sut) = GetFixture();
        var histogram = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));
        var keys = histogram.Keys.OrderBy(k => k).ToList();

        Assert.Equal(TimeSpan.FromHours(1), keys[1] - keys[0]);
        Assert.Equal(TimeSpan.FromHours(1), keys[2] - keys[1]);
    }

    [Fact]
    public void All_Buckets_Contain_Entries_For_Both_Upload_And_Download_Directions()
    {
        // Gapless pre-fill must include both directions even for empty buckets.
        var (_, sut) = GetFixture();
        var histogram = sut.GetTransferHistogram(Start, End, buckets: 3);

        foreach (var bucket in histogram.Values)
        {
            Assert.True(bucket.ContainsKey(TransferDirection.Upload));
            Assert.True(bucket.ContainsKey(TransferDirection.Download));
        }
    }

    [Fact]
    public void All_Buckets_Contain_Empty_State_Dictionaries_When_No_Data()
    {
        var (_, sut) = GetFixture();
        var histogram = sut.GetTransferHistogram(Start, End, buckets: 3);

        foreach (var bucket in histogram.Values)
        {
            Assert.Empty(bucket[TransferDirection.Upload]);
            Assert.Empty(bucket[TransferDirection.Download]);
        }
    }

    [Fact]
    public void First_Bucket_Key_Is_Not_Later_Than_Start()
    {
        // The first bucket must cover Start; it may be snapped earlier due to epoch alignment.
        var (_, sut) = GetFixture();
        var histogram = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));
        Assert.True(histogram.Keys.Min() <= Start);
    }

    [Fact]
    public void Interval_And_Equivalent_Buckets_Count_Produce_Same_Number_Of_Bucket_Keys()
    {
        // 3 buckets over 3 hours computes an effective interval of 3600 s — same as
        // specifying the interval explicitly.
        var (_, sut) = GetFixture();

        var byBuckets = sut.GetTransferHistogram(Start, End, buckets: 3);
        var byInterval = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));

        Assert.Equal(byInterval.Count, byBuckets.Count);
    }

    // ── Time window filtering ──────────────────────────────────────────────────

    [Fact]
    public void Excludes_Transfer_Whose_EndedAt_Is_Before_Start()
    {
        var (mocks, sut) = GetFixture();
        mocks.Insert(startedAt: Start.AddHours(-2), endedAt: Start.AddSeconds(-1));

        Assert.Equal(0, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Excludes_Transfer_Whose_EndedAt_Is_After_End()
    {
        var (mocks, sut) = GetFixture();
        mocks.Insert(startedAt: Start.AddMinutes(10), endedAt: End.AddSeconds(1));

        Assert.Equal(0, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Includes_Transfer_Whose_EndedAt_Is_Within_Window()
    {
        var (mocks, sut) = GetFixture();
        mocks.Insert(startedAt: Start.AddMinutes(10), endedAt: Start.AddMinutes(30));

        Assert.Equal(1, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Includes_Transfer_Whose_EndedAt_Is_One_Second_Before_End()
    {
        var (mocks, sut) = GetFixture();
        mocks.Insert(startedAt: End.AddMinutes(-10), endedAt: End.AddSeconds(-1));

        Assert.Equal(1, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Excludes_Transfer_Whose_EndedAt_Is_Exactly_At_End()
    {
        // SQLite BETWEEN is inclusive, so the FILTER passes, but the bucket formula
        //   strftime('%s', EndedAt) / interval * interval
        // snaps EndedAt == End to a bucket that starts AT End. The C# pre-fill loop
        // (`while (currentInterval < end)`) never adds that bucket, so TryGetValue
        // misses and the result is silently dropped. End is an exclusive upper bound
        // in practice despite the inclusive BETWEEN clause.
        var (mocks, sut) = GetFixture();
        mocks.Insert(startedAt: End.AddMinutes(-10), endedAt: End);

        Assert.Equal(0, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Excludes_Transfer_With_Null_EndedAt()
    {
        // The query requires EndedAt IS NOT NULL. A completed-state transfer whose
        // EndedAt was somehow never set should still be excluded.
        var (mocks, sut) = GetFixture();
        mocks.Insert(
            startedAt: Start.AddMinutes(10),
            endedAt: null,
            state: TransferStates.Completed | TransferStates.Succeeded);

        Assert.Equal(0, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    // ── State filtering ────────────────────────────────────────────────────────

    [Fact]
    public void Excludes_Transfer_With_Non_Completed_State()
    {
        // InProgress (8) is not in TransferStateCategories.Completed, so even if the
        // record has a non-null EndedAt it must be excluded by the State IN filter.
        var (mocks, sut) = GetFixture();
        mocks.Insert(
            startedAt: Start.AddMinutes(10),
            endedAt: Start.AddMinutes(30),
            state: TransferStates.InProgress);

        Assert.Equal(0, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Includes_Succeeded_Transfers()
    {
        var (mocks, sut) = GetFixture();
        mocks.Insert(
            startedAt: Start.AddMinutes(10),
            endedAt: Start.AddMinutes(30),
            state: TransferStates.Completed | TransferStates.Succeeded);

        Assert.Equal(1, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    [Fact]
    public void Includes_Failed_Transfers_Of_All_Terminal_States()
    {
        // Errored, Cancelled, TimedOut, Rejected, and Aborted are all in
        // TransferStateCategories.Completed and must be included.
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Errored);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Cancelled);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.TimedOut);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Rejected);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Aborted);

        Assert.Equal(5, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    // ── Data aggregation ───────────────────────────────────────────────────────

    [Fact]
    public void TotalBytes_Is_Sum_Of_Size_Across_All_Transfers_In_Bucket()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t, size: 1_000);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t, size: 2_000);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t, size: 3_000);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(6_000, summary.TotalBytes);
    }

    [Fact]
    public void Count_Is_Number_Of_Transfers_In_Bucket()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(3, summary.Count);
    }

    [Fact]
    public void DistinctUsers_Counts_Only_Unique_Usernames_Not_Transfer_Count()
    {
        // 3 transfers from 2 users → DistinctUsers should be 2, not 3.
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(username: "alice", startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(username: "alice", startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(username: "bob", startedAt: t.AddMinutes(-5), endedAt: t);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(2, summary.DistinctUsers);
    }

    [Fact]
    public void AverageSpeed_Is_Total_Bytes_Divided_By_Total_Duration_Seconds()
    {
        // The query computes SUM(Size) / SUM(duration_seconds) — aggregate throughput,
        // not the average of each transfer's individual speed.
        //
        // Two transfers each lasting 60 s:
        //   - 600 bytes → 10 B/s
        //   - 1 200 bytes → 20 B/s
        // Aggregate: 1 800 bytes / 120 s = 15 B/s
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddSeconds(-60), endedAt: t, size: 600);
        mocks.Insert(startedAt: t.AddSeconds(-60), endedAt: t, size: 1_200);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(15.0, summary.AverageSpeed, precision: 2);
    }

    [Fact]
    public void AverageSpeed_Is_Zero_When_All_Transfers_Have_Zero_Duration()
    {
        // When StartedAt == EndedAt the total duration is 0. The query uses
        // NULLIF(SUM(duration), 0) to avoid division by zero, and COALESCE returns 0.0.
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t, endedAt: t, size: 1_000_000);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(0.0, summary.AverageSpeed);
    }

    [Fact]
    public void AverageWait_Is_Mean_Seconds_Between_EnqueuedAt_And_StartedAt()
    {
        // Transfer A: enqueued 70 s before started → waited 60 s (started 10 s before ended)
        // Transfer B: enqueued 130 s before started → waited 120 s
        // Average: (60 + 120) / 2 = 90 s
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(enqueuedAt: t.AddSeconds(-70), startedAt: t.AddSeconds(-10), endedAt: t);
        mocks.Insert(enqueuedAt: t.AddSeconds(-130), startedAt: t.AddSeconds(-10), endedAt: t);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(90.0, summary.AverageWait, precision: 1);
    }

    [Fact]
    public void AverageDuration_Is_Mean_Seconds_Between_StartedAt_And_EndedAt()
    {
        // Transfer A: ran 60 s; Transfer B: ran 120 s → average 90 s
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddSeconds(-60), endedAt: t);
        mocks.Insert(startedAt: t.AddSeconds(-120), endedAt: t);

        var summary = histogram(sut).Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(90.0, summary.AverageDuration, precision: 1);
    }

    // ── Grouping ───────────────────────────────────────────────────────────────

    [Fact]
    public void Uploads_And_Downloads_In_Same_Bucket_Are_In_Separate_Direction_Entries()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(direction: TransferDirection.Upload, startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(direction: TransferDirection.Download, startedAt: t.AddMinutes(-5), endedAt: t);

        var bucket = histogram(sut).Values.Single();

        Assert.Equal(1, bucket[TransferDirection.Upload].Values.Sum(s => s.Count));
        Assert.Equal(1, bucket[TransferDirection.Download].Values.Sum(s => s.Count));
    }

    [Fact]
    public void Different_Terminal_States_In_Same_Bucket_And_Direction_Are_Separate_Keys()
    {
        // A succeeded and an errored upload in the same bucket must appear as two
        // separate entries in the direction's state dictionary, not aggregated together.
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Succeeded);
        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Errored);

        var uploadStates = histogram(sut).Values.Single()[TransferDirection.Upload];

        Assert.Equal(2, uploadStates.Count);
        Assert.True(uploadStates.ContainsKey(TransferStates.Succeeded));
        Assert.True(uploadStates.ContainsKey(TransferStates.Errored));
    }

    [Fact]
    public void State_Key_Has_The_Completed_Flag_Stripped()
    {
        // The code maps state keys via `StateDescription & ~TransferStates.Completed`,
        // so a transfer stored as Completed|Succeeded (48) should appear under
        // key TransferStates.Succeeded (32), not the combined value.
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(startedAt: t.AddMinutes(-5), endedAt: t,
            state: TransferStates.Completed | TransferStates.Succeeded);

        var uploadStates = histogram(sut).Values.Single()[TransferDirection.Upload];

        Assert.True(uploadStates.ContainsKey(TransferStates.Succeeded));
        Assert.False(uploadStates.ContainsKey(TransferStates.Completed | TransferStates.Succeeded));
    }

    // ── Direction filter ───────────────────────────────────────────────────────

    [Fact]
    public void Direction_Filter_Upload_Includes_Only_Uploads()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(direction: TransferDirection.Upload, startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(direction: TransferDirection.Download, startedAt: t.AddMinutes(-5), endedAt: t);

        var histogram = sut.GetTransferHistogram(Start, End, buckets: 1, direction: TransferDirection.Upload);

        Assert.All(histogram.Values, bucket => Assert.Empty(bucket[TransferDirection.Download]));
        Assert.Equal(1, TotalCount(histogram));
    }

    [Fact]
    public void Direction_Filter_Download_Includes_Only_Downloads()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(direction: TransferDirection.Upload, startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(direction: TransferDirection.Download, startedAt: t.AddMinutes(-5), endedAt: t);

        var histogram = sut.GetTransferHistogram(Start, End, buckets: 1, direction: TransferDirection.Download);

        Assert.All(histogram.Values, bucket => Assert.Empty(bucket[TransferDirection.Upload]));
        Assert.Equal(1, TotalCount(histogram));
    }

    [Fact]
    public void No_Direction_Filter_Includes_Both_Directions()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(direction: TransferDirection.Upload, startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(direction: TransferDirection.Download, startedAt: t.AddMinutes(-5), endedAt: t);

        var bucket = sut.GetTransferHistogram(Start, End, buckets: 1).Values.Single();

        Assert.Equal(1, bucket[TransferDirection.Upload].Values.Sum(s => s.Count));
        Assert.Equal(1, bucket[TransferDirection.Download].Values.Sum(s => s.Count));
    }

    // ── Username filter ────────────────────────────────────────────────────────

    [Fact]
    public void Username_Filter_Includes_Only_Matching_User()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(username: "alice", startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(username: "bob", startedAt: t.AddMinutes(-5), endedAt: t);

        var histogram = sut.GetTransferHistogram(Start, End, buckets: 1, username: "alice");
        var summary = histogram.Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(1, summary.Count);
        Assert.Equal(1, summary.DistinctUsers);
    }

    [Fact]
    public void No_Username_Filter_Includes_All_Users()
    {
        var (mocks, sut) = GetFixture();
        var t = Start.AddMinutes(30);

        mocks.Insert(username: "alice", startedAt: t.AddMinutes(-5), endedAt: t);
        mocks.Insert(username: "bob", startedAt: t.AddMinutes(-5), endedAt: t);

        var summary = sut.GetTransferHistogram(Start, End, buckets: 1)
            .Values.Single()[TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(2, summary.Count);
        Assert.Equal(2, summary.DistinctUsers);
    }

    // ── Bucket assignment ──────────────────────────────────────────────────────

    [Fact]
    public void Transfer_Ending_At_01_30_Falls_In_01_00_Bucket_With_One_Hour_Interval()
    {
        // The SQL bucket formula floors each EndedAt to the nearest interval boundary:
        //   strftime('%s', '2024-01-01 01:30:00') / 3600 * 3600 → Unix timestamp of 01:00:00
        // So a transfer ending at 01:30 must land in the 01:00 bucket, not 00:00 or 02:00.
        var (mocks, sut) = GetFixture();
        var endedAt = Start.AddMinutes(90);  // 01:30:00

        mocks.Insert(startedAt: endedAt.AddMinutes(-5), endedAt: endedAt);

        var histogram = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));
        var bucket01 = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        Assert.True(histogram[bucket01][TransferDirection.Upload].ContainsKey(TransferStates.Succeeded));

        // The other two buckets (00:00 and 02:00) should be empty.
        var otherKeys = histogram.Keys.Where(k => k != bucket01);
        foreach (var key in otherKeys)
        {
            Assert.Empty(histogram[key][TransferDirection.Upload]);
        }
    }

    [Fact]
    public void Multiple_Transfers_In_Same_Bucket_Are_Aggregated_Together()
    {
        // Both transfers end in the 01:xx hour window → single aggregated entry in the 01:00 bucket.
        var (mocks, sut) = GetFixture();

        mocks.Insert(startedAt: Start.AddMinutes(65), endedAt: Start.AddMinutes(70), size: 1_000);
        mocks.Insert(startedAt: Start.AddMinutes(75), endedAt: Start.AddMinutes(80), size: 2_000);

        var histogram = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));
        var bucket01 = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var summary = histogram[bucket01][TransferDirection.Upload][TransferStates.Succeeded];

        Assert.Equal(2, summary.Count);
        Assert.Equal(3_000, summary.TotalBytes);
    }

    [Fact]
    public void Transfers_Ending_In_Different_Buckets_Are_Not_Aggregated()
    {
        // One transfer in hour 0, one in hour 1 — they must stay in separate buckets.
        var (mocks, sut) = GetFixture();

        mocks.Insert(startedAt: Start.AddMinutes(25), endedAt: Start.AddMinutes(30), size: 1_000);
        mocks.Insert(startedAt: Start.AddMinutes(85), endedAt: Start.AddMinutes(90), size: 2_000);

        var histogram = sut.GetTransferHistogram(Start, End, interval: TimeSpan.FromHours(1));
        var bucket00 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var bucket01 = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1_000, histogram[bucket00][TransferDirection.Upload][TransferStates.Succeeded].TotalBytes);
        Assert.Equal(2_000, histogram[bucket01][TransferDirection.Upload][TransferStates.Succeeded].TotalBytes);
    }

    [Fact]
    public void Single_Bucket_Aggregates_Transfers_Spread_Across_The_Window()
    {
        var (mocks, sut) = GetFixture();

        mocks.Insert(startedAt: Start.AddMinutes(25), endedAt: Start.AddMinutes(30));
        mocks.Insert(startedAt: Start.AddMinutes(85), endedAt: Start.AddMinutes(90));
        mocks.Insert(startedAt: Start.AddMinutes(145), endedAt: Start.AddMinutes(150));

        // buckets=1 puts the entire window in a single bucket
        Assert.Equal(3, TotalCount(sut.GetTransferHistogram(Start, End, buckets: 1)));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    // Shorthand to call GetTransferHistogram with the standard 3-bucket window.
    private static Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>>
        histogram(ReportsService sut) =>
            sut.GetTransferHistogram(Start, End, buckets: 1);

    private static long TotalCount(
        Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>> h) =>
            h.Values
                .SelectMany(d => d.Values)
                .SelectMany(s => s.Values)
                .Sum(summary => summary.Count);

    private static (Mocks mocks, ReportsService sut) GetFixture()
    {
        var mocks = new Mocks();
        var sut = new ReportsService(new ConnectionStringDictionary(
            new Dictionary<Database, ConnectionString> { { Database.Transfers, mocks.ConnectionString } }));
        return (mocks, sut);
    }

    private class Mocks
    {
        private readonly SqliteConnection _anchor;
        private readonly DbContextOptions<TransfersDbContext> _dbContextOptions;

        public Mocks()
        {
            var dbName = $"hist_{Guid.NewGuid():N}";
            ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

            // keep the shared-cache in-memory database alive for the life of this fixture
            _anchor = new SqliteConnection(ConnectionString);
            _anchor.Open();

            _dbContextOptions = new DbContextOptionsBuilder<TransfersDbContext>()
                .UseSqlite(ConnectionString)
                .Options;

            // derive the schema from the EF model so column/type regressions are caught at insert time
            using var context = new TransfersDbContext(_dbContextOptions);
            context.Database.EnsureCreated();
        }

        public string ConnectionString { get; }

        public void Insert(
            string username = "user",
            TransferDirection direction = TransferDirection.Upload,
            TransferStates? state = null,
            DateTime? enqueuedAt = null,
            DateTime? startedAt = null,
            DateTime? endedAt = null,
            long size = 1_000_000)
        {
            using var context = new TransfersDbContext(_dbContextOptions);
            context.Transfers.Add(new slskd.Transfers.Transfer
            {
                Id = Guid.NewGuid(),
                Username = username,
                Direction = direction,
                Filename = "file.mp3",
                Size = size,
                State = state ?? (TransferStates.Completed | TransferStates.Succeeded),
                RequestedAt = endedAt ?? DateTime.UtcNow,
                EnqueuedAt = enqueuedAt,
                StartedAt = startedAt,
                EndedAt = endedAt,
                BytesTransferred = size,
            });
            // SaveChanges sets StateDescription = State.ToString() via the DbContext override
            context.SaveChanges();
        }
    }
}
