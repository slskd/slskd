namespace slskd.Tests.Unit.Transfers.Uploads
{
    using System.Collections.Generic;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Transfers;
    using slskd.Users;
    using Xunit;

    public class GovernorTests
    {
        [Fact]
        public void Instantiates_With_BuiltIn_Buckets()
        {
            var (governor, _) = GetFixture();

            var buckets = governor.GetProperty<Dictionary<string, ITokenBucket>>("TokenBuckets");

            Assert.Equal(3, buckets.Count);
            Assert.True(buckets.ContainsKey(Application.PriviledgedGroup));
            Assert.True(buckets.ContainsKey(Application.DefaultGroup));
            Assert.True(buckets.ContainsKey(Application.LeecherGroup));
        }

        [Theory, AutoData]
        public void Instantiates_With_User_Defined_Buckets(string group1, string group2)
        {
            var options = new Options()
            {
                Groups = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        { group1, new Options.GroupsOptions.UserDefinedOptions() },
                        { group2, new Options.GroupsOptions.UserDefinedOptions() },
                    }
                }
            };

            var (governor, _) = GetFixture(options);

            var buckets = governor.GetProperty<Dictionary<string, ITokenBucket>>("TokenBuckets");

            Assert.Equal(5, buckets.Count);
            Assert.True(buckets.ContainsKey(group1));
            Assert.True(buckets.ContainsKey(group2));
        }

        public class Configuration
        {
            [Theory, AutoData]
            public void Reconfigures_Buckets_When_Options_Change(string group)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        { group, new Options.GroupsOptions.UserDefinedOptions() },
                    }
                    }
                };

                // do not pass options; only default buckets
                var (governor, mocks) = GetFixture();

                var buckets = governor.GetProperty<Dictionary<string, ITokenBucket>>("TokenBuckets");

                // ensure only default buckets are created
                Assert.Equal(3, buckets.Count);
                Assert.False(buckets.ContainsKey(group));

                // reconfigure with options
                mocks.OptionsMonitor.RaiseOnChange(options);

                // grab the new copy of buckets
                buckets = governor.GetProperty<Dictionary<string, ITokenBucket>>("TokenBuckets");

                Assert.Equal(4, buckets.Count);
                Assert.True(buckets.ContainsKey(group));
            }
        }

        private static (UploadGovernor governor, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);
            var governor = new UploadGovernor(
                mocks.UserService.Object,
                mocks.OptionsMonitor);

            return (governor, mocks);
        }

        private class Mocks
        {
            public Mocks(Options options = null)
            {
                OptionsMonitor = new TestOptionsMonitor<Options>(options ?? new Options());
            }

            public Mock<IUserService> UserService { get; } = new Mock<IUserService>();
            public TestOptionsMonitor<Options> OptionsMonitor { get; init; }
        }
    }
}
