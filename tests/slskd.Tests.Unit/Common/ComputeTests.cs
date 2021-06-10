namespace slskd.Tests.Unit
{
    using Xunit;

    public class ComputeTests
    {
        [Theory]
        [InlineData(0, 1, 2, 300, 0)]
        [InlineData(1, 1, 2, 300, 1)]
        [InlineData(2, 1, 2, 300, 3)]
        [InlineData(3, 1, 2, 300, 7)]
        [InlineData(4, 1, 2, 300, 15)]
        [InlineData(5, 1, 2, 300, 31)]
        [InlineData(999999, 1, 2, 300, 300)]
        public void ExponentialBackoffDelay(int iteration, int delayInSeconds, int backoffRate, int maxDelayInSeconds, int expectedDelay)
        {
            var computedDelay = Compute.ExponentialBackoffDelay(iteration, delayInSeconds, backoffRate, maxDelayInSeconds);
            Assert.Equal(expectedDelay, computedDelay);
        }
    }
}
