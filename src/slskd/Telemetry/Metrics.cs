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
        ///     Gets a histogram representing the time taken to resolve a response to an incoming search request, in milliseconds.
        /// </summary>
        public static Histogram ResponseLatency { get; } = Prometheus.Metrics.CreateHistogram(
            "slskd_search_response_latency",
            "The time taken to resolve a response to an incoming search request, in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 11),
            });

        /// <summary>
        ///     Gets an EMA representing the average time taken to resolve a response to an incoming search request, in milliseconds.
        /// </summary>
        public static ExponentialMovingAverage CurrentResponseLatency { get; } = new ExponentialMovingAverage(smoothingFactor: 0.5, onUpdate: value => CurrentResponseLatencyGauge.Set(value));

        /// <summary>
        ///     Gets an automatically resetting counter of the number of search requests received.
        /// </summary>
        public static TimedCounter CurrentRequestReceiveRate { get; } = new TimedCounter(TimeSpan.FromSeconds(1), onElapsed: count => CurrentRequestReceiveRateGauge.Set(count));

        /// <summary>
        ///     Gets a counter representing the total number of search requests received.
        /// </summary>
        public static Counter RequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_search_requests_received", "Total number of search requests received");

        /// <summary>
        ///     Gets a counter representing the total number of search responses sent.
        /// </summary>
        public static Counter ResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_search_responses_sent", "Total number of search responses sent");

        private static Gauge CurrentResponseLatencyGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_search_response_latency_current", "The average time taken to resolve a response to an incoming search request, in milliseconds");
        private static Gauge CurrentRequestReceiveRateGauge { get; } = Prometheus.Metrics.CreateGauge("slskd_search_request_receive_rate_current", "Number of search requests received in the last second");
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
                Buckets = Histogram.ExponentialBuckets(1, 2, 10),
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