using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Prometheus;

namespace slskd
{
    public static class Metrics
    {
        public static Histogram SearchResponseLatency { get; } = Prometheus.Metrics
            .CreateHistogram($"slskd_search_response_latency", "The time taken to resolve a response to an incoming search request, in milliseconds",
                new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(1, 2, 10),
                });

        public static Counter SearchRequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_search_requests_received", "Total number of search requests received");
        public static Counter SearchResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_search_responses_sent", "Total number of search responses sent");

        public static Counter BrowseResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_browse_responses_sent", "Total number of browse responses sent");
        public static Histogram BrowseResponseLatency { get; } = Prometheus.Metrics.CreateHistogram("slskd_browse_response_latency", "The time taken to resolve a response to an incoming browse request, in milliseconds",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(1, 2, 10),
            });

        public static T Measure<T>(Histogram histogram, Func<T> action)
        {
            var sw = new Stopwatch();
            sw.Start();

            var result = action();

            sw.Stop();

            histogram.Observe(sw.ElapsedMilliseconds);

            return result;
        }

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
