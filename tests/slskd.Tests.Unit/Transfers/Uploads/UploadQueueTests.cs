namespace slskd.Tests.Unit.Transfers.Uploads
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Transfers;
    using slskd.Users;
    using Soulseek;
    using Xunit;
    using static slskd.Transfers.UploadQueue;

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

        public class Configuration
        {
            [Theory, AutoData]
            public void Reconfigures_Groups_When_Options_Change(string group, int priority, int slots, QueueStrategy strategy)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions()
                                    {
                                        Priority = priority,
                                        Slots = slots,
                                        Strategy = strategy.ToString(),
                                    }
                                }
                            },
                        }
                    }
                };

                // do not pass options; init with defaults
                var (queue, mocks) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

                // user defined group does not exist
                Assert.False(groups.ContainsKey(group));

                // reconfigure
                mocks.OptionsMonitor.RaiseOnChange(options);

                // get the new copy
                groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

                Assert.True(groups.ContainsKey(group));

                var p = groups[group];

                Assert.Equal(group, p.Name);
                Assert.Equal(priority, p.Priority);
                Assert.Equal(slots, p.Slots);
                Assert.Equal(0, p.UsedSlots);
                Assert.Equal(strategy, p.Strategy);
            }

            [Theory, AutoData]
            public void Retains_Used_Slot_Count_When_Options_Change(string group, int newPriority, int usedSlots)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions()
                                    {
                                        Priority = 0,
                                        Slots = 0,
                                        Strategy = QueueStrategy.FirstInFirstOut.ToString(),
                                    }
                                }
                            },
                        }
                    }
                };

                var (queue, mocks) = GetFixture(options);

                var groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");
                groups[group].UsedSlots = usedSlots;

                // reconfigure with different options to bypass the hash check
                options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions()
                                    {
                                        Priority = newPriority, // change priority
                                        Slots = 0,
                                        Strategy = QueueStrategy.FirstInFirstOut.ToString(),
                                    }
                                }
                            },
                        }
                    }
                };

                mocks.OptionsMonitor.RaiseOnChange(options);

                // get the new copy
                groups = queue.GetProperty<Dictionary<string, UploadQueue.Group>>("Groups");

                var p = groups[group];

                Assert.Equal(usedSlots, p.UsedSlots);
                Assert.Equal(newPriority, p.Priority);
            }
        }

        public class Enqueue
        {
            [Theory, AutoData]
            public void Enqueue_Enqueues_If_Nothing_Is_Enqueued_Already(string username, string filename)
            {
                var (queue, _) = GetFixture();
                var tx = GetTransfer(username, filename);

                Assert.Empty(queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("Uploads"));

                queue.Enqueue(tx);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("Uploads");

                Assert.Single(uploads);
                Assert.True(uploads.ContainsKey(username));
                Assert.Single(uploads.GetValueOrDefault(username));
                Assert.Equal(tx.Filename, uploads.GetValueOrDefault(username).First().Filename);
            }

            [Theory, AutoData]
            public void Enqueue_Enqueues_If_Something_Is_Enqueued_Already(string username, string filename, string filename2)
            {
                var (queue, _) = GetFixture();
                var tx = GetTransfer(username, filename);
                var tx2 = GetTransfer(username, filename2);
                
                queue.Enqueue(tx);
                queue.Enqueue(tx2);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("Uploads");

                Assert.Single(uploads);
                Assert.True(uploads.ContainsKey(username));
                Assert.Equal(2, uploads.GetValueOrDefault(username).Count);
                Assert.Equal(tx.Filename, uploads.GetValueOrDefault(username)[0].Filename);
                Assert.Equal(tx2.Filename, uploads.GetValueOrDefault(username)[1].Filename);
            }

            [Theory, AutoData]
            public void Enqueue_Enqueues_Transfers_From_Different_Users(string username, string filename, string username2, string filename2)
            {
                var (queue, _) = GetFixture();
                var tx = GetTransfer(username, filename);
                var tx2 = GetTransfer(username2, filename2);

                Assert.Empty(queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("Uploads"));

                queue.Enqueue(tx);
                queue.Enqueue(tx2);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("Uploads");

                Assert.Equal(2, uploads.Count);
                Assert.True(uploads.ContainsKey(username));
                Assert.True(uploads.ContainsKey(username2));

                // username should have a list containing 1 file
                Assert.Single(uploads.GetValueOrDefault(username));
                Assert.Equal(tx.Filename, uploads.GetValueOrDefault(username).First().Filename);

                // username2 should also have a list containing 1 file
                Assert.Single(uploads.GetValueOrDefault(username2));
                Assert.Equal(tx2.Filename, uploads.GetValueOrDefault(username2).First().Filename);
            }
        }

        private static (UploadQueue queue, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);
            var queue = new UploadQueue(
                mocks.UserService.Object,
                mocks.OptionsMonitor);

            return (queue, mocks);
        }

        private static Transfer GetTransfer(string username, string filename)
            => new Transfer(TransferDirection.Upload, username, filename, 0, TransferStates.Queued, 0, 0);

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
