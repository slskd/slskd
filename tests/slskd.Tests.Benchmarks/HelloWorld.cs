namespace slskd.Tests.Benchmarks
{
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using Xunit;

    public class HelloWorldBenchmarks
    {
        [Benchmark]
        public void LoopOneHundredTimes()
        {
            for (var i = 0; i < 100; i++)
            {
                _ = i;
            }
        }
    }

    public class HelloWorld
    {
        [Fact]
        public void RunHelloWorldBenchmark()
        {
            var summary = BenchmarkRunner.Run<HelloWorldBenchmarks>();

            Assert.NotNull(summary);
        }
    }
}
