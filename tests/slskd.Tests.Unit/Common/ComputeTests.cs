namespace slskd.Tests.Unit
{
    using Xunit;

    public class ComputeTests
    {
        [Theory]
        [InlineData(1, 300000, 0)]
        [InlineData(2, 300000, 1000)]
        [InlineData(3, 300000, 3000)]
        [InlineData(4, 300000, 7000)]
        [InlineData(5, 300000, 15000)]
        [InlineData(6, 300000, 31000)]
        [InlineData(999999, 300000, 300000)]
        public void ExponentialBackoffDelay(int iteration, int maxDelayInMs, int expectedDelay)
        {
            var (computedDelay, _) = Compute.ExponentialBackoffDelay(iteration, maxDelayInMs);
            Assert.Equal(expectedDelay, computedDelay);
        }
    }
}
