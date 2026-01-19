// <copyright file="Metrics.cs" company="slskd Team">
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
            ///     This metric previously represented a per-second rate; changing the period to one minute is a
            ///     breaking change for consumers that assumed requests per second.
            /// </summary>
            public static TimedCounter CurrentRequestReceiveRate { get; } = new TimedCounter(TimeSpan.FromMinutes(1), onElapsed: count => CurrentRequestReceiveRateGauge.Set(count));

            /// <summary>
            ///     Gets a counter representing the total number of search requests dropped due to processing pressure.
            /// </summary>
            public static Counter RequestsDropped { get; } = Prometheus.Metrics.CreateCounter("slskd_search_incoming_requests_dropped", "Total number of search requests dropped due to processing pressure");

            /// <summary>
            ///     Gets an automatically resetting counter of the number of search requests dropped due to processing pressure.
            /// </summary>
            public static TimedCounter CurrentRequestDropRate { get; } = new TimedCounter(TimeSpan.FromMinutes(1), onElapsed: count => CurrentRequestDropRateGauge.Set(count));

            /// <summary>
            ///     Gets a counter representing the total number of search responses sent.
            /// </summary>
            public static Counter ResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_search_incoming_responses_sent", "Total number of search responses sent");

            /// <summary>
            ///     Gets an automatically resetting counter of the number of search responses sent.
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