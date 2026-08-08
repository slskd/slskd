using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using slskd.Telemetry;
using Soulseek;
using Xunit;

namespace slskd.Tests.Unit.Telemetry;

public class ReportsServiceTests : IDisposable
{
    private readonly SqliteConnection _anchor;
    private readonly string _connectionString;

    public ReportsServiceTests()
    {
        var dbName = $"test_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        _anchor = new SqliteConnection(_connectionString);
        _anchor.Open();

        _anchor.Execute(@"
            CREATE TABLE Transfers (
                Id TEXT PRIMARY KEY,
                Username TEXT NOT NULL,
                Direction TEXT NOT NULL,
                Filename TEXT NOT NULL,
                Size INTEGER NOT NULL DEFAULT 0,
                State INTEGER NOT NULL DEFAULT 0,
                StateDescription TEXT,
                RequestedAt TEXT,
                EnqueuedAt TEXT,
                StartedAt TEXT,
                EndedAt TEXT,
                BytesTransferred INTEGER NOT NULL DEFAULT 0,
                AverageSpeed REAL NOT NULL DEFAULT 0.0,
                PlaceInQueue INTEGER,
                Exception TEXT,
                Removed INTEGER NOT NULL DEFAULT 0,
                BatchId TEXT,
                Attempts INTEGER NOT NULL DEFAULT 0,
                NextAttemptAt TEXT
            )");
    }

    public void Dispose()
    {
        _anchor.Dispose();
    }

    private ReportsService CreateService() =>
        new(new ConnectionStringDictionary(
            new Dictionary<Database, ConnectionString> { { Database.Transfers, _connectionString } }));

    [Fact]
    public void GetTransferHistogram_DropsTransfersInFirstPartialBucket_WhenStartIsNotOnUnixBucketBoundary()
    {
        // start = 2026-01-04T00:00:00Z has Unix-epoch offset mod 6048 = 4320 seconds,
        // so the first SQL bucket floor-snaps to 2026-01-03T22:33:36Z.  The C# re-snap
        // of that datetime gives 2026-01-03T21:07:12Z — a full 6048 s before the C#
        // pre-fill first key (2026-01-03T22:48:00Z) — so TryGetValue misses and the
        // transfer is silently dropped.
        var state = TransferStates.Completed | TransferStates.Succeeded;

        _anchor.Execute(@"
            INSERT INTO Transfers (Id, Username, Direction, Filename, Size, State, StateDescription,
                RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, AverageSpeed)
            VALUES (@Id, @Username, @Direction, @Filename, @Size, @State, @StateDescription,
                @RequestedAt, @EnqueuedAt, @StartedAt, @EndedAt, @BytesTransferred, @AverageSpeed)",
            new
            {
                Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
                Username = "testuser",
                Direction = TransferDirection.Upload.ToString(),
                Filename = "test.mp3",
                Size = 1_048_576L,
                State = (int)state,
                StateDescription = state.ToString(),
                RequestedAt = "2026-01-04 00:00:00",
                EnqueuedAt = "2026-01-04 00:00:00",
                StartedAt = "2026-01-04 00:01:00",
                EndedAt = "2026-01-04 00:10:00",
                BytesTransferred = 1_048_576L,
                AverageSpeed = 1748.0,
            });

        var sut = CreateService();

        var start = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc);

        var histogram = sut.GetTransferHistogram(start: start, end: end, buckets: 100);

        var totalCount = histogram.Values
            .SelectMany(directions => directions.Values)
            .SelectMany(states => states.Values)
            .Sum(summary => summary.Count);

        Assert.Equal(1, totalCount);
    }
}
