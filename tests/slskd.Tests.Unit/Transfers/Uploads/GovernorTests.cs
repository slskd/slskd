namespace slskd.Tests.Unit.Transfers.Uploads
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Transfers;
    using slskd.Users;
    using Soulseek;
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

        public class GetBytesAsync
        {
            [Theory, AutoData]
            public async Task Gets_Bytes_From_Bucket(string username, string filename)
            {
                var (governor, _) = GetFixture();

                // mock the default bucket
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                {
                    { Application.DefaultGroup, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                await governor.GetBytesAsync(tx, 1, CancellationToken.None);

                bucket.Verify(m => m.GetAsync(1, CancellationToken.None), Times.Once);
            }

            [Theory, AutoData]
            public async Task Gets_Bytes_From_Default_Bucket_If_No_Bucket_For_Group_Exists(string username, string filename, string group)
            {
                var (governor, mocks) = GetFixture();

                // return a bogus group for this user, specifically one that does not have
                // a corresponding bucket
                mocks.UserService.Setup(m => m.GetGroup(username)).Returns(group);

                // mock the default bucket
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                {
                    { Application.DefaultGroup, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                await governor.GetBytesAsync(tx, 1, CancellationToken.None);

                bucket.Verify(m => m.GetAsync(1, CancellationToken.None), Times.Once);
            }

            [Theory, AutoData]
            public async Task Gets_Bytes_From_User_Defined_Bucket(string username, string filename, string group)
            {
                var (governor, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(username)).Returns(group);

                // mock a bucket for the user's group
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                {
                    { Application.DefaultGroup, new Mock<ITokenBucket>().Object },
                    { group, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                await governor.GetBytesAsync(tx, 1, CancellationToken.None);

                bucket.Verify(m => m.GetAsync(1, CancellationToken.None), Times.Once);
            }
        }

        public class ReturnBytes
        {
            [Theory, AutoData]
            public void Returns_Bytes_To_Bucket(string username, string filename, int attemptedBytes)
            {
                var (governor, _) = GetFixture();

                // mock the default bucket
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                { 
                    { Application.DefaultGroup, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                governor.ReturnBytes(transfer: tx, attemptedBytes, grantedBytes: attemptedBytes / 2, actualBytes: attemptedBytes / 4);

                // assert that the difference between granted and actual was returned
                bucket.Verify(m => m.Return((attemptedBytes / 2) - (attemptedBytes / 4)), Times.Once);
            }

            [Theory, AutoData]
            public void Does_Not_Return_Bytes_To_Bucket_If_No_Bytes_Were_Wasted(string username, string filename)
            {
                var (governor, _) = GetFixture();

                // mock the default bucket
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                {
                    { Application.DefaultGroup, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                governor.ReturnBytes(transfer: tx, attemptedBytes: 100, grantedBytes: 50, actualBytes: 50);

                // assert that the bucket's return method was never called
                bucket.Verify(m => m.Return(It.IsAny<int>()), Times.Never);
            }

            [Theory, AutoData]
            public void Returns_Bytes_To_Default_Bucket_If_No_Bucket_For_Group_Exists(string username, string filename, string group)
            {
                var (governor, mocks) = GetFixture();

                // return a bogus group for this user, specifically one that does not have
                // a corresponding bucket
                mocks.UserService.Setup(m => m.GetGroup(username)).Returns(group);

                // mock only the default bucket
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                {
                    { Application.DefaultGroup, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                governor.ReturnBytes(transfer: tx, attemptedBytes: 100, grantedBytes: 50, actualBytes: 25);

                // assert that the difference between granted and actual was returned
                bucket.Verify(m => m.Return(25));
            }

            [Theory, AutoData]
            public void Returns_Bytes_To_User_Defined_Bucket(string username, string filename, string group)
            {
                var (governor, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(username)).Returns(group);

                // mock a bucket for the user's group
                var bucket = new Mock<ITokenBucket>();
                governor.SetProperty("TokenBuckets", new Dictionary<string, ITokenBucket>()
                {
                    { Application.DefaultGroup, new Mock<ITokenBucket>().Object },
                    { group, bucket.Object },
                });

                var tx = new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Completed, 0, 0);

                governor.ReturnBytes(transfer: tx, attemptedBytes: 100, grantedBytes: 50, actualBytes: 25);

                // assert that the difference between granted and actual was returned
                bucket.Verify(m => m.Return(25));
            }
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
