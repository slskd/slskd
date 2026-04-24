namespace slskd.Tests.Unit.Users
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using AutoFixture.Xunit2;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Internal;
    using Moq;
    using slskd.Users;
    using Soulseek;
    using Xunit;

    public class UserServiceTests
    {
        public class GetGroup
        {
            [Theory, AutoData]
            public void Returns_Default_For_All_Users_If_No_User_Defined_Groups_Configured(string username)
            {
                var (service, _) = GetFixture();

                Assert.Equal(Application.DefaultGroup, service.GetGroup(username));
            }

            [Fact]
            public void Returns_Default_If_Username_Is_Null()
            {
                var (service, _) = GetFixture();

                Assert.Equal(Application.DefaultGroup, service.GetGroup(null));
            }

            [Theory, AutoData]
            public void Returns_User_Defined_Group(string group, string username)
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
                                        Members = new[] { username },
                                    }
                                },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.Equal(group, service.GetGroup(username));
            }
        }

        public class IsBlacklisted
        {
            [Fact]
            public void Throws_If_Username_Is_Null()
            {
                var (service, _) = GetFixture();

                Assert.Throws<ArgumentNullException>(() => service.IsBlacklisted(null));
            }

            [Theory, AutoData]
            public void Returns_Cached_Value_On_Cache_Hit(string username)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Members = new[] { username },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                // prime the cache with a computed result
                service.IsBlacklisted(username);

                // bypassCache = false returns false on a cache miss; if the result is still true,
                // the value was returned from cache rather than computed again
                Assert.True(service.IsBlacklisted(username, bypassCache: false));
            }

            [Theory, AutoData]
            public void Returns_False_On_Cache_Miss_With_BypassCache_False(string username)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Members = new[] { username },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                // the user is blacklisted, but bypassCache = false on a cache miss returns false
                // without computing, so the result is false even though the user is blacklisted
                Assert.False(service.IsBlacklisted(username, bypassCache: false));
            }

            [Theory, AutoData]
            public void Returns_True_If_Username_Is_Blacklisted_And_BypassCache_True(string username)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Members = new[] { username },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.True(service.IsBlacklisted(username, bypassCache: true));
            }

            [Theory, AutoData]
            public void Returns_True_If_Username_Matches_Blacklist_Pattern(string prefix, string suffix)
            {
                var username = $"{prefix}_blacklisted_{suffix}";

                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Patterns = new[] { @".*_blacklisted_.*" },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.True(service.IsBlacklisted(username));
            }

            [Theory, AutoData]
            public void Returns_True_If_IP_Address_Is_In_Blacklisted_CIDR(string username)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Cidrs = new[] { "192.168.100.0/24" },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.True(service.IsBlacklisted(username, IPAddress.Parse("192.168.100.5")));
            }

            [Theory, AutoData]
            public async Task Returns_True_If_IP_Address_Is_In_Blacklist_File(string username)
            {
                var tempFile = Path.GetTempFileName();

                try
                {
                    System.IO.File.WriteAllText(tempFile, "192.168.200.0/24\n");

                    var (service, _) = GetFixture();

                    var blacklist = new Blacklist();
                    await blacklist.Load(tempFile, BlacklistFormat.CIDR);

                    service.SetField("<Blacklist>k__BackingField", blacklist);

                    Assert.True(service.IsBlacklisted(username, IPAddress.Parse("192.168.200.5")));
                }
                finally
                {
                    System.IO.File.Delete(tempFile);
                }
            }

            [Theory, AutoData]
            public void Returns_False_If_Not_Blacklisted_By_Any_Method(string username)
            {
                var (service, _) = GetFixture();

                Assert.False(service.IsBlacklisted(username, IPAddress.Parse("10.0.0.1")));
            }

            [Theory, AutoData]
            public void Caches_Result_Of_IsBlacklistedInternal(string username)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Members = new[] { username },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                // first call: cache miss, IsBlacklistedInternal is called, result is cached
                Assert.True(service.IsBlacklisted(username));

                // second call with bypassCache = false: returns cached true rather than the
                // false that a cache miss would produce, confirming the result was cached
                Assert.True(service.IsBlacklisted(username, bypassCache: false));
            }
        }

        public class Configuration
        {
            [Theory, AutoData]
            public void Reconfigures_Groups_When_Options_Change(string group, string user)
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
                                        Members = new[] { user },
                                    }
                                },
                            }
                        }
                    }
                };

                // do not pass options; only default groups
                var (service, mocks) = GetFixture();

                // ensure defaults
                Assert.Equal(Application.DefaultGroup, service.GetGroup(user));

                // reconfigure with options
                mocks.OptionsMonitor.RaiseOnChange(options);

                Assert.Equal(group, service.GetGroup(user));
            }

            [Theory, AutoData]
            public void Gives_Lowest_Priority_Group_To_Users_Appearing_In_Multiple_Groups(string group0, string group100, string user)
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
                                    group100,
                                    new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                    {
                                        Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions() { Priority = 100 },
                                        Members = new[] { user },
                                    }
                                },
                                {
                                    group0,
                                    new Options.TransfersOptions.GroupsOptions.UserDefinedOptions()
                                    {
                                        Upload = new Options.TransfersOptions.GroupsOptions.BaseGroupOptions.GroupUploadOptions() { Priority = 0 },
                                        Members = new[] { user },
                                    }
                                },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.Equal(group0, service.GetGroup(user));
            }
        }

        public class BlacklistDecisionCache
        {
            [Theory, AutoData]
            public void Clears_Cache_When_Options_Change(string username)
            {
                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Members = new[] { username },
                            }
                        }
                    }
                };

                var (service, mocks) = GetFixture(options);

                Assert.True(service.IsBlacklisted(username));
                Assert.True(service.IsBlacklisted(username, bypassCache: false));

                mocks.OptionsMonitor.RaiseOnChange(new Options());

                Assert.False(service.IsBlacklisted(username, bypassCache: false));
            }

            [Theory, AutoData]
            public void Clears_Cache_When_Options_Change_Verified(string username)
            {
                var (service, mocks) = GetFixture();

                var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
                service.SetField("<BlacklistDecisionCache>k__BackingField", cache);

                cache.Set(username, true, new MemoryCacheEntryOptions { Size = 1 });
                Assert.True(cache.TryGetValue(username, out _));

                mocks.OptionsMonitor.RaiseOnChange(new Options());

                Assert.False(cache.TryGetValue(username, out _));
            }

            [Theory, AutoData]
            public void Expires_Cache_Entries_After_10_Minutes(string username)
            {
                var clock = new TestSystemClock();

                var options = new Options()
                {
                    Transfers = new Options.TransfersOptions
                    {
                        Groups = new Options.TransfersOptions.GroupsOptions
                        {
                            Blacklisted = new Options.TransfersOptions.GroupsOptions.BlacklistedOptions
                            {
                                Members = new[] { username },
                            }
                        }
                    }
                };

                var (service, _) = GetFixture(options, clock);

                Assert.True(service.IsBlacklisted(username));
                Assert.True(service.IsBlacklisted(username, bypassCache: false));

                clock.UtcNow = clock.UtcNow.AddMinutes(11);

                Assert.False(service.IsBlacklisted(username, bypassCache: false));
            }

            [Theory, AutoData]
            public void Expires_Cache_Entries_After_10_Minutes_Verified(string username)
            {
                var clock = new TestSystemClock();

                var (service, _) = GetFixture();

                var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock, SizeLimit = 1000 });
                service.SetField("<BlacklistDecisionCache>k__BackingField", cache);

                service.IsBlacklisted(username);
                Assert.True(cache.TryGetValue(username, out _));

                clock.UtcNow = clock.UtcNow.AddMinutes(11);

                Assert.False(cache.TryGetValue(username, out _));
            }
        }

        private static (UserService service, Mocks mocks) GetFixture(Options options = null, ISystemClock systemClock = null)
        {
            var mocks = new Mocks(options, systemClock);
            var service = new UserService(
                mocks.SoulseekClient.Object,
                mocks.OptionsMonitor,
                mocks.SystemClock);

            return (service, mocks);
        }

        private class Mocks
        {
            public Mocks(Options options = null, ISystemClock systemClock = null)
            {
                OptionsMonitor = new TestOptionsMonitor<Options>(options ?? new Options());
                SystemClock = systemClock;
            }

            public Mock<ISoulseekClient> SoulseekClient { get; } = new Mock<ISoulseekClient>();
            public TestOptionsMonitor<Options> OptionsMonitor { get; init; }
            public ISystemClock SystemClock { get; init; }
        }

        private class TestSystemClock : ISystemClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        }
    }
}
