namespace slskd.Tests.Unit.Common
{
    using Xunit;

    public class ComputeTests
    {
        [Theory]
        // base = 1000 (original cases)
        [InlineData(1, 1000, 300000, 0)]
        [InlineData(2, 1000, 300000, 1000)]
        [InlineData(3, 1000, 300000, 3000)]
        [InlineData(4, 1000, 300000, 7000)]
        [InlineData(5, 1000, 300000, 15000)]
        [InlineData(6, 1000, 300000, 31000)]
        [InlineData(999999, 1000, 300000, 300000)]
        // base = 500 — delay should be half of base=1000 cases
        [InlineData(1, 500, 300000, 0)]
        [InlineData(2, 500, 300000, 500)]
        [InlineData(3, 500, 300000, 1500)]
        [InlineData(4, 500, 300000, 3500)]
        [InlineData(5, 500, 300000, 7500)]
        [InlineData(6, 500, 300000, 15500)]
        [InlineData(999999, 500, 300000, 300000)]
        // base = 2000 — delay should be double of base=1000 cases
        [InlineData(1, 2000, 300000, 0)]
        [InlineData(2, 2000, 300000, 2000)]
        [InlineData(3, 2000, 300000, 6000)]
        [InlineData(4, 2000, 300000, 14000)]
        [InlineData(5, 2000, 300000, 30000)]
        [InlineData(6, 2000, 300000, 62000)]
        [InlineData(999999, 2000, 300000, 300000)]
        public void ExponentialBackoffDelay_Returns_Expected_Delay(int iteration, int baseDelayInMilliseconds, int maxDelayInMs, int expectedDelay)
        {
            var (computedDelay, _) = Compute.ExponentialBackoffDelay(iteration, baseDelayInMilliseconds, maxDelayInMs);
            Assert.Equal(expectedDelay, computedDelay);
        }

        [Fact]
        public void ExponentialBackoffDelay_Uses_Default_Base_Delay_Of_1000()
        {
            var (withDefault, _) = Compute.ExponentialBackoffDelay(iteration: 3);
            var (withExplicit, _) = Compute.ExponentialBackoffDelay(iteration: 3, baseDelayInMilliseconds: 1000);

            Assert.Equal(withExplicit, withDefault);
        }

        [Theory]
        [InlineData(1000)]
        [InlineData(500)]
        [InlineData(2000)]
        public void ExponentialBackoffDelay_Returns_Jitter_Within_Base_Delay(int baseDelayInMilliseconds)
        {
            var (_, jitter) = Compute.ExponentialBackoffDelay(5, baseDelayInMilliseconds, int.MaxValue);

            Assert.InRange(jitter, 0, baseDelayInMilliseconds - 1);
        }
    }
}
