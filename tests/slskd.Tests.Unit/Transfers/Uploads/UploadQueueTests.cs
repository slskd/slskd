namespace slskd.Tests.Unit.Transfers.Uploads
{
    using System.Collections.Generic;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Transfers;
    using slskd.Users;
    using Xunit;

    public class UploadQueueTests
    {
        [Fact]
        public void Instantiates_With_BuiltIn_Groups()
        {
            var (queue, _) = GetFixture();

            var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

            Assert.Equal(3, groups.Count);
            Assert.True(groups.ContainsKey(Application.PriviledgedGroup));
            Assert.True(groups.ContainsKey(Application.DefaultGroup));
            Assert.True(groups.ContainsKey(Application.LeecherGroup));
        }

        [Fact]
        public void Instantiates_With_Expected_Privileged_Options()
        {
            var (queue, _) = GetFixture();

            var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

            var p = groups[Application.PriviledgedGroup];

            Assert.Equal(Application.PriviledgedGroup, p.Name);
            Assert.Equal(0, p.Priority);
            Assert.Equal(new Options().Global.Upload.Slots, p.Slots);
            Assert.Equal(0, p.UsedSlots);
            Assert.Equal(QueueStrategy.FirstInFirstOut, p.Strategy);
        }

        [Theory, AutoData]
        public void Instantiates_With_Expected_Default_Group_Options(int priority, int slots, QueueStrategy strategy)
        {
            var (queue, _) = GetFixture(new Options()
            {
                Groups = new Options.GroupsOptions()
                {
                    Default = new Options.GroupsOptions.BuiltInOptions()
                    {
                        Upload = new Options.GroupsOptions.UploadOptions()
                        {
                            Priority = priority,
                            Slots = slots,
                            Strategy = strategy.ToString(),
                        }
                    }
                }
            });

            var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

            var p = groups[Application.DefaultGroup];

            Assert.Equal(Application.DefaultGroup, p.Name);
            Assert.Equal(priority, p.Priority);
            Assert.Equal(slots, p.Slots);
            Assert.Equal(0, p.UsedSlots);
            Assert.Equal(strategy, p.Strategy);
        }

        [Theory, AutoData]
        public void Instantiates_With_Expected_Leecher_Group_Options(int priority, int slots, QueueStrategy strategy)
        {
            var (queue, _) = GetFixture(new Options()
            {
                Groups = new Options.GroupsOptions()
                {
                    Leechers = new Options.GroupsOptions.BuiltInOptions()
                    {
                        Upload = new Options.GroupsOptions.UploadOptions()
                        {
                            Priority = priority,
                            Slots = slots,
                            Strategy = strategy.ToString(),
                        }
                    }
                }
            });

            var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

            var p = groups[Application.LeecherGroup];

            Assert.Equal(Application.LeecherGroup, p.Name);
            Assert.Equal(priority, p.Priority);
            Assert.Equal(slots, p.Slots);
            Assert.Equal(0, p.UsedSlots);
            Assert.Equal(strategy, p.Strategy);
        }

        [Theory, AutoData]
        public void Instantiates_With_Expected_User_Defined_Group_Options(string group1, int priority1, int slots1, QueueStrategy strategy1, string group2, int priority2, int slots2, QueueStrategy strategy2)
        {
            var (queue, _) = GetFixture(new Options()
            {
                Groups = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            group1,
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Upload = new Options.GroupsOptions.UploadOptions()
                                {
                                    Priority = priority1,
                                    Slots = slots1,
                                    Strategy = strategy1.ToString(),
                                }
                            }
                        },
                        {
                            group2,
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Upload = new Options.GroupsOptions.UploadOptions()
                                {
                                    Priority = priority2,
                                    Slots = slots2,
                                    Strategy = strategy2.ToString(),
                                }
                            }
                        }
                    }
                }
            });

            var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

            var p = groups[group1];

            Assert.Equal(group1, p.Name);
            Assert.Equal(priority1, p.Priority);
            Assert.Equal(slots1, p.Slots);
            Assert.Equal(0, p.UsedSlots);
            Assert.Equal(strategy1, p.Strategy);

            p = groups[group2];

            Assert.Equal(group2, p.Name);
            Assert.Equal(priority2, p.Priority);
            Assert.Equal(slots2, p.Slots);
            Assert.Equal(0, p.UsedSlots);
            Assert.Equal(strategy2, p.Strategy);
        }

        private static (UploadQueue queue, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);
            var queue = new UploadQueue(
                mocks.UserService.Object,
                mocks.OptionsMonitor);

            return (queue, mocks);
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
