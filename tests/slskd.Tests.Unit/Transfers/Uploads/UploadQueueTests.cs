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
            Assert.Equal(0, p.UsedSlots);
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
            Assert.Equal(0, p.UsedSlots);
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
            Assert.Equal(0, p.UsedSlots);
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
                Assert.Equal(0, p.UsedSlots);
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
                Assert.Equal(0, p.UsedSlots);
                Assert.Equal(strategy, p.Strategy);
            }

            [Theory, AutoData]
            public void Retains_Used_Slot_Count_When_Options_Change(string group, int newPriority, int usedSlots)
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
                groups[group].UsedSlots = usedSlots;

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
            public async Task Decrements_UsedSlots_For_Group(string username, string filename, string filename2)
            {
                var (queue, _) = GetFixture();

                queue.Enqueue(username, filename);
                await queue.AwaitStartAsync(username, filename);

                queue.Enqueue(username, filename2);
                await queue.AwaitStartAsync(username, filename2);

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Equal(2, groups[Application.DefaultGroup].UsedSlots);

                queue.Complete(username, filename);

                groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                Assert.Equal(1, groups[Application.DefaultGroup].UsedSlots);
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
            public void RoundRobin_Returns_Zero_For_First_File_Regardless_Of_Other_Users(string username, string other)
            {
                var (queue, _) = GetFixture();

                SetStrategy(queue, QueueStrategy.RoundRobin);

                var uploads = new ConcurrentDictionary<string, List<Upload>>();
                uploads.TryAdd(other, CreateUploads(other, count: 10, offset: -100));
                uploads.TryAdd(username, CreateUploads(username, count: 3));

                queue.SetProperty("UploadDictionary", uploads);

                Assert.Equal(0, queue.EstimatePosition(username, "file0"));
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

                // 4 (local) + min(4, 5) + min(4, 2) + min(4, 12)
                Assert.Equal(4 + 4 + 2 + 4, queue.EstimatePosition(d, "file4"));
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

            private static void SetStrategy(UploadQueue queue, QueueStrategy strategy)
            {
                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");
                groups[Application.DefaultGroup].Strategy = strategy;
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
        }

        public class Process
        {
            [Fact]
            public void Does_Nothing_If_MaxSlots_Is_Reached()
            {
                var (queue, _) = GetFixture();

                var groups = queue.GetProperty<Dictionary<string, UploadGroup>>("Groups");

                groups[Application.DefaultGroup].UsedSlots = int.MaxValue;

                var result = queue.InvokeMethod<UploadGroup>("Process");

                Assert.Null(result);
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
            public void Increments_UsedSlots_Of_Group(string user1, string file1)
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

                Assert.Equal(1, groups[Application.PrivilegedGroup].UsedSlots);
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
                groups[Application.DefaultGroup].UsedSlots = 1;

                var result = queue.InvokeMethod<Upload>("Process");

                // leecher group upload released
                Assert.Equal(user2, result.Username);
                Assert.Equal(file2, result.Filename);
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
