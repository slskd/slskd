namespace slskd.Tests.Unit.Core
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using Xunit;

    public class GroupsOptionsTests
    {
        public class Validate
        {
            [Fact]
            public void Returns_No_Errors_When_No_Duplicate_Users()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            "group1",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user1", "user2" },
                            }
                        },
                        {
                            "group2",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user3", "user4" },
                            }
                        },
                    },
                    Blacklisted = new Options.GroupsOptions.BlacklistedOptions()
                    {
                        Members = new[] { "user5", "user6" },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Empty(results);
            }

            [Fact]
            public void Returns_Error_When_User_In_Multiple_UserDefined_Groups()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            "group1",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user1", "duplicateUser" },
                            }
                        },
                        {
                            "group2",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user2", "duplicateUser" },
                            }
                        },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Single(results);
                Assert.Contains("more than one group", results[0].ErrorMessage);
            }

            [Fact]
            public void Returns_Error_When_User_In_Both_UserDefined_And_Blacklisted_Groups()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            "group1",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user1", "duplicateUser" },
                            }
                        },
                    },
                    Blacklisted = new Options.GroupsOptions.BlacklistedOptions()
                    {
                        Members = new[] { "user2", "duplicateUser" },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Single(results);
                Assert.Contains("more than one group", results[0].ErrorMessage);
            }

            [Fact]
            public void Returns_Error_When_Multiple_Users_Are_Duplicated()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            "group1",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user1", "duplicate1", "duplicate2" },
                            }
                        },
                        {
                            "group2",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "duplicate1", "user2" },
                            }
                        },
                    },
                    Blacklisted = new Options.GroupsOptions.BlacklistedOptions()
                    {
                        Members = new[] { "duplicate2", "user3" },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Single(results);
                Assert.Contains("more than one group", results[0].ErrorMessage);
            }

            [Fact]
            public void Returns_No_Error_When_Groups_Are_Empty()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>(),
                    Blacklisted = new Options.GroupsOptions.BlacklistedOptions()
                    {
                        Members = new string[] { },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Empty(results);
            }

            [Fact]
            public void Returns_Error_For_Built_In_Group_Name_Collision()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            "privileged",  // This is a built-in group name
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user1" },
                            }
                        },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Single(results);
                Assert.Contains("collides with a built in group", results[0].ErrorMessage);
            }

            [Fact]
            public void Returns_Multiple_Errors_When_Both_Name_Collision_And_Duplicate_Users()
            {
                // Arrange
                var options = new Options.GroupsOptions()
                {
                    UserDefined = new Dictionary<string, Options.GroupsOptions.UserDefinedOptions>()
                    {
                        {
                            "privileged",  // Built-in group name collision
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "user1", "duplicateUser" },
                            }
                        },
                        {
                            "group2",
                            new Options.GroupsOptions.UserDefinedOptions()
                            {
                                Members = new[] { "duplicateUser", "user2" },
                            }
                        },
                    }
                };

                // Act
                var results = options.Validate(new ValidationContext(options)).ToList();

                // Assert
                Assert.Equal(2, results.Count);
                Assert.Contains(results, r => r.ErrorMessage.Contains("collides with a built in group"));
                Assert.Contains(results, r => r.ErrorMessage.Contains("more than one group"));
            }
        }
    }
}
