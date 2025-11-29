// <copyright file="StatisticsService.cs" company="slskd Team">
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

namespace slskd.Telemetry;

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using Soulseek;

/// <summary>
///     Statistics.
/// </summary>
public class StatisticsService
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StatisticsService"/> class.
    /// </summary>
    public StatisticsService(ConnectionStringDictionary connectionStringDictionary)
    {
        ConnectionStrings = connectionStringDictionary;
    }

    private ConnectionStringDictionary ConnectionStrings { get; }

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
                AVG(AverageSpeed) AS AverageSpeed,
                AVG(strftime('%s', StartedAt) - strftime('%s', EnqueuedAt)) AS AverageWait,
                AVG(strftime('%s', EndedAt) - strftime('%s', StartedAt)) AS AverageDuration
            FROM Transfers
            WHERE EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
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
                AverageSpeed = result.AverageSpeed,
                AverageWait = result.AverageWait,
                AverageDuration = result.AverageDuration,
            };

            dict[result.Direction].Add(result.StateDescription & ~TransferStates.Completed, record);
        }

        return dict;
    }

    /// <summary>
    ///     Returns a summary of all transfer data grouped by direction and final transfer state. Includes only
    ///     completed transfers.
    /// </summary>
    /// <param name="start">The start time of the summary window.</param>
    /// <param name="end">The end time of the summary window.</param>
    /// <param name="interval">The interval for the histogram.</param>
    /// <param name="direction">The optional direction (Upload or Download) for the summary.</param>
    /// <param name="username">The optional username by which to filter results.</param>
    /// <returns>A nested dictionary keyed by direction and state and containing summary information.</returns>
    /// <exception cref="ArgumentException">Thrown if end time is not later than start time.</exception>
    public Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>> GetTransferSummaryHistogram(
        DateTime start,
        DateTime end,
        TimeSpan interval,
        TransferDirection? direction = null,
        string username = null)
    {
        if (end <= start)
        {
            throw new ArgumentException("End time must be later than start time");
        }

        var dict = new Dictionary<DateTime, Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>>();

        // Fill the dictionary with gapless intervals and empty dictionaries
        var currentInterval = new DateTime(start.Ticks / interval.Ticks * interval.Ticks, DateTimeKind.Utc);
        while (currentInterval < end)
        {
            dict[currentInterval] = new Dictionary<TransferDirection, Dictionary<TransferStates, TransferSummary>>()
            {
                { TransferDirection.Download, new Dictionary<TransferStates, TransferSummary>() },
                { TransferDirection.Upload, new Dictionary<TransferStates, TransferSummary>() },
            };
            currentInterval = currentInterval.Add(interval);
        }

        var sql = @$"
            SELECT
                datetime(strftime('%s', EndedAt) / CAST(@Interval AS INTEGER) * CAST(@Interval AS INTEGER), 'unixepoch') AS Interval,
                Direction,
                StateDescription,
                SUM(Size) AS TotalBytes,
                COUNT(*) AS Count,
                AVG(AverageSpeed) AS AverageSpeed,
                COALESCE(AVG(strftime('%s', StartedAt) - strftime('%s', EnqueuedAt)), 0.0) AS AverageWait,
                COALESCE(AVG(strftime('%s', EndedAt) - strftime('%s', StartedAt)), 0.0) AS AverageDuration
            FROM Transfers
            WHERE EndedAt IS NOT NULL
                AND EndedAt BETWEEN @Start AND @End
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
            Interval = (int)interval.TotalSeconds,
        };

        var results = connection.Query<TransferSummaryRow>(sql, param);

        foreach (var result in results)
        {
            var record = new TransferSummary
            {
                TotalBytes = result.TotalBytes,
                Count = result.Count,
                AverageSpeed = result.AverageSpeed,
                AverageWait = result.AverageWait,
                AverageDuration = result.AverageDuration,
            };

            if (result.Interval.HasValue)
            {
                // Normalize the result interval to match the bucket interval
                var bucketInterval = new DateTime(result.Interval.Value.Ticks / interval.Ticks * interval.Ticks, DateTimeKind.Utc);

                if (dict.TryGetValue(bucketInterval, out var intervalDict))
                {
                    intervalDict[result.Direction][result.StateDescription & ~TransferStates.Completed] = record;
                }
            }
        }

        return dict;
    }

    /// <summary>
    ///     Returns the top <paramref name="limit"/> user summaries by total count and direction, order by total count
    ///     descending. Only successful transfers are included in counts.
    /// </summary>
    /// <param name="direction">The direction (Upload or Download) for the summary.</param>
    /// <param name="start">The optional start time of the summary window.</param>
    /// <param name="end">The optional end time of the summary window.</param>
    /// <param name="limit">The number of records to return (default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns>A dictionary keyed by username and containing summary information.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if end time is not later than start time, or limit is not greater than zero.
    /// </exception>
    public List<TransferSummary> GetTransferRanking(
        TransferDirection direction,
        DateTime? start = null,
        DateTime? end = null,
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
                StateDescription,
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
            ORDER BY Count DESC, TotalBytes DESC, Username ASC
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
    ///     Returns a summary of distinct transfer errors and the number of times they occurred.
    /// </summary>
    /// <param name="start">The optional start time of the summary window.</param>
    /// <param name="end">The optional end time of the summary window.</param>
    /// <param name="direction">The direction (Upload or Download) for the summary.</param>
    /// <param name="username">The optional username by which to filter results.</param>
    /// <param name="limit">The number of records to return (default: 25).</param>
    /// <param name="offset">The record offset (if paginating).</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">
    ///     Thrown if end time is not later than start time, or limit is not greater than zero.
    /// </exception>
    public Dictionary<string, long> GetTransferErrorSummary(
        DateTime? start = null,
        DateTime? end = null,
        TransferDirection? direction = null,
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

        var dict = new Dictionary<string, long>();

        var sql = @$"
            SELECT 
                Direction,
                CASE 
                    WHEN INSTR(Exception, ':') > 0 
                    THEN SUBSTR(Exception, INSTR(Exception, ':') + 2)
                    ELSE Exception
                END as Exception,
                COUNT(*) as Count
            FROM transfers 
            WHERE state & ~48
                {(direction is not null ? "AND Direction = @Direction" : string.Empty)}
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

        var results = connection.Query<ErrorSummaryRow>(sql, param);

        foreach (var result in results)
        {
            dict.Add(result.Exception, result.Count);
        }

        return dict;
    }

    private record TransferSummaryRow
    {
        public DateTime? Interval { get; init; }
        public string Username { get; init; }
        public TransferDirection Direction { get; init; }
        public TransferStates StateDescription { get; init; }
        public long TotalBytes { get; init; }
        public long Count { get; init; }
        public double AverageSpeed { get; init; }
        public double AverageWait { get; init; }
        public double AverageDuration { get; init; }
    }

    private record ErrorSummaryRow
    {
        public string Exception { get; init; }
        public long Count { get; init; }
    }
}