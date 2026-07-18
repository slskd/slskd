// <copyright file="ReportsService.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Telemetry;

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using slskd.Transfers;
using Soulseek;

/// <summary>
///     Reports.
/// </summary>
public class ReportsService
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ReportsService"/> class.
    /// </summary>
    public ReportsService(ConnectionStringDictionary connectionStringDictionary)
    {
        ConnectionStrings = connectionStringDictionary;
    }

    private ConnectionStringDictionary ConnectionStrings { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<ReportsService>();

    /// <summary>
    ///     Returns a summary of all transfer data grouped by direction and final transfer state. Includes only
    ///     completed transfers.
    /// </summary>
    /// <param name="start">The optional start time of the summary window.</param>
    /// <param name="end">The optional end time of the summary window.</param>
    /// <param name="direction">The optional direction (Upload or Download) for the summary.</param>
    /// <param name="username">The optional username by which to filter results.</param>
    /// <returns>A nested dictionary keyed by direction and state and containing summary information.</returns>
    /// <exception cref="ArgumentException">Thrown if end time is not later than start time.</exception>
    public Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>> GetTransferSummary(
        DateTime? start = null,
        DateTime? end = null,
        TransferDirection? direction = null,
        string username = null)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time");
        }

        var dict = new Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>()
        {
            { TransferDirection.Download, new Dictionary<TransferStates, TransferSummary>() },
            { TransferDirection.Upload, new Dictionary<TransferStates, TransferSummary>() },
        };

        var sql = @$"
            SELECT
                Direction,
                StateDescription,
                SUM(Size) AS TotalBytes,
                COUNT(*) AS Count,
                COUNT(DISTINCT Username) AS DistinctUsers,
                AVG(AverageSpeed) AS AverageSpeed,
                AVG(strftime('%s', StartedAt) - strftime('%s', EnqueuedAt)) AS AverageWait,
                AVG(strftime('%s', EndedAt) - strftime('%s', StartedAt)) AS AverageDuration
            FROM Transfers
            WHERE EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
                AND State IN ({string.Join(", ", TransferStateCategories.Completed.Select(s => (int)s))})
                {(direction is not null ? "AND Direction = @Direction" : string.Empty)}
                {(username is not null ? "AND Username = @Username" : string.Empty)}
            GROUP BY Direction, StateDescription
        ";

        using var connection = new SqliteConnection(ConnectionStrings[Database.Transfers]);

        var param = new
        {
            Direction = direction?.ToString(),
            Username = username,
            Start = start,
            End = end,
        };

        var results = connection.Query<TransferSummaryRow>(sql, param);

        foreach (var result in results)
        {
            var record = new TransferSummary
            {
                TotalBytes = result.TotalBytes,
                Count = result.Count,
                DistinctUsers = result.DistinctUsers,
                AverageSpeed = result.AverageSpeed,
                AverageWait = result.AverageWait,
                AverageDuration = result.AverageDuration,
            };

            dict[result.Direction].Add(result.StateDescription & ~TransferStates.Completed, record);
        }

        return dict;
    }

    /// <summary>
    ///     Returns a histogram of all transfer data, aggregated into either fixed size time intervals or a fixed
    ///     number of evenly sized buckets, and grouped by direction and final transfer state. Includes only
    ///     completed transfers.
    /// </summary>
    /// <param name="start">The start time of the histogram window.</param>
    /// <param name="end">The end time of the histogram window.</param>
    /// <param name="interval">
    ///     The interval for the histogram. Mutually exclusive with <paramref name="buckets"/>; exactly one must
    ///     be specified.
    /// </param>
    /// <param name="buckets">
    ///     The number of evenly sized buckets into which the histogram window should be divided. Mutually
    ///     exclusive with <paramref name="interval"/>; exactly one must be specified.
    /// </param>
    /// <param name="direction">The optional direction (Upload or Download) by which to filter results.</param>
    /// <param name="username">The optional username by which to filter results.</param>
    /// <returns>A nested dictionary keyed by direction and state and containing summary information.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if either <paramref name="start"/> or <paramref name="end"/> is default, if both <paramref name="interval"/>
    ///     and <paramref name="buckets"/> are specified, if neither is specified.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown if <paramref name="end"/> is not later than <paramref name="start"/>, or if <paramref name="buckets"/>
    ///     is outside of the allowed range.
    /// </exception>
    public Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>> GetTransferHistogram(
        DateTime start,
        DateTime end,
        TimeSpan? interval = null,
        int? buckets = null,
        TransferDirection? direction = null,
        string username = null)
    {
        if (start == default || end == default)
        {
            throw new ArgumentException("Start and end times are both required");
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End time must be later than start time");
        }

        if (interval.HasValue && buckets.HasValue)
        {
            throw new ArgumentException("Interval and buckets are mutually exclusive, provide one or the other, not both");
        }

        if (!interval.HasValue && !buckets.HasValue)
        {
            throw new ArgumentException("Either interval or buckets must be supplied");
        }

        if (buckets.HasValue && (buckets.Value > 1000 || buckets.Value < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(buckets), buckets, "Buckets must be between 1 and 1000");
        }

        if (interval.HasValue && interval.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be greater than zero");
        }

        // if interval was provided, just use that. otherwise, compute the interval from the seconds between start and end
        // divided by the desired buckets
        var effectiveInterval = interval ?? TimeSpan.FromSeconds(Math.Max(1, (long)(end - start).TotalSeconds / buckets!.Value));

        var dict = new Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>>();

        // fill the dictionary with gapless intervals and empty dictionaries
        // snap to Unix epoch to match the SQL bucket formula: strftime('%s', ...) / interval * interval, otherwise
        // we may end up with an empty/partial first bucket
        var intervalSeconds = (long)effectiveInterval.TotalSeconds;
        var currentInterval = DateTime.UnixEpoch.AddSeconds((long)(start - DateTime.UnixEpoch).TotalSeconds / intervalSeconds * intervalSeconds);

        while (currentInterval < end)
        {
            dict[currentInterval] = new Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>()
            {
                { TransferDirection.Download, new Dictionary<TransferStates, TransferSummary>() },
                { TransferDirection.Upload, new Dictionary<TransferStates, TransferSummary>() },
            };
            currentInterval = currentInterval.Add(effectiveInterval);
        }

        var sql = @$"
            SELECT
                datetime(strftime('%s', EndedAt) / CAST(@Interval AS INTEGER) * CAST(@Interval AS INTEGER), 'unixepoch') AS Interval,
                Direction,
                StateDescription,
                SUM(Size) AS TotalBytes,
                COUNT(*) AS Count,
                COUNT(DISTINCT Username) AS DistinctUsers,
                COALESCE(CAST(SUM(Size) AS REAL) / NULLIF(SUM(strftime('%s', EndedAt) - strftime('%s', StartedAt)), 0), 0.0) AS AverageSpeed,
                COALESCE(AVG(strftime('%s', StartedAt) - strftime('%s', EnqueuedAt)), 0.0) AS AverageWait,
                COALESCE(AVG(strftime('%s', EndedAt) - strftime('%s', StartedAt)), 0.0) AS AverageDuration
            FROM Transfers
            WHERE EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
                AND State IN ({string.Join(", ", TransferStateCategories.Completed.Select(s => (int)s))})
                {(direction is not null ? "AND Direction = @Direction" : string.Empty)}
                {(username is not null ? "AND Username = @Username" : string.Empty)}
            GROUP BY Interval, Direction, StateDescription
            ORDER BY Interval, Direction, StateDescription;
        ";

        using var connection = new SqliteConnection(ConnectionStrings[Database.Transfers]);

        var param = new
        {
            Direction = direction?.ToString(),
            Username = username,
            Start = start,
            End = end,
            Interval = (int)effectiveInterval.TotalSeconds,
        };

        var results = connection.Query<TransferSummaryRow>(sql, param);

        foreach (var result in results)
        {
            var record = new TransferSummary
            {
                TotalBytes = result.TotalBytes,
                Count = result.Count,
                DistinctUsers = result.DistinctUsers,
                AverageSpeed = result.AverageSpeed,
                AverageWait = result.AverageWait,
                AverageDuration = result.AverageDuration,
            };

            if (result.Interval.HasValue)
            {
                var bucketInterval = DateTime.SpecifyKind(result.Interval.Value, DateTimeKind.Utc);

                if (dict.TryGetValue(bucketInterval, out var intervalDict))
                {
                    intervalDict[result.Direction][result.StateDescription & ~TransferStates.Completed] = record;
                }
            }
        }

        return dict;
    }

    /// <summary>
    ///     Returns the top N users summaries by count, total bytes, or average speed. Only successful transfers are included in counts.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download).</param>
    /// <param name="start">The optional start time of the summary window.</param>
    /// <param name="end">The optional end time of the summary window.</param>
    /// <param name="sortBy">The property by which to sort.</param>
    /// <param name="sortOrder">The sort order.</param>
    /// <param name="limit">The number of records to return (default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns>A dictionary keyed by username and containing summary information.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if end time is not later than start time, or limit is not greater than zero.
    /// </exception>
    public List<TransferSummary> GetTransferLeaderboard(
        TransferDirection direction,
        DateTime? start = null,
        DateTime? end = null,
        LeaderboardSortBy sortBy = LeaderboardSortBy.Count,
        SortOrder sortOrder = SortOrder.DESC,
        int limit = 25,
        int offset = 0)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException("Limit must be greater than zero");
        }

        var sql = @$"
            SELECT
                Username,
                Direction,
                State,
                SUM(Size) AS TotalBytes,
                COUNT(*) AS Count,
                AVG(AverageSpeed) AS AverageSpeed,
                AVG(strftime('%s', StartedAt) - strftime('%s', EnqueuedAt)) AS AverageWait,
                AVG(strftime('%s', EndedAt) - strftime('%s', StartedAt)) AS AverageDuration
            FROM Transfers
            WHERE Direction = @Direction
                AND State = 48
                AND EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
            GROUP BY Username, Direction, StateDescription
            ORDER BY {sortBy} {sortOrder}, Username DESC
            LIMIT @Limit
            OFFSET @Offset
        ";

        using var connection = new SqliteConnection(ConnectionStrings[Database.Transfers]);

        var param = new
        {
            Direction = direction.ToString(),
            Limit = limit,
            Offset = offset,
            Start = start,
            End = end,
        };

        var results = connection.Query<TransferSummaryRow>(sql, param);

        var list = results.Select(row => new TransferSummary
        {
            Username = row.Username,
            TotalBytes = row.TotalBytes,
            Count = row.Count,
            AverageSpeed = row.AverageSpeed,
            AverageWait = row.AverageWait,
            AverageDuration = row.AverageDuration,
        }).ToList();

        return list;
    }

    /// <summary>
    ///     Returns a list of transfer exceptions.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download).</param>
    /// <param name="start">The optional start time of the summary window.</param>
    /// <param name="end">The optional end time of the summary window.</param>
    /// <param name="username">The optional username by which to filter results.</param>
    /// <param name="sortOrder">The sort order.</param>
    /// <param name="limit">The number of records to return (default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns>A list of exceptions.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if end time is not later than start time, or limit is not greater than zero.
    /// </exception>
    public List<TransferExceptionDetail> GetTransferExceptions(
        TransferDirection direction,
        DateTime? start = null,
        DateTime? end = null,
        string username = null,
        SortOrder sortOrder = SortOrder.DESC,
        int limit = 25,
        int offset = 0)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException("Limit must be greater than zero");
        }

        var sql = @$"
            SELECT
                Id,
                Username,
                Direction,
                Filename,
                Size,
                State,
                RequestedAt,
                EnqueuedAt,
                StartedAt,
                EndedAt,
                BytesTransferred,
                AverageSpeed,
                CASE
                    WHEN INSTR(Exception, ':') > 0
                    THEN SUBSTR(Exception, INSTR(Exception, ':') + 2)
                    ELSE Exception
                END as Exception
            FROM transfers
            WHERE state & ~48
                AND Direction = @Direction
                AND EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
                {(username is not null ? "AND Username = @Username" : string.Empty)}
            ORDER BY EndedAt {sortOrder}
            LIMIT @Limit
            OFFSET @Offset
        ";

        using var connection = new SqliteConnection(ConnectionStrings[Database.Transfers]);

        var param = new
        {
            Direction = direction.ToString(),
            Username = username,
            Start = start,
            End = end,
            Limit = limit,
            Offset = offset,
        };

        var results = connection.Query<TransferExceptionDetail>(sql, param);
        return results.ToList();
    }

    /// <summary>
    ///     Returns a summary of distinct transfer exceptions and the number of times they occurred.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download) for the summary.</param>
    /// <param name="start">The optional start time of the summary window.</param>
    /// <param name="end">The optional end time of the summary window.</param>
    /// <param name="username">The optional username by which to filter results.</param>
    /// <param name="limit">The number of records to return (default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns>A dictionary keyed by direction and containing a list of exceptions and the number of times they occurred.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if end time is not later than start time, or limit is not greater than zero.
    /// </exception>
    public List<TransferExceptionSummary> GetTransferExceptionsPareto(
        TransferDirection direction,
        DateTime? start = null,
        DateTime? end = null,
        string username = null,
        int limit = 25,
        int offset = 0)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException("Limit must be greater than zero");
        }

        var sql = @$"
            SELECT
                CASE
                    WHEN INSTR(Exception, ':') > 0
                    THEN SUBSTR(Exception, INSTR(Exception, ':') + 2)
                    ELSE Exception
                END as Exception,
                COUNT(*) as Count,
                COUNT(DISTINCT Username) as DistinctUsers
            FROM transfers
            WHERE state & ~48
                AND Direction = @Direction
                AND EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
                {(username is not null ? "AND Username = @Username" : string.Empty)}
            GROUP BY Direction,
            CASE
                WHEN INSTR(Exception, ':') > 0
                THEN SUBSTR(Exception, INSTR(Exception, ':') + 2)
                ELSE Exception
            END
            ORDER BY Count DESC
            LIMIT @Limit
            OFFSET @Offset
        ";

        using var connection = new SqliteConnection(ConnectionStrings[Database.Transfers]);

        var param = new
        {
            Direction = direction.ToString(),
            Username = username,
            Start = start,
            End = end,
            Limit = limit,
            Offset = offset,
        };

        var results = connection.Query<TransferExceptionSummary>(sql, param);
        return results.ToList();
    }

    /// <summary>
    ///     Returns a list of directories and the frequency at which they have been downloaded.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="username"></param>
    /// <param name="sortOrder"></param>
    /// <param name="limit"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if end time is not later than start time, or limit is not greater than zero.
    /// </exception>
    public List<TransferDirectorySummary> GetTransferDirectoryFrequency(
        DateTime? start = null,
        DateTime? end = null,
        string username = null,
        SortOrder sortOrder = SortOrder.DESC,
        int limit = 25,
        int offset = 0)
    {
        start ??= DateTime.MinValue;
        end ??= DateTime.MaxValue;

        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException("Limit must be greater than zero");
        }

        var sql = @$"
            WITH directories AS (
                SELECT
                    RTRIM(
                        RTRIM(filename, REPLACE(REPLACE(filename, '/', ''), '\', '')), '/\'
                    ) AS Directory,
                    Username
                FROM transfers 
                WHERE Direction = 'Upload'
                AND State = 48
                AND EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
                {(username is not null ? "AND Username = @Username" : string.Empty)}
            )
            SELECT Directory, COUNT(*) AS Count, COUNT(DISTINCT Username) AS DistinctUsers
            FROM directories
            GROUP BY Directory
            ORDER BY DistinctUsers DESC, Count DESC
            LIMIT @Limit
            OFFSET @Offset
        ";

        using var connection = new SqliteConnection(ConnectionStrings[Database.Transfers]);

        var param = new
        {
            Start = start,
            End = end,
            Limit = limit,
            Offset = offset,
            Username = username,
        };

        var results = connection.Query<TransferDirectorySummary>(sql, param);
        return results.ToList();
    }

    private record TransferSummaryRow
    {
        public DateTime? Interval { get; init; }
        public string Username { get; init; }
        public TransferDirection Direction { get; init; }
        public TransferStates StateDescription { get; init; }
        public long TotalBytes { get; init; }
        public long Count { get; init; }
        public long DistinctUsers { get; init; }
        public double AverageSpeed { get; init; }
        public double AverageWait { get; init; }
        public double AverageDuration { get; init; }
    }
}