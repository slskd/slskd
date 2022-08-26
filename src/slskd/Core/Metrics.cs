using Prometheus;

namespace slskd
{
    public static class Metrics
    {
        public static Histogram SearchResponseLatency { get; } = Prometheus.Metrics
            .CreateHistogram("slskd_search_response_latency", "Histogram of received order values (in USD).",
                new HistogramConfiguration
                {
                    // We divide measurements in 10 buckets of $100 each, up to $1000.
                    Buckets = Histogram.ExponentialBuckets(1, 2, 10),
                });

        public static Counter SearchRequestsReceived { get; } = Prometheus.Metrics.CreateCounter("slskd_search_requests_received", "total number of search requests");
        public static Counter SearchResponsesSent { get; } = Prometheus.Metrics.CreateCounter("slskd_search_responses_sent", "total number of search responses sent");
    }
}
