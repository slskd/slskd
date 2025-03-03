namespace slskd.Tests.Unit.Users
{
    using System.Collections.Generic;
    using System.IO;
    using AutoFixture.Xunit2;
    using Moq;
    using slskd.Files;
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
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            group,
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { username },
                            }
                        },
                    }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.Equal(group, service.GetGroup(username));
            }
        }

        public class Configuration
        {
            [Theory, AutoData]
            public void Reconfigures_Groups_When_Options_Change(string group, string user)
            {
                var options = new Options()
                {
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        { group, new Options.GroupsOptions.UserDefinedOptions()
                        {
                            Members = new[] { user },
                        } },
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
                    Groups = new Options.GroupsOptions()
                    {
                        UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                        {
                            {
                                group100,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions() { Priority = 100 },
                                    Members = new[] { user },
                                }
                            },
                            {
                                group0,
                                new Options.GroupsOptions.UserDefinedOptions()
                                {
                                    Upload = new Options.GroupsOptions.UploadOptions() { Priority = 0 },
                                    Members = new[] { user },
                                }
                            },
                        }
                    }
                };

                var (service, _) = GetFixture(options);

                Assert.Equal(group0, service.GetGroup(user));
            }
        }

        public class GetProfilePicture
        {
            [Theory]
            [InlineData(null)]
            [InlineData("")]
            [InlineData(" ")]
            public void Returns_Null_When_Path_Is_NullOrWhitespace(string path)
            {
                var (service, _) = GetFixture();
                
                var result = service.GetProfilePicture(path);
                
                Assert.Null(result);
            }

            [Fact]
            public void Returns_Null_When_File_Does_Not_Exist()
            {
                const string nonExistentPath = "nonexistent-file.jpg";
                var (service, _) = GetFixture();
                
                var result = service.GetProfilePicture(nonExistentPath);
                
                Assert.Null(result);
            }

            [Fact]
            public void Returns_File_Bytes_When_File_Exists()
            {
                var tempFile = Path.GetTempFileName();
                try
                {
                    // Write some test data to the temp file
                    System.IO.File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3, 4, 5 });
                    
                    var (service, _) = GetFixture();
                    
                    var result = service.GetProfilePicture(tempFile);
                    
                    Assert.NotNull(result);
                    Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result);
                }
                finally
                {
                    // Clean up
                    if (System.IO.File.Exists(tempFile))
                    {
                        System.IO.File.Delete(tempFile);
                    }
                }
            }
        }

        private static (UserService governor, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);
            var service = new UserService(
                mocks.SoulseekClient.Object,
                mocks.OptionsMonitor,
                mocks.FileService);

            return (service, mocks);
        }

        private class Mocks
        {
            public Mocks(Options options = null)
            {
                OptionsMonitor = new TestOptionsMonitor<Options>(options ?? new Options());
                FileService = new FileService(OptionsMonitor);
            }

            public Mock<ISoulseekClient> SoulseekClient { get; } = new Mock<ISoulseekClient>();
            public TestOptionsMonitor<Options> OptionsMonitor { get; init; }
            public FileService FileService { get; init; }
        }
    }
}
