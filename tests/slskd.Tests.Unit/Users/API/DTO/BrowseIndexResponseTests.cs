namespace slskd.Tests.Unit.Users.API.DTO
{
    using System;
    using System.Linq;
    using slskd.Users.API;
    using Soulseek;
    using Xunit;

    public class BrowseIndexResponseTests
    {
        [Fact]
        public void FromSoulseek_Maps_Unlocked_Directory_Metadata_Without_Files()
        {
            var directories = new[]
            {
                new Directory("Music\\Artist", new[]
                {
                    new File(1, "one.flac", 123, "flac", []),
                    new File(1, "two.flac", 456, "flac", []),
                }),
            };
            var response = new BrowseResponse(directories);

            var result = BrowseIndexResponse.FromSoulseek(response);

            var directory = Assert.Single(result.Directories);
            Assert.Equal("Music\\Artist", directory.Name);
            Assert.Equal(2, directory.FileCount);
            Assert.Empty(result.LockedDirectories);
            Assert.Equal(1, result.Info.Directories);
            Assert.Equal(2, result.Info.Files);
            Assert.Equal(0, result.Info.LockedDirectories);
            Assert.Equal(0, result.Info.LockedFiles);
            Assert.DoesNotContain(
                typeof(BrowseIndexDirectory).GetProperties().Select(p => p.Name),
                name => name == "Files");
        }

        [Fact]
        public void FromSoulseek_Maps_Locked_Directory_Metadata_And_Counts()
        {
            var directories = Array.Empty<Directory>();
            var response = new BrowseResponse(directories);

            var result = BrowseIndexResponse.FromSoulseek(response);

            Assert.Empty(result.Directories);
            Assert.Empty(result.LockedDirectories);
            Assert.Equal(0, result.Info.Directories);
            Assert.Equal(0, result.Info.Files);
            Assert.Equal(0, result.Info.LockedDirectories);
            Assert.Equal(0, result.Info.LockedFiles);
        }

        [Fact]
        public void FromSoulseek_Treats_Null_Collections_As_Empty()
        {
            var response = new BrowseResponse(null);

            var result = BrowseIndexResponse.FromSoulseek(response);

            Assert.Empty(result.Directories);
            Assert.Empty(result.LockedDirectories);
            Assert.Equal(0, result.Info.Directories);
            Assert.Equal(0, result.Info.Files);
            Assert.Equal(0, result.Info.LockedDirectories);
            Assert.Equal(0, result.Info.LockedFiles);
        }
    }
}
