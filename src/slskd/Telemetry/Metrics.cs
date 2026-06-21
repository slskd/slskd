// <copyright file="Metrics.cs" company="JP Dillingham">
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
using System.Threading;
using Prometheus;

/// <summary>
///     This class contains all of the custom Prometheus metrics for slskd.  These represent a small subset of the metrics
///     available, with the rest coming from the .NET and ASP.NET frameworks and the host system.
/// </summary>
/// <remarks>
///     These metrics and their names are part of the public API contract; avoid renaming or removing them, and think
///     carefully about naming when adding new metrics.
/// </remarks>
public static class Metrics
{
    private static SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);

    /// <summary>
    ///     Updates metrics under a mutex to prevent mismatches and under/overruns.
    /// </summary>
    /// <remarks>
    ///     This method uses a <see cref="SemaphoreSlim"/> for synchronization and it is therefore safe to do async work
    ///     inside of <paramref name="work"/>, though it is discouraged for causing un-needed contention.
    /// </remarks>
    /// <param name="work">Updates to execute.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static void Update(Action work, CancellationToken cancellationToken = default)
    {
        SyncRoot.Wait(cancellationToken);

        try
        {
            work.Invoke();
        }
        finally
        {
            SyncRoot.Release();
        }
    }

    /// <summary>
    ///     Metrics related to search requests and responses.
    /// </summary>
    public static class Search
    {
        /// <summary>
        ///     Metrics related to incoming search requests and responses.
        /// </summary>
        public static class Incoming
        {
            /// <summary>
            ///     Gets a counter representing the total number of search requests received.
            /// </summary>
            public static Counter RequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_search_incoming_requests_received", "Total number of search requests received");

            /// <summary>
            ///     Gets an automatically resetting counter of the number of search requests received per minute.
            /// </summary>
            public static TimedCounter CurrentRequestReceiveRate { get; } = new TimedCounter(TimeSpan.FromMinutes(1), onElapsed: count => CurrentRequestReceiveRateGauge.Set(count));

            /// <summary>
            ///     Gets a counter representing the total number of search requests dropped due to processing pressure.
            /// </summary>
            public static Counter RequestsDropped { get; } = Prometheus.Metrics.CreateCounter("slskd_search_incoming_requests_dropped", "Total number of search requests dropped due to processing pressure");

            /// <summary>
            ///     Gets an automatically resetting counter of the number of search requests dropped due to processing pressure per minute.
            /// </summary>
            public static TimedCounter CurrentRequestDropRate { get; } = new TimedCounter(TimeSpan.FromMinutes(1), onElapsed: count => CurrentRequestDropRateGauge.Set(count));

            /// <summary>
            ///     Gets a counter representing the total number of search responses sent.
            /// </summary>
            public static Counter ResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_search_incoming_responses_sent", "Total number of search responses sent");

            /// <summary>
            ///     Gets an automatically resetting counter of the number of search responses sent per minute.
            /// </summary>
            public static TimedCounter CurrentResponseSendRate { get; } = new TimedCounter(TimeSpan.FromMinutes(1), onElapsed: count => CurrentResponseSendRateGauge.Set(count));

            /// <summary>
            ///     Gets a histogram representing the time taken to resolve and return a response to an incoming search request, in milliseconds.
            /// </summary>
            public static Histogram ResponseLatency { get; } = Prometheus.Metrics.CreateHistogram(
                "slskd_search_incoming_response_latency",
                "The time taken to resolve and return a response to an incoming search request, in milliseconds",
                new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(0.1, 2, 11),
                });

            /// <summary>
            ///     Gets an EMA representing the average time taken to resolve a response to an incoming search request, in milliseconds.
            /// </summary>
            public static ExponentialMovingAverage CurrentResponseLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentResponseLatencyGauge.Set(value));

            /// <summary>
            ///     Gets a gauge representing the number of incoming search requests waiting to be processed.
            /// </summary>
            public static Gauge CurrentRequestQueueDepth { get; } = Prometheus.Metrics.CreateGauge("slskd_search_incoming_request_queue_depth_current", "The number of incoming search requests waiting to be processed");

            private static Gauge CurrentResponseLatencyGauge { get; } =
                Prometheus.Metrics.CreateGauge("slskd_search_incoming_response_latency_current", "The average time taken to resolve and return a response to an incoming search request, in milliseconds");
            private static Gauge CurrentRequestReceiveRateGauge { get; } =
                Prometheus.Metrics.CreateGauge("slskd_search_incoming_request_receive_rate_current", "Number of search requests received in the last minute");
            private static Gauge CurrentRequestDropRateGauge { get; } =
                Prometheus.Metrics.CreateGauge("slskd_search_incoming_request_drop_rate_current", "Number of search requests dropped in the last minute");
            private static Gauge CurrentResponseSendRateGauge { get; } =
                Prometheus.Metrics.CreateGauge("slskd_search_incoming_response_send_rate_current", "Number of search responses sent in the last minute");

            /// <summary>
            ///     Metrics related to the filtering of incoming search requests.
            /// </summary>
            public static class Filter
            {
                /// <summary>
                ///     Gets a histogram representing the time taken to apply filters to an incoming search request, in milliseconds.
                /// </summary>
                public static Histogram Latency { get; } = Prometheus.Metrics.CreateHistogram(
                    "slskd_search_incoming_filter_latency",
                    "The time taken to apply filters to an incoming search request, in milliseconds",
                    new HistogramConfiguration
                    {
                        Buckets = Histogram.ExponentialBuckets(0.1, 2, 11),
                    });

                /// <summary>
                ///     Gets an EMA representing the average time taken to apply filters to an incoming search request, in milliseconds.
                /// </summary>
                public static ExponentialMovingAverage CurrentLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentLatencyGauge.Set(value));

                private static Gauge CurrentLatencyGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_search_incoming_filter_latency_current", "The average time taken to apply filters to an incoming search request, in milliseconds");
            }

            /// <summary>
            ///     Metrics related to the querying of shares for search results.
            /// </summary>
            public static class Query
            {
                /// <summary>
                ///     Gets a histogram representing the time taken to query share database(s) for results, in milliseconds.
                /// </summary>
                public static Histogram Latency { get; } = Prometheus.Metrics.CreateHistogram(
                    "slskd_search_incoming_query_latency",
                    "The time taken to query share database(s) for results, in milliseconds",
                    new HistogramConfiguration
                    {
                        Buckets = Histogram.ExponentialBuckets(0.1, 2, 11),
                    });

                /// <summary>
                ///     Gets an EMA representing the average time taken to query share database(s) for results, in milliseconds.
                /// </summary>
                public static ExponentialMovingAverage CurrentLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentLatencyGauge.Set(value));
                private static Gauge CurrentLatencyGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_search_incoming_query_latency_current", "The average time taken to query share database(s) for results, in milliseconds");
            }
        }
    }

    /// <summary>
    ///     Metrics related to browse requests and responses.
    /// </summary>
    public static class Browse
    {
        /// <summary>
        ///     Gets a histogram representing the time taken to resolve a response to an incoming browse request, in milliseconds.
        /// </summary>
        public static Histogram ResponseLatency { get; } = Prometheus.Metrics.CreateHistogram(
            "slskd_browse_response_latency",
            "The time taken to resolve a response to an incoming browse request, in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 1, factor: 2, count: 10),
            });

        /// <summary>
        ///     Gets an EMA representing the average time taken to resolve a response to an incoming search request, in milliseconds.
        /// </summary>
        public static ExponentialMovingAverage CurrentResponseLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentResponseLatencyGauge.Set(value));

        /// <summary>
        ///     Gets a counter representing the total number of browse requests received.
        /// </summary>
        public static Counter RequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_browse_requests_received", "Total number of browse requests received");

        /// <summary>
        ///     Gets a counter representing the total number of browse responses sent.
        /// </summary>
        public static Counter ResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_browse_responses_sent", "Total number of browse responses sent");

        private static Gauge CurrentResponseLatencyGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_browse_response_latency_current", "The average time taken to resolve a response to an incoming browse request, in milliseconds");
    }

    /// <summary>
    ///     Metrics related to incoming enqueue requests and responses.
    /// </summary>
    public static class Enqueue
    {
        /// <summary>
        ///     Gets a histogram representing the time taken to evaluate an incoming request to enqueue a file against configured limits, in milliseconds.
        /// </summary>
        public static Histogram DecisionLatency { get; } = Prometheus.Metrics.CreateHistogram(
            "slskd_enqueue_decision_latency",
            "The time taken to evaluate an incoming request to enqueue a file against configured limits, in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 1, factor: 2, count: 16), // up to ~2 seconds
            });

        /// <summary>
        ///     Gets an EMA representing the average time taken to evaluate an incoming request to enqueue a file against configured limits, in milliseconds.
        /// </summary>
        public static ExponentialMovingAverage CurrentDecisionLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentDecisionLatencyGauge.Set(value));

        /// <summary>
        ///     Gets a histogram representing the total time taken to resolve a response to an incoming request to enqueue a file, in milliseconds.
        /// </summary>
        public static Histogram Latency { get; } = Prometheus.Metrics.CreateHistogram(
            "slskd_enqueue_latency",
            "The total time taken to resolve a response to an incoming request to enqueue a file, in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 1, factor: 2, count: 16), // up to ~2 seconds
            });

        /// <summary>
        ///     Gets an EMA representing the average total time taken to resolve a response to an incoming request to enqueue a file, in milliseconds.
        /// </summary>
        public static ExponentialMovingAverage CurrentLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentLatencyGauge.Set(value));

        /// <summary>
        ///     Gets a counter representing the total number of incoming enqueue requests received.
        /// </summary>
        public static Counter RequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_enqueue_requests_received", "Total number of incoming enqueue requests received");

        /// <summary>
        ///     Gets a counter representing the total number of incoming enqueue requests dropped due to processing pressure.
        /// </summary>
        public static Counter RequestsDropped { get; } = Prometheus.Metrics.CreateCounter("slskd_enqueue_requests_dropped", "Total number of incoming enqueue requests dropped due to processing pressure");

        /// <summary>
        ///     Gets a counter representing the total number of incoming enqueue requests rejected.
        /// </summary>
        public static Counter RequestsRejected { get; } = Prometheus.Metrics.CreateCounter("slskd_enqueue_requests_rejected", "Total number of incoming enqueue requests rejected");

        /// <summary>
        ///     Gets a counter representing the total number of incoming enqueue requests accepted.
        /// </summary>
        public static Counter RequestsAccepted { get; } = Prometheus.Metrics.CreateCounter("slskd_enqueue_requests_accepted", "Total number of incoming enqueue requests accepted");

        /// <summary>
        ///     Gets a gauge representing the number of incoming enqueue requests waiting to be processed.
        /// </summary>
        public static Gauge CurrentQueueDepth { get; } = Prometheus.Metrics.CreateGauge("slskd_enqueue_queue_depth_current", "The number of incoming enqueue requests waiting to be processed");

        private static Gauge CurrentDecisionLatencyGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_enqueue_decision_latency_current", "The average time taken to evaluate an incoming request to enqueue a file against configured limits, in milliseconds");
        private static Gauge CurrentLatencyGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_enqueue_latency_current", "The average total time taken to resolve a response to an incoming request to enqueue a file, in milliseconds");
    }

    /// <summary>
    ///     Metrics related to transfers.
    /// </summary>
    public static class Transfers
    {
        /// <summary>
        ///     Metrics related to uploads.
        /// </summary>
        public static class Uploads
        {
            /// <summary>
            ///     Metrics related to in-progress uploads.
            /// </summary>
            public static class InProgress
            {
                /// <summary>
                ///     Gets a gauge representing the current number of unique users with in-progress uploads.
                /// </summary>
                public static Gauge Users { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_in_progress_users_current",
                    help: "Current number of unique users with in-progress uploads");

                /// <summary>
                ///     Gets a gauge representing the current number of in-progress uploads.
                /// </summary>
                public static Gauge Files { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_in_progress_files_current",
                    help: "Current number of in-progress uploads");

                /// <summary>
                ///     Gets a gauge representing the current total size, in bytes, of in-progress uploads.
                /// </summary>
                public static Gauge Bytes { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_in_progress_bytes_current",
                    help: "Current total size, in bytes, of in-progress uploads");

                /// <summary>
                ///     Gets a gauge representing the current total speed, in bytes per second, of in-progress uploads.
                /// </summary>
                public static ExponentialMovingAverage CurrentTotalSpeed { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentTotalSpeedGauge.Set(value));
                public static Gauge CurrentTotalSpeedGauge { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_in_progress_speed_total_current",
                    help: "Current total speed, in bytes per second, of in-progress uploads");

                /// <summary>
                ///     Gets a gauge representing the current average speed, in bytes per second, of in-progress uploads.
                /// </summary>
                public static ExponentialMovingAverage CurrentAverageSpeed { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentAverageSpeedGauge.Set(value));
                public static Gauge CurrentAverageSpeedGauge { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_in_progress_speed_average_current",
                    help: "Current average speed, in bytes per second, of in-progress uploads");
            }

            /// <summary>
            ///     Metrics related to queued uploads.
            /// </summary>
            public static class Queued
            {
                /// <summary>
                ///     Gets a gauge representing the current number of unique users with queued uploads.
                /// </summary>
                public static Gauge Users { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_queued_users_current",
                    help: "Current number of unique users with queued uploads");

                /// <summary>
                ///     Gets a gauge representing the current number of queued uploads.
                /// </summary>
                public static Gauge Files { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_queued_files_current",
                    help: "Current number of queued uploads");

                /// <summary>
                ///     Gets a gauge representing the current total size, in bytes, of queued uploads.
                /// </summary>
                public static Gauge Bytes { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_queued_bytes_current",
                    help: "Current total size, in bytes, of queued uploads");
            }

            /// <summary>
            ///     Metrics related to completed uploads.
            /// </summary>
            public static class Completed
            {
                /// <summary>
                ///     Gets a counter representing the total number of uploads that completed successfully.
                /// </summary>
                public static Counter Succeeded { get; } = Prometheus.Metrics.CreateCounter(
                    name: "slskd_transfers_uploads_completed_succeeded",
                    help: "Total number of uploads that completed successfully");

                /// <summary>
                ///     Gets a counter representing the total number of bytes transferred by successfully completed uploads.
                /// </summary>
                public static Counter Bytes { get; } = Prometheus.Metrics.CreateCounter(
                    name: "slskd_transfers_uploads_completed_bytes",
                    help: "Total number of bytes transferred by successfully completed uploads");

                /// <summary>
                ///     Gets a counter representing the total number of uploads that failed.
                /// </summary>
                public static Counter Failed { get; } = Prometheus.Metrics.CreateCounter(
                    name: "slskd_transfers_uploads_completed_failed",
                    help: "Total number of uploads that failed");

                /// <summary>
                ///     Gets a gauge representing the current average speed, in bytes per second, of completed downloads.
                /// </summary>
                public static ExponentialMovingAverage CurrentAverageSpeed { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentAverageSpeedGauge.Set(value));
                public static Gauge CurrentAverageSpeedGauge { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_uploads_completed_speed_average_current",
                    help: "Current average speed, in bytes per second, of completed uploads");

                /// <summary>
                ///     Gets a histogram representing the the average speed of completed uploads, in bytes per second.
                /// </summary>
                public static Histogram AverageSpeed { get; } = Prometheus.Metrics.CreateHistogram(
                    name: "slskd_transfers_uploads_completed_speed_average",
                    help: "The average speed of completed uploads, in bytes per second",
                    new HistogramConfiguration
                    {
                        Buckets = [
                            1_024,           // 1 KB/s
                            10_240,          // 10 KB/s
                            51_200,          // 50 KB/s
                            102_400,         // 100 KB/s
                            204_800,         // 200 KB/s
                            307_200,         // 300 KB/s
                            409_600,         // 400 KB/s
                            512_000,         // 500 KB/s
                            1_048_576,       // 1 MB/s
                            2_097_152,       // 2 MB/s
                            4_194_304,       // 4 MB/s
                            6_291_456,       // 6 MB/s
                            10_485_760,      // 10 MB/s
                            26_214_400,      // 25 MB/s
                            52_428_800,      // 50 MB/s
                            104_857_600,     // 100 MB/s
                            262_144_000,     // 250 MB/s
                            524_288_000,     // 500 MB/s
                            1_073_741_824,   // 1 GB/s
                        ],
                    });
            }
        }

        /// <summary>
        ///     Metrics related to downloads.
        /// </summary>
        public static class Downloads
        {
            /// <summary>
            ///     Metrics related to in-progress downloads.
            /// </summary>
            public static class InProgress
            {
                /// <summary>
                ///     Gets a gauge representing the current number of unique users with in-progress downloads.
                /// </summary>
                public static Gauge Users { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_in_progress_users_current",
                    help: "Current number of unique users with in-progress downloads");

                /// <summary>
                ///     Gets a gauge representing the current number of in-progress downloads.
                /// </summary>
                public static Gauge Files { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_in_progress_files_current",
                    help: "Current number of in-progress downloads");

                /// <summary>
                ///     Gets a gauge representing the current total size, in bytes, of in-progress downloads.
                /// </summary>
                public static Gauge Bytes { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_in_progress_bytes_current",
                    help: "Current total size, in bytes, of in-progress downloads");

                /// <summary>
                ///     Gets a gauge representing the current total speed, in bytes per second, of in-progress downloads.
                /// </summary>
                public static ExponentialMovingAverage CurrentTotalSpeed { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentTotalSpeedGauge.Set(value));
                public static Gauge CurrentTotalSpeedGauge { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_in_progress_speed_total_current",
                    help: "Current total speed, in bytes per second, of in-progress downloads");

                /// <summary>
                ///     Gets a gauge representing the current average speed, in bytes per second, of in-progress downloads.
                /// </summary>
                public static ExponentialMovingAverage CurrentAverageSpeed { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentAverageSpeedGauge.Set(value));
                public static Gauge CurrentAverageSpeedGauge { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_in_progress_speed_average_current",
                    help: "Current average speed, in bytes per second, of in-progress downloads");
            }

            /// <summary>
            ///     Metrics related to queued downloads.
            /// </summary>
            public static class Queued
            {
                /// <summary>
                ///     Gets a gauge representing the current number of unique users with queued downloads.
                /// </summary>
                public static Gauge Users { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_queued_users_current",
                    help: "Current number of unique users with queued downloads");

                /// <summary>
                ///     Gets a gauge representing the current number of queued downloads.
                /// </summary>
                public static Gauge Files { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_queued_files_current",
                    help: "Current number of queued downloads");

                /// <summary>
                ///     Gets a gauge representing the current total size, in bytes, of queued downloads.
                /// </summary>
                public static Gauge Bytes { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_queued_bytes_current",
                    help: "Current total size, in bytes, of queued downloads");
            }

            /// <summary>
            ///     Metrics related to completed downloads.
            /// </summary>
            public static class Completed
            {
                /// <summary>
                ///     Gets a counter representing the total number of downloads that completed successfully.
                /// </summary>
                public static Counter Succeeded { get; } = Prometheus.Metrics.CreateCounter(
                    name: "slskd_transfers_downloads_completed_succeeded",
                    help: "Total number of downloads that completed successfully");

                /// <summary>
                ///     Gets a counter representing the total number of bytes transferred by successfully completed downloads.
                /// </summary>
                public static Counter Bytes { get; } = Prometheus.Metrics.CreateCounter(
                    name: "slskd_transfers_downloads_completed_bytes",
                    help: "Total number of bytes transferred by successfully completed downloads");

                /// <summary>
                ///     Gets a counter representing the total number of downloads that failed.
                /// </summary>
                public static Counter Failed { get; } = Prometheus.Metrics.CreateCounter(
                    name: "slskd_transfers_downloads_completed_failed",
                    help: "Total number of downloads that failed");

                /// <summary>
                ///     Gets a gauge representing the current average speed, in bytes per second, of completed downloads.
                /// </summary>
                public static ExponentialMovingAverage CurrentAverageSpeed { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentAverageSpeedGauge.Set(value));
                public static Gauge CurrentAverageSpeedGauge { get; } = Prometheus.Metrics.CreateGauge(
                    name: "slskd_transfers_downloads_completed_speed_average_current",
                    help: "Current average speed, in bytes per second, of completed downloads");

                /// <summary>
                ///     Gets a histogram representing the the average speed of completed downloads, in bytes per second.
                /// </summary>
                public static Histogram AverageSpeed { get; } = Prometheus.Metrics.CreateHistogram(
                    name: "slskd_transfers_downloads_completed_speed_average",
                    help: "The average speed of completed downloads, in bytes per second",
                    new HistogramConfiguration
                    {
                        Buckets = [
                            1_024,           // 1 KB/s
                            10_240,          // 10 KB/s
                            51_200,          // 50 KB/s
                            102_400,         // 100 KB/s
                            204_800,         // 200 KB/s
                            307_200,         // 300 KB/s
                            409_600,         // 400 KB/s
                            512_000,         // 500 KB/s
                            1_048_576,       // 1 MB/s
                            2_097_152,       // 2 MB/s
                            4_194_304,       // 4 MB/s
                            6_291_456,       // 6 MB/s
                            10_485_760,      // 10 MB/s
                            26_214_400,      // 25 MB/s
                            52_428_800,      // 50 MB/s
                            104_857_600,     // 100 MB/s
                            262_144_000,     // 250 MB/s
                            524_288_000,     // 500 MB/s
                            1_073_741_824,   // 1 GB/s
                        ],
                    });
            }
        }
    }

    /// <summary>
    ///     Metrics related to the distributed network.
    /// </summary>
    public static class DistributedNetwork
    {
        /// <summary>
        ///     Gets a gauge representing the current number of connected distributed children.
        /// </summary>
        public static Gauge Children { get; } = Prometheus.Metrics.CreateGauge("slskd_dnet_children", "Current number of connected distributed children");

        /// <summary>
        ///     Gets a gauge representing the current distributed child limit.
        /// </summary>
        public static Gauge ChildLimit { get; } = Prometheus.Metrics.CreateGauge("slskd_dnet_child_limit", "Current distributed child limit");

        /// <summary>
        ///     Gets a gauge indicating whether a distributed parent connection is established.
        /// </summary>
        public static Gauge HasParent { get; } = Prometheus.Metrics.CreateGauge("slskd_dnet_has_parent", "A value indicating whether a distributed parent connection is established");

        /// <summary>
        ///     Gets a gauge representing the current distributed branch level.
        /// </summary>
        public static Gauge BranchLevel { get; } = Prometheus.Metrics.CreateGauge("slskd_dnet_branch_level", "Current distributed branch level");

        /// <summary>
        ///     Gets a gauge representing the most recent average time taken to broadcast incoming search requests to connected children.
        /// </summary>
        public static Gauge CurrentBroadcastLatency { get; } = Prometheus.Metrics.CreateGauge("slskd_dnet_broadcast_latency_current", "The average time taken to broadcast incoming search requests to connected children");

        /// <summary>
        ///     Gets a histogram representing the time taken to resolve a response to an incoming browse request, in milliseconds.
        /// </summary>
        public static Histogram BroadcastLatency { get; } = Prometheus.Metrics.CreateHistogram(
            "slskd_dnet_broadcast_latency",
            "The time taken to broadcast incoming search requests to connected children, in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 11),
            });
    }
}