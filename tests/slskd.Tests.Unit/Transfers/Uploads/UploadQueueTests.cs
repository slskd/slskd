namespace slskd.Tests.Unit.Transfers.Uploads
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Transfers;
    using slskd.Users;
    using System;
    using Xunit;
    using slskd.Transfers.Uploads;

    public class UploadQueueTests
    {
        [Fact]
        public void Instantiates_With_BuiltIn_Groups()
        {
            var (queue, _) = GetFixture();

            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

            Assert.Equal(3, groups.Count);
            Assert.True(groups.ContainsKey(Application.PrivilegedGroup));
            Assert.True(groups.ContainsKey(Application.DefaultGroup));
            Assert.True(groups.ContainsKey(Application.LeecherGroup));
        }

        [Fact]
        public void Instantiates_With_Expected_Privileged_Options()
        {
            var (queue, _) = GetFixture();

            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

            var p = groups[Application.PrivilegedGroup];

            Assert.Equal(Application.PrivilegedGroup, p.Name);
            Assert.Equal(0, p.Priority);
            Assert.Equal(new Options().Transfers.Upload.Slots, p.Slots);
            Assert.Empty(p.UsedSlots);
            Assert.Equal(QueueStrategy.FirstInFirstOut, p.Strategy);
        }

        [Theory, AutoData]
        public void Instantiates_With_Expected_Default_Group_Options(int priority, int slots, QueueStrategy strategy)
        {
            var (queue, _) = GetFixture(new Options()
            {
                Transfers = new Options.TransfersOptions
                {
                    Upload = new Options.TransfersOptions.GlobalUploadOptions
                    {
                        Slots = int.MaxValue,
                    },
                    Groups = new Options.TransfersOptions.GroupsOptions()
                    {
                        Default = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions()
                        {
                            Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                            {
                                Priority = priority,
                                Slots = slots,
                                Strategy = strategy.ToString(),
                            }
                        }
                    }
                },
            });

            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

            var p = groups[Application.DefaultGroup];

            Assert.Equal(Application.DefaultGroup, p.Name);
            Assert.Equal(priority, p.Priority);
            Assert.Equal(slots, p.Slots);
            Assert.Empty(p.UsedSlots);
            Assert.Equal(strategy, p.Strategy);
        }

        [Theory, AutoData]
        public void Instantiates_With_Expected_Leecher_Group_Options(int priority, int slots, QueueStrategy strategy)
        {
            var (queue, _) = GetFixture(new Options()
            {
                Transfers = new Options.TransfersOptions
                {
                    Upload = new Options.TransfersOptions.GlobalUploadOptions
                    {
                        Slots = int.MaxValue,
                    },
                    Groups = new Options.TransfersOptions.GroupsOptions()
                    {
                        Leechers = new Options.TransfersOptions.GroupsOptions.LeecherOptions()
                        {
                            Thresholds = new Options.TransfersOptions.GroupsOptions.LeecherOptions.ThresholdOptions(),
                            Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                            {
                                Priority = priority,
                                Slots = slots,
                                Strategy = strategy.ToString(),
                            }
                        }
                    }
                }
            });

            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

            var p = groups[Application.LeecherGroup];

            Assert.Equal(Application.LeecherGroup, p.Name);
            Assert.Equal(priority, p.Priority);
            Assert.Equal(slots, p.Slots);
            Assert.Empty(p.UsedSlots);
            Assert.Equal(strategy, p.Strategy);
        }

        [Theory, AutoData]
        public void Instantiates_With_Expected_User_Defined_Group_Options(string group1, int priority1, int slots1, QueueStrategy strategy1, string group2, int priority2, int slots2, QueueStrategy strategy2)
        {
            var (queue, _) = GetFixture(new Options()
            {
                Transfers = new Options.TransfersOptions
                {
                    Upload = new Options.TransfersOptions.GlobalUploadOptions
                    {
                        Slots = int.MaxValue,
                    },
                    Groups = new Options.TransfersOptions.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.TransfersOptions.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group1,
                                new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                                    {
                                        Priority = priority1,
                                        Slots = slots1,
                                        Strategy = strategy1.ToString(),
                                    }
                                }
                            },
                            {
                                group2,
                                new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                                    {
                                        Priority = priority2,
                                        Slots = slots2,
                                        Strategy = strategy2.ToString(),
                                    }
                                }
                            }
                        }
                    }
                }
            });

            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

            var p = groups[group1];

            Assert.Equal(group1, p.Name);
            Assert.Equal(priority1, p.Priority);
            Assert.Equal(slots1, p.Slots);
            Assert.Empty(p.UsedSlots);
            Assert.Equal(strategy1, p.Strategy);

            p = groups[group2];

            Assert.Equal(group2, p.Name);
            Assert.Equal(priority2, p.Priority);
            Assert.Equal(slots2, p.Slots);
            Assert.Empty(p.UsedSlots);
            Assert.Equal(strategy2, p.Strategy);
        }

        public class Configuration
        {
            [Theory, AutoData]
            public void Reconfigures_Groups_When_Options_Change(string group, int priority, int slots, QueueStrategy strategy)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Upload = new Options.TransfersOptions.GlobalUploadOptions
                        {
                            Slots = int.MaxValue,
                        },
                        Groups = new Options.TransfersOptions.GroupsOptions()
                        {
                            UserDefined = new Dictionary<string, Options.TransfersOptions.GroupsOptions.UserDefinedOptions>()
                            {
                                {
                                    group,
                                    new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                    {
                                        Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                                        {
                                            Priority = priority,
                                            Slots = slots,
                                            Strategy = strategy.ToString(),
                                        }
                                    }
                                },
                            }
                        }
                    }
                };

                // do not pass options; init with defaults
                var (queue, mocks) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                // user defined group does not exist
                Assert.False(groups.ContainsKey(group));

                // reconfigure
                mocks.OptionsMonitor.RaiseOnChange(options);

                // get the new copy
                groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.True(groups.ContainsKey(group));

                var p = groups[group];

                Assert.Equal(group, p.Name);
                Assert.Equal(priority, p.Priority);
                Assert.Equal(slots, p.Slots);
                Assert.Empty(p.UsedSlots);
                Assert.Equal(strategy, p.Strategy);
            }

            [Theory, AutoData]
            public void Limits_Group_Slots_To_Global_Slot_Count(string group, int priority, QueueStrategy strategy)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Upload = new Options.TransfersOptions.GlobalUploadOptions
                        {
                            Slots = 42,
                        },
                        Groups = new Options.TransfersOptions.GroupsOptions()
                        {
                            UserDefined = new Dictionary<string, Options.TransfersOptions.GroupsOptions.UserDefinedOptions>()
                            {
                                {
                                    group,
                                    new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                    {
                                        Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                                        {
                                            Priority = priority,
                                            Slots = int.MaxValue, // lots
                                            Strategy = strategy.ToString(),
                                        }
                                    }
                                },
                            }
                        }
                    }
                };

                // do not pass options; init with defaults
                var (queue, mocks) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                // user defined group does not exist
                Assert.False(groups.ContainsKey(group));

                // reconfigure
                mocks.OptionsMonitor.RaiseOnChange(options);

                // get the new copy
                groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.True(groups.ContainsKey(group));

                var p = groups[group];

                Assert.Equal(group, p.Name);
                Assert.Equal(priority, p.Priority);
                Assert.Equal(42, p.Slots); // clamped to global value
                Assert.Empty(p.UsedSlots);
                Assert.Equal(strategy, p.Strategy);
            }

            [Theory, AutoData]
            public void Retains_Used_Slots_When_Options_Change(string group, int newPriority, string user1, string file1, string user2, string file2)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions()
                        {
                            UserDefined = new Dictionary<string, Options.TransfersOptions.GroupsOptions.UserDefinedOptions>()
                            {
                                {
                                    group,
                                    new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                    {
                                        Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                                        {
                                            Priority = 0,
                                            Slots = 0,
                                            Strategy = QueueStrategy.FirstInFirstOut.ToString(),
                                        }
                                    }
                                },
                            }
                        }
                    }
                };

                var (queue, mocks) = GetFixture(options);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[group].UsedSlots.Add((user1, file1));
                groups[group].UsedSlots.Add((user2, file2));

                // reconfigure with different options to bypass the hash check
                options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions()
                        {
                            UserDefined = new Dictionary<string, Options.TransfersOptions.GroupsOptions.UserDefinedOptions>()
                            {
                                {
                                    group,
                                    new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                    {
                                        Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions()
                                        {
                                            Priority = newPriority, // change priority
                                            Slots = 0,
                                            Strategy = QueueStrategy.FirstInFirstOut.ToString(),
                                        }
                                    }
                                },
                            }
                        }
                    }
                };

                mocks.OptionsMonitor.RaiseOnChange(options);

                // get the new copy
                groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                var p = groups[group];

                Assert.Equal(2, p.UsedSlots.Count);
                Assert.Contains((user1, file1), p.UsedSlots);
                Assert.Contains((user2, file2), p.UsedSlots);
                Assert.Equal(newPriority, p.Priority);
            }
        }

        public class Enqueue
        {
            [Theory, AutoData]
            public void Enqueue_Enqueues_If_Nothing_Is_Enqueued_Already(string username, string filename)
            {
                var (queue, _) = GetFixture();

                Assert.Empty(queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary"));

                queue.Enqueue(username, filename);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                Assert.Single(uploads);
                Assert.True(uploads.ContainsKey(username));
                Assert.Single(uploads.GetValueOrDefault(username));
                Assert.Equal(filename, uploads.GetValueOrDefault(username).First().Filename);
            }

            [Theory, AutoData]
            public void Enqueue_Enqueues_If_Something_Is_Enqueued_Already(string username, string filename, string filename2)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);
                queue.Enqueue(username, filename2);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                Assert.Single(uploads);
                Assert.True(uploads.ContainsKey(username));
                Assert.Equal(2, uploads.GetValueOrDefault(username).Count);
                Assert.Equal(filename, uploads.GetValueOrDefault(username)[0].Filename);
                Assert.Equal(filename2, uploads.GetValueOrDefault(username)[1].Filename);
            }

            [Theory, AutoData]
            public void Enqueue_Enqueues_Transfers_From_Different_Users(string username, string filename, string username2, string filename2)
            {
                var (queue, _) = GetFixture();

                Assert.Empty(queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary"));

                queue.Enqueue(username, filename);
                queue.Enqueue(username2, filename2);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                Assert.Equal(2, uploads.Count);
                Assert.True(uploads.ContainsKey(username));
                Assert.True(uploads.ContainsKey(username2));

                // username should have a list containing 1 file
                Assert.Single(uploads.GetValueOrDefault(username));
                Assert.Equal(filename, uploads.GetValueOrDefault(username).First().Filename);

                // username2 should also have a list containing 1 file
                Assert.Single(uploads.GetValueOrDefault(username2));
                Assert.Equal(filename2, uploads.GetValueOrDefault(username2).First().Filename);
            }
        }

        public class Complete
        {
            [Theory, AutoData]
            public void Throws_If_No_Such_Username(string username, string filename)
            {
                var (queue, _) = GetFixture();

                var ex = Record.Exception(() => queue.Complete(username, filename));

                Assert.NotNull(ex);
                Assert.IsType<SlskdException>(ex);
                Assert.True(ex.Message.Contains("no enqueued uploads for user", System.StringComparison.InvariantCultureIgnoreCase));
            }

            [Theory, AutoData]
            public void Throws_If_No_Such_Filename(string username, string filename)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);

                var ex = Record.Exception(() => queue.Complete(username, "foo"));

                Assert.NotNull(ex);
                Assert.IsType<SlskdException>(ex);
                Assert.True(ex.Message.Contains("is not enqueued for user", System.StringComparison.InvariantCultureIgnoreCase));
            }

            [Theory, AutoData]
            public async Task Removes_Filename(string username, string filename, string filename2)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);
                await queue.AwaitStartAsync(username, filename);
                queue.Enqueue(username, filename2);
                await queue.AwaitStartAsync(username, filename2);

                queue.Complete(username, filename);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                Assert.Single(uploads);
                Assert.True(uploads.ContainsKey(username));
                Assert.Single(uploads[username]);
                Assert.Equal(filename2, uploads[username][0].Filename);
            }

            [Theory, AutoData]
            public async Task Releases_Slot_Held_By_Completed_Upload(string username, string filename, string filename2)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);
                await queue.AwaitStartAsync(username, filename);

                queue.Enqueue(username, filename2);
                await queue.AwaitStartAsync(username, filename2);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Equal(2, groups[Application.DefaultGroup].UsedSlots.Count);
                Assert.Contains((username, filename), groups[Application.DefaultGroup].UsedSlots);
                Assert.Contains((username, filename2), groups[Application.DefaultGroup].UsedSlots);

                queue.Complete(username, filename);

                groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Single(groups[Application.DefaultGroup].UsedSlots);
                Assert.Contains((username, filename2), groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public async Task Releases_Slot_To_Pinned_Group_If_Users_Group_Changed(string username, string filename)
            {
                var (queue, mocks) = GetFixture();

                queue.Enqueue(username, filename);
                await queue.AwaitStartAsync(username, filename);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Contains((username, filename), groups[Application.DefaultGroup].UsedSlots);

                // the user moves to a different group mid-transfer
                mocks.UserService.Setup(m => m.GetGroup(username)).Returns(Application.LeecherGroup);

                queue.Complete(username, filename);

                Assert.Empty(groups[Application.DefaultGroup].UsedSlots);
                Assert.Empty(groups[Application.LeecherGroup].UsedSlots);
            }

            [Theory, AutoData]
            public async Task Does_Not_Release_Slots_Held_By_Other_Uploads_When_Completing_Upload_That_Never_Started(string username, string filename, string other, string otherFilename)
            {
                var (queue, _) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Slots = 1;

                queue.Enqueue(other, otherFilename);
                await queue.AwaitStartAsync(other, otherFilename);

                // no slot available; this upload is queued but never started
                queue.Enqueue(username, filename);
                var task = queue.AwaitStartAsync(username, filename);

                Assert.False(task.IsCompleted);

                queue.Complete(username, filename);

                Assert.Single(groups[Application.DefaultGroup].UsedSlots);
                Assert.Contains((other, otherFilename), groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public async Task Releasing_A_Slot_Starts_The_Next_Ready_Upload(string username, string filename, string other, string otherFilename)
            {
                var (queue, _) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Slots = 1;

                queue.Enqueue(other, otherFilename);
                await queue.AwaitStartAsync(other, otherFilename);

                queue.Enqueue(username, filename);
                var task = queue.AwaitStartAsync(username, filename);

                Assert.False(task.IsCompleted);

                queue.Complete(other, otherFilename);

                Assert.True(task.IsCompletedSuccessfully);
                Assert.Single(groups[Application.DefaultGroup].UsedSlots);
                Assert.Contains((username, filename), groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public void Cleans_Up_If_User_Has_No_More_Files_Enqueued(string username, string filename, string filename2)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);
                queue.Enqueue(username, filename2);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                Assert.Single(uploads);

                queue.Complete(username, filename);
                queue.Complete(username, filename2);

                uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                Assert.Empty(uploads);
            }
        }

        public class AwaitStartAsync
        {
            [Theory, AutoData]
            public async Task Throws_If_No_Such_Username(string username, string filename)
            {
                var (queue, _) = GetFixture();

                var ex = await Record.ExceptionAsync(() => queue.AwaitStartAsync(username, filename));

                Assert.NotNull(ex);
                Assert.IsType<SlskdException>(ex);
                Assert.True(ex.Message.Contains("no enqueued uploads for user", System.StringComparison.InvariantCultureIgnoreCase));
            }

            [Theory, AutoData]
            public async Task Throws_If_No_Such_Filename(string username, string filename)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);

                var ex = await Record.ExceptionAsync(() => queue.AwaitStartAsync(username, "foo"));

                Assert.NotNull(ex);
                Assert.IsType<SlskdException>(ex);
                Assert.True(ex.Message.Contains("is not enqueued for user", System.StringComparison.InvariantCultureIgnoreCase));
            }

            [Theory, AutoData]
            public void Returns_Task_Associated_With_Upload(string username, string filename)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);

                var uploads = queue.GetProperty<ConcurrentDictionary<string, List<Upload>>>("UploadDictionary");

                var task = queue.AwaitStartAsync(username, filename);

                Assert.Equal(task, uploads[username][0].TaskCompletionSource.Task);
            }

            [Theory, AutoData]
            public void Occupies_Slot_When_Upload_Is_Released(string username, string filename)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                // enqueueing alone does not consume a slot; the upload must be ready first
                Assert.Empty(groups[Application.DefaultGroup].UsedSlots);

                var task = queue.AwaitStartAsync(username, filename);

                Assert.True(task.IsCompletedSuccessfully);
                Assert.Single(groups[Application.DefaultGroup].UsedSlots);
                Assert.Contains((username, filename), groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public void Does_Not_Occupy_Slot_When_No_Slot_Is_Available(string username, string filename, string other, string otherFilename)
            {
                var (queue, _) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Slots = 1;
                groups[Application.DefaultGroup].UsedSlots.Add((other, otherFilename));

                queue.Enqueue(username, filename);
                var task = queue.AwaitStartAsync(username, filename);

                Assert.False(task.IsCompleted);
                Assert.Single(groups[Application.DefaultGroup].UsedSlots);
                Assert.DoesNotContain((username, filename), groups[Application.DefaultGroup].UsedSlots);
            }
        }

        public class EstimatePosition
        {
            private static readonly DateTime Now = DateTime.UtcNow;

            [Theory]
            [InlineAutoData(QueueStrategy.RoundRobin)]
            [InlineAutoData(QueueStrategy.FirstInFirstOut)]
            public void Throws_NotFoundException_If_User_Has_No_Uploads(QueueStrategy strategy, string username, string filename)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, strategy);

                var ex = Record.Exception(() => queue.EstimatePosition(username, filename));

                Assert.IsType<NotFoundException>(ex);
            }

            [Theory]
            [InlineAutoData(QueueStrategy.RoundRobin)]
            [InlineAutoData(QueueStrategy.FirstInFirstOut)]
            public void Throws_NotFoundException_If_File_Is_Not_Enqueued(QueueStrategy strategy, string username, string filename, string otherFilename)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, strategy);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();

                uploads.TryAdd(username, new List<Upload>()
                {
                    new Upload() { Username = username, Filename = otherFilename, Enqueued = Now },
                });

                queue.SetProperty("UploadDictionary", uploads);

                var ex = Record.Exception(() => queue.EstimatePosition(username, filename));

                Assert.IsType<NotFoundException>(ex);
            }

            [Theory, AutoData]
            public void RoundRobin_Returns_Local_Position_If_User_Is_Alone_In_Group(string username)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(username, CreateUploads(username, count: 3));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(0, queue.EstimatePosition(username, "file0"));
                Assert.Equal(1, queue.EstimatePosition(username, "file1"));
                Assert.Equal(2, queue.EstimatePosition(username, "file2"));
            }

            [Theory, AutoData]
            public void RoundRobin_First_File_Is_Behind_Every_Other_User_With_Queued_Files(string username, string other1, string other2)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other1, CreateUploads(other1, count: 10, offset: -100));
                uploads.TryAdd(other2, CreateUploads(other2, count: 1, offset: -100));
                uploads.TryAdd(username, CreateUploads(username, count: 3));

                queue.SetProperty("UploadDictionary", uploads);

                // pessimistic; both other users have a file in round 0, and are assumed to go first
                Assert.Equal(2, queue.EstimatePosition(username, "file0"));
            }

            [Theory, AutoData]
            public void RoundRobin_Adds_Lesser_Of_Local_Position_And_Upload_Count_For_Each_Other_User(string a, string b, string c, string d)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);

                // aaaaa
                // bb
                // cccccccccccc
                // ddddddd
                //     ^ (local position 4)
                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(a, CreateUploads(a, count: 5));
                uploads.TryAdd(b, CreateUploads(b, count: 2));
                uploads.TryAdd(c, CreateUploads(c, count: 12));
                uploads.TryAdd(d, CreateUploads(d, count: 7));

                queue.SetProperty("UploadDictionary", uploads);

                // 4 (local) + min(4, 5) + min(4, 2) + min(4, 12), + 1 each for a and c, which have a file in round 4
                Assert.Equal(4 + 4 + 2 + 4 + 2, queue.EstimatePosition(d, "file4"));
            }

            [Theory]
            [InlineAutoData(1, 3)]
            [InlineAutoData(2, 4)]
            [InlineAutoData(3, 5)]
            [InlineAutoData(10, 5)]
            public void RoundRobin_Counts_Other_User_As_Ahead_Only_If_They_Have_A_File_In_The_Same_Round(int otherCount, int expected, string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);

                // the requested file is at local position 2 (round 2). the other user contends for round 2 only if they
                // have more than 2 files: 2 (local) + min(2, otherCount) + (otherCount > 2 ? 1 : 0)
                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(username, CreateUploads(username, count: 3));
                uploads.TryAdd(other, CreateUploads(other, count: otherCount));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(expected, queue.EstimatePosition(username, "file2"));
            }

            [Theory, AutoData]
            public void RoundRobin_Ignores_Users_In_Other_Groups(string username, string other)
            {
                var (queue, mocks) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);

                mocks.UserService.Setup(m => m.GetGroup(other)).Returns(Application.PrivilegedGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other, CreateUploads(other, count: 10));
                uploads.TryAdd(username, CreateUploads(username, count: 3));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(2, queue.EstimatePosition(username, "file2"));
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Returns_Zero_If_Upload_Is_Earliest_In_Group(string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(username, CreateUploads(username, count: 1));
                uploads.TryAdd(other, CreateUploads(other, count: 5, offset: 10));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(0, queue.EstimatePosition(username, "file0"));
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Counts_All_Uploads_In_Group_Enqueued_Earlier(string a, string b, string c)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);

                // enqueued at:
                // a: 0, 3, 6
                // b: 1, 4, 7
                // c: 2, 5, 8
                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(a, CreateUploads(a, count: 3, offset: 0, step: 3));
                uploads.TryAdd(b, CreateUploads(b, count: 3, offset: 1, step: 3));
                uploads.TryAdd(c, CreateUploads(c, count: 3, offset: 2, step: 3));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(0, queue.EstimatePosition(a, "file0"));
                Assert.Equal(4, queue.EstimatePosition(b, "file1"));
                Assert.Equal(8, queue.EstimatePosition(c, "file2"));
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Counts_Uploads_In_Progress(string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);

                var started = CreateUploads(other, count: 2);
                started.ForEach(u => u.Started = Now);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other, started);
                uploads.TryAdd(username, CreateUploads(username, count: 1, offset: 10));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(2, queue.EstimatePosition(username, "file0"));
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Ignores_Users_In_Other_Groups(string username, string other)
            {
                var (queue, mocks) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);

                mocks.UserService.Setup(m => m.GetGroup(other)).Returns(Application.PrivilegedGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other, CreateUploads(other, count: 10, offset: -100));
                uploads.TryAdd(username, CreateUploads(username, count: 3));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(2, queue.EstimatePosition(username, "file2"));
            }
        }

        public class ForecastPosition
        {
            [Theory]
            [InlineAutoData(QueueStrategy.RoundRobin)]
            [InlineAutoData(QueueStrategy.FirstInFirstOut)]
            public void Returns_Zero_If_Slot_Is_Available_Regardless_Of_Queue(QueueStrategy strategy, string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, strategy);
                SetSlotAvailable(queue, true);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other, CreateUploads(other, count: 10));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(0, queue.ForecastPosition(username).Position);
            }

            [Theory]
            [InlineAutoData(QueueStrategy.RoundRobin)]
            [InlineAutoData(QueueStrategy.FirstInFirstOut)]
            public void Returns_One_If_No_Slot_Is_Available_And_Queue_Is_Empty(QueueStrategy strategy, string username)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, strategy);
                SetSlotAvailable(queue, false);

                Assert.Equal(1, queue.ForecastPosition(username).Position);
            }

            [Theory, AutoData]
            public void RoundRobin_Returns_Number_Of_Users_In_Group_Plus_One(string username, string other1, string other2)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);
                SetSlotAvailable(queue, false);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other1, CreateUploads(other1, count: 10));
                uploads.TryAdd(other2, CreateUploads(other2, count: 1));

                queue.SetProperty("UploadDictionary", uploads);

                // worst case; the new file is last in the rotation. file counts don't matter
                Assert.Equal(3, queue.ForecastPosition(username).Position);
            }

            [Theory, AutoData]
            public void RoundRobin_Counts_Requesting_User_Once_Regardless_Of_Their_Queued_Files(string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);
                SetSlotAvailable(queue, false);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(username, CreateUploads(username, count: 10));
                uploads.TryAdd(other, CreateUploads(other, count: 1));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(3, queue.ForecastPosition(username).Position);
            }

            [Theory, AutoData]
            public void RoundRobin_Ignores_Users_In_Other_Groups(string username, string other1, string other2)
            {
                var (queue, mocks) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);
                SetSlotAvailable(queue, false);

                mocks.UserService.Setup(m => m.GetGroup(other2)).Returns(Application.PrivilegedGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other1, CreateUploads(other1, count: 1));
                uploads.TryAdd(other2, CreateUploads(other2, count: 1));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(2, queue.ForecastPosition(username).Position);
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Returns_Number_Of_Uploads_In_Group_Plus_One(string username, string other1, string other2)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);
                SetSlotAvailable(queue, false);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(username, CreateUploads(username, count: 2));
                uploads.TryAdd(other1, CreateUploads(other1, count: 3));
                uploads.TryAdd(other2, CreateUploads(other2, count: 4));

                queue.SetProperty("UploadDictionary", uploads);

                // the new file goes to the back of the queue, behind every upload in the group, including the user's own
                Assert.Equal(2 + 3 + 4 + 1, queue.ForecastPosition(username).Position);
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Counts_Uploads_In_Progress(string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);
                SetSlotAvailable(queue, false);

                var started = CreateUploads(other, count: 2);
                started.ForEach(u => u.Started = Now);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other, started);

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(3, queue.ForecastPosition(username).Position);
            }

            [Theory, AutoData]
            public void FirstInFirstOut_Ignores_Users_In_Other_Groups(string username, string other1, string other2)
            {
                var (queue, mocks) = GetFixture();

                SetStrategy(queue, QueueStrategy.FirstInFirstOut);
                SetSlotAvailable(queue, false);

                mocks.UserService.Setup(m => m.GetGroup(other2)).Returns(Application.PrivilegedGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other1, CreateUploads(other1, count: 3));
                uploads.TryAdd(other2, CreateUploads(other2, count: 10));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(4, queue.ForecastPosition(username).Position);
            }

            [Theory]
            [InlineAutoData(QueueStrategy.RoundRobin)]
            [InlineAutoData(QueueStrategy.FirstInFirstOut)]
            public void Returns_Group_And_Slot_Counts_If_No_Slot_Is_Available(QueueStrategy strategy, string username)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, strategy);
                SetSlotAvailable(queue, false);

                var (group, totalSlots, freeSlots, _) = queue.ForecastPosition(username);

                Assert.Equal(Application.DefaultGroup, group);
                Assert.Equal(1, totalSlots);
                Assert.Equal(0, freeSlots);
            }
        }

        public class Process
        {
            [Fact]
            public void Does_Nothing_If_MaxSlots_Is_Reached()
            {
                var (queue, _) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                FillSlots(groups[Application.DefaultGroup], queue.GetProperty<int>("GlobalSlots"));

                var result = queue.InvokeMethod<UploadGroup>("Process");

                Assert.Null(result);
            }

            [Theory, AutoData]
            public void Does_Not_Release_Upload_If_Global_Slots_Are_Consumed_Across_Groups(string user1, string file1)
            {
                var (queue, _) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                var globalSlots = queue.GetProperty<int>("GlobalSlots");

                // split the global slots between two groups; neither group is full on its own
                FillSlots(groups[Application.PrivilegedGroup], globalSlots - 1);
                FillSlots(groups[Application.LeecherGroup], 1);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(user1, new List<Upload>() { new Upload() { Username = user1, Filename = file1, Ready = DateTime.UtcNow } });

                queue.SetProperty("UploadDictionary", uploads);

                var result = queue.InvokeMethod<Upload>("Process");

                Assert.Null(result);
                Assert.Empty(groups[Application.DefaultGroup].UsedSlots);
            }

            [Fact]
            public void Does_Nothing_If_No_Uploads()
            {
                var (queue, _) = GetFixture();

                var result = queue.InvokeMethod<UploadGroup>("Process");

                Assert.Null(result);
            }

            [Theory, AutoData]
            public void Sets_Started_And_Group_Properties_Of_Released_Upload(string user1, string file1)
            {
                var (queue, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(user1)).Returns(Application.PrivilegedGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();

                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Ready = DateTime.UtcNow }
                });

                queue.SetProperty("UploadDictionary", uploads);

                var result = queue.InvokeMethod<Upload>("Process");

                Assert.Equal(user1, result.Username);
                Assert.Equal(file1, result.Filename);
                Assert.NotNull(result.Started);
                Assert.Equal(Application.PrivilegedGroup, result.Group);
            }

            [Theory, AutoData]
            public void Occupies_Slot_In_Group_Of_Released_Upload(string user1, string file1)
            {
                var (queue, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(user1)).Returns(Application.PrivilegedGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();

                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Ready = DateTime.UtcNow }
                });

                queue.SetProperty("UploadDictionary", uploads);

                _ = queue.InvokeMethod<Upload>("Process");

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Single(groups[Application.PrivilegedGroup].UsedSlots);
                Assert.Contains((user1, file1), groups[Application.PrivilegedGroup].UsedSlots);

                // no other group is charged for the slot
                Assert.All(groups.Values.Where(g => g.Name != Application.PrivilegedGroup), g => Assert.Empty(g.UsedSlots));
            }

            [Theory, AutoData]
            public void Does_Not_Release_Upload_That_Already_Started(string user1, string file1)
            {
                var (queue, _) = GetFixture();

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Ready = DateTime.UtcNow, Started = DateTime.UtcNow, Group = Application.DefaultGroup },
                });

                queue.SetProperty("UploadDictionary", uploads);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].UsedSlots.Add((user1, file1));

                var result = queue.InvokeMethod<Upload>("Process");

                Assert.Null(result);
                Assert.Single(groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public void Releases_Higher_Priority_Upload_First(string user1, string user2, string file1, string file2)
            {
                var (queue, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(user1)).Returns(Application.PrivilegedGroup);
                mocks.UserService.Setup(m => m.GetGroup(user2)).Returns(Application.DefaultGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();

                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Ready = DateTime.UtcNow }
                });

                uploads.TryAdd(user2, new List<Upload>()
                {
                    new Upload() { Username = user2, Filename = file2, Ready = DateTime.UtcNow }
                });

                queue.SetProperty("UploadDictionary", uploads);

                var result = queue.InvokeMethod<Upload>("Process");

                Assert.Equal(user1, result.Username);
                Assert.Equal(file1, result.Filename);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Contains((user1, file1), groups[Application.PrivilegedGroup].UsedSlots);
                Assert.Empty(groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public void Releases_Lower_Priority_Upload_First_If_All_Higher_Slots_Consumed_Or_Empty(string user1, string user2, string file1, string file2)
            {
                var (queue, mocks) = GetFixture();

                // no privileged uploads
                mocks.UserService.Setup(m => m.GetGroup(user1)).Returns(Application.DefaultGroup);
                mocks.UserService.Setup(m => m.GetGroup(user2)).Returns(Application.LeecherGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();

                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Ready = DateTime.UtcNow }
                });

                uploads.TryAdd(user2, new List<Upload>()
                {
                    new Upload() { Username = user2, Filename = file2, Ready = DateTime.UtcNow }
                });

                queue.SetProperty("UploadDictionary", uploads);

                // all default group slots consumed
                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Slots = 1;
                FillSlots(groups[Application.DefaultGroup], 1);

                var result = queue.InvokeMethod<Upload>("Process");

                // leecher group upload released
                Assert.Equal(user2, result.Username);
                Assert.Equal(file2, result.Filename);

                Assert.Contains((user2, file2), groups[Application.LeecherGroup].UsedSlots);
                Assert.DoesNotContain((user1, file1), groups[Application.DefaultGroup].UsedSlots);
            }

            [Theory, AutoData]
            public void Releases_First_Enqueued_Upload_When_Strategy_Is_FirstInFirstOut(string user1, string user2, string file1, string file2)
            {
                var (queue, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(user1)).Returns(Application.DefaultGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                var ready = DateTime.UtcNow;
                var enqueued = DateTime.UtcNow;

                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Enqueued = enqueued.AddHours(-1), Ready = ready }
                });

                uploads.TryAdd(user2, new List<Upload>()
                {
                    new Upload() { Username = user2, Filename = file2, Enqueued = enqueued.AddHours(-2), Ready = ready }
                });

                queue.SetProperty("UploadDictionary", uploads);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Strategy = QueueStrategy.FirstInFirstOut;

                var result = queue.InvokeMethod<Upload>("Process");

                Assert.Equal(user2, result.Username);
                Assert.Equal(file2, result.Filename);
            }

            [Theory, AutoData]
            public void Releases_First_Ready_Upload_When_Strategy_Is_RoundRobin(string user1, string user2, string file1, string file2)
            {
                var (queue, mocks) = GetFixture();

                mocks.UserService.Setup(m => m.GetGroup(user1)).Returns(Application.DefaultGroup);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                var ready = DateTime.UtcNow;
                var enqueued = DateTime.UtcNow;

                uploads.TryAdd(user1, new List<Upload>()
                {
                    new Upload() { Username = user1, Filename = file1, Enqueued = enqueued, Ready = ready }
                });

                uploads.TryAdd(user2, new List<Upload>()
                {
                    new Upload() { Username = user2, Filename = file2, Enqueued = enqueued, Ready = ready.AddMinutes(-1) }
                });

                queue.SetProperty("UploadDictionary", uploads);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Strategy = QueueStrategy.RoundRobin;

                var result = queue.InvokeMethod<Upload>("Process");

                Assert.Equal(user2, result.Username);
                Assert.Equal(file2, result.Filename);
            }
        }

        private static readonly DateTime Now = DateTime.UtcNow;

        private static void SetStrategy(UploadQueue queue, QueueStrategy strategy)
        {
            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
            groups[Application.DefaultGroup].Strategy = strategy;
        }

        private static void SetSlotAvailable(UploadQueue queue, bool available)
        {
            var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
            groups[Application.DefaultGroup].Slots = 1;
            groups[Application.DefaultGroup].UsedSlots.Clear();
            FillSlots(groups[Application.DefaultGroup], available ? 0 : 1);
        }

        // occupies the specified number of slots in the group with placeholder uploads
        private static void FillSlots(UploadGroup group, int count)
        {
            for (int i = 0; i < count; i++)
            {
                group.UsedSlots.Add(($"placeholder-user-{i}", $"placeholder-file-{i}"));
            }
        }

        // creates uploads named file0..fileN, enqueued at Now + offset + (i * step) seconds, in ascending order
        private static List<Upload> CreateUploads(string username, int count, int offset = 0, int step = 1)
        {
            return Enumerable.Range(0, count)
                .Select(i => new Upload()
                {
                    Username = username,
                    Filename = $"file{i}",
                    Enqueued = Now.AddSeconds(offset + (i * step)),
                })
                .ToList();
        }

        private static (UploadQueue queue, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);

            mocks.UserService.Setup(m => m.GetGroup(It.IsAny<string>()))
                .Returns(Application.DefaultGroup);

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
