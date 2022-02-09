namespace slskd.Tests.Unit.Common
{
    using System;
    using System.Threading.Tasks;
    using AutoFixture.Xunit2;
    using Xunit;

    public class TokenBucketTests
    {
        [Trait("Category", "Instantiation")]
        [Fact(DisplayName = "Throws ArgumentOutOfRangeException given 0 count")]
        public void Throws_ArgumentOutOfRangeException_Given_0_Count()
        {
            var ex = Record.Exception(() => new TokenBucket(0, 1000));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentOutOfRangeException>(ex);
            Assert.Equal("capacity", ((ArgumentOutOfRangeException)ex).ParamName);
        }

        [Trait("Category", "Instantiation")]
        [Fact(DisplayName = "Throws ArgumentOutOfRangeException given negative count")]
        public void Throws_ArgumentOutOfRangeException_Given_Negative_Count()
        {
            var ex = Record.Exception(() => new TokenBucket(-1, 1000));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentOutOfRangeException>(ex);
            Assert.Equal("capacity", ((ArgumentOutOfRangeException)ex).ParamName);
        }

        [Trait("Category", "Instantiation")]
        [Fact(DisplayName = "Throws ArgumentOutOfRangeException given 0 interval")]
        public void Throws_ArgumentOutOfRangeException_Given_0_Interval()
        {
            var ex = Record.Exception(() => new TokenBucket(1000, 0));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentOutOfRangeException>(ex);
            Assert.Equal("interval", ((ArgumentOutOfRangeException)ex).ParamName);
        }

        [Trait("Category", "Instantiation")]
        [Fact(DisplayName = "Throws ArgumentOutOfRangeException given negative interval")]
        public void Throws_ArgumentOutOfRangeException_Given_Negative_Interval()
        {
            var ex = Record.Exception(() => new TokenBucket(1000, -1));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentOutOfRangeException>(ex);
            Assert.Equal("interval", ((ArgumentOutOfRangeException)ex).ParamName);
        }

        [Trait("Category", "Instantiation")]
        [Theory(DisplayName = "Sets properties"), AutoData]
        public void Sets_Properties(int count, int interval)
        {
            using (var t = new TokenBucket(count, interval))
            {
                Assert.Equal(count, t.Capacity);
                Assert.Equal(interval, t.GetProperty<System.Timers.Timer>("Clock").Interval);
                Assert.Equal(count, t.GetProperty<long>("CurrentCount"));
            }
        }

        [Trait("Category", "SetCount")]
        [Fact(DisplayName = "SetCount throws ArgumentOutOfRangeException given 0 count")]
        public void SetCount_Throws_ArgumentOutOfRangeException_Given_0_Count()
        {
            using (var t = new TokenBucket(10, 1000))
            {
                var ex = Record.Exception(() => t.SetCapacity(0));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentOutOfRangeException>(ex);
                Assert.Equal("capacity", ((ArgumentOutOfRangeException)ex).ParamName);
            }
        }

        [Trait("Category", "SetCount")]
        [Fact(DisplayName = "SetCount throws ArgumentOutOfRangeException given negative count")]
        public void SetCount_Throws_ArgumentOutOfRangeException_Given_Negative_Count()
        {
            using (var t = new TokenBucket(10, 1000))
            {
                var ex = Record.Exception(() => t.SetCapacity(-1));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentOutOfRangeException>(ex);
                Assert.Equal("capacity", ((ArgumentOutOfRangeException)ex).ParamName);
            }
        }

        [Trait("Category", "SetCapacity")]
        [Theory(DisplayName = "SetCapacity sets capacity"), AutoData]
        public void SetCapacity_Sets_Capacity(int count)
        {
            using (var t = new TokenBucket(10, 1000))
            {
                t.SetCapacity(count);

                Assert.Equal(count, t.Capacity);
            }
        }

        [Trait("Category", "GetAsync")]
        [Fact(DisplayName = "GetAsync decrements count by requested count")]
        public async Task GetAsync_Decrements_Count_By_Requested_Count()
        {
            using (var t = new TokenBucket(10, 10000))
            {
                await t.GetAsync(5);

                Assert.Equal(5, t.GetProperty<long>("CurrentCount"));
            }
        }

        [Trait("Category", "GetAsync")]
        [Fact(DisplayName = "GetAsync returns capacity if request exceeds capacity")]
        public async Task GetAsync_Returns_Capacity_If_Request_Exceeds_Capacity()
        {
            using (var t = new TokenBucket(10, 10000))
            {
                int tokens = 0;
                var ex = await Record.ExceptionAsync(async () => tokens = await t.GetAsync(11));

                Assert.Null(ex);
                Assert.Equal(10, tokens);
            }
        }

        [Trait("Category", "GetAsync")]
        [Fact(DisplayName = "GetAsync returns available tokens if request exceeds available count")]
        public async Task GetAsync_Returns_Available_Tokens_If_Request_Exceeds_Available_Count()
        {
            using (var t = new TokenBucket(10, 10000))
            {
                await t.GetAsync(6);
                var count = await t.GetAsync(6);

                Assert.Equal(4, count);
            }
        }

        [Trait("Category", "GetAsync")]
        [Fact(DisplayName = "GetAsync waits for reset if bucket is depleted")]
        public async Task GetAsync_Waits_For_Reset_If_Bucket_Is_Depleted()
        {
            using (var t = new TokenBucket(1, 10))
            {
                await t.GetAsync(1);
                await t.GetAsync(1);
                await t.GetAsync(1);

                Assert.True(true);
            }
        }

        [Trait("Category", "Return")]
        [Fact(DisplayName = "Return does not change count given negative")]
        public async Task Return_Does_Not_Change_Count_Given_Negative()
        {
            using (var t = new TokenBucket(10, 1000000))
            {
                await t.GetAsync(5);

                Assert.Equal(5, t.GetProperty<long>("CurrentCount"));

                t.Return(-5);

                Assert.Equal(5, t.GetProperty<long>("CurrentCount"));
            }
        }

        [Trait("Category", "Return")]
        [Fact(DisplayName = "Return adds capacity given value larger than capacity")]
        public async Task Return_Adds_Capacity_Given_Value_Larger_Than_Capacity()
        {
            using (var t = new TokenBucket(10, 1000000))
            {
                await t.GetAsync(5);

                Assert.Equal(5, t.GetProperty<long>("CurrentCount"));

                t.Return(50);

                Assert.Equal(15, t.GetProperty<long>("CurrentCount"));
            }
        }

        [Trait("Category", "Return")]
        [Fact(DisplayName = "Return adds given value")]
        public async Task Return_Adds_Given_Value()
        {
            using (var t = new TokenBucket(10, 1000000))
            {
                await t.GetAsync(5);

                Assert.Equal(5, t.GetProperty<long>("CurrentCount"));

                t.Return(5);

                Assert.Equal(10, t.GetProperty<long>("CurrentCount"));
            }
        }
    }
}
