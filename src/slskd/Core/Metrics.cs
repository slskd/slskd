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

namespace slskd
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Prometheus;

    public static class Metrics
    {
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
                    Buckets = Histogram.ExponentialBuckets(1, 2, 10),
                });

            /// <summary>
            ///     Gets a counter representing the total number of search requests received.
            /// </summary>
            public static Counter RequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_search_requests_received", "Total number of search requests received");

            /// <summary>
            ///     Gets a counter representing the total number of search responses sent.
            /// </summary>
            public static Counter ResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_search_responses_sent", "Total number of search responses sent");
        }

        public static class Browse
        {
            /// <summary>
            ///     Gets a counter representing the total number of browse responses sent.
            /// </summary>
            public static Counter ResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_browse_responses_sent", "Total number of browse responses sent");

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
        }

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
        }

        /// <summary>
        ///     Measure the duration of the provided <paramref name="action"/> with the specified <paramref name="histogram"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="histogram"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static T Measure<T>(Histogram histogram, Func<T> action)
        {
            var sw = new Stopwatch();
            sw.Start();

            var result = action();

            sw.Stop();

            histogram.Observe(sw.ElapsedMilliseconds);

            return result;
        }

        /// <summary>
        ///     Measure the duration of the provided <paramref name="action"/> with the specified <paramref name="histogram"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="histogram"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static async Task<T> MeasureAsync<T>(Histogram histogram, Func<Task<T>> action)
        {
            var sw = new Stopwatch();
            sw.Start();

            var result = await action();

            sw.Stop();

            histogram.Observe(sw.ElapsedMilliseconds);

            return result;
        }
    }
}
