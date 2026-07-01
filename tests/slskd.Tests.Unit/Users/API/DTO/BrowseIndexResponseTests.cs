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
        public void FromSoulseek_Returns_Empty_Collections_For_Empty_Browse_Response()
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

        [Fact]
        public void FromSoulseek_Maps_Locked_Directory_Metadata_Without_Files()
        {
            var directories = new[]
            {
                new Directory("Music\\Public", new[]
                {
                    new File(1, "public.mp3", 1000, "mp3", []),
                }),
            };
            var lockedDirectories = new[]
            {
                new Directory("Music\\Private", new[]
                {
                    new File(1, "private1.flac", 2000, "flac", []),
                    new File(1, "private2.flac", 3000, "flac", []),
                }),
                new Directory("Music\\Private\\Album", new[]
                {
                    new File(1, "track.flac", 4000, "flac", []),
                }),
            };
            var response = new BrowseResponse(directories, lockedDirectories);

            var result = BrowseIndexResponse.FromSoulseek(response);

            Assert.Single(result.Directories);
            Assert.Equal("Music\\Public", result.Directories.First().Name);
            Assert.Equal(1, result.Directories.First().FileCount);
            Assert.Equal(2, result.LockedDirectories.Count);
            Assert.Equal("Music\\Private", result.LockedDirectories.First().Name);
            Assert.Equal(2, result.LockedDirectories.First().FileCount);
            Assert.Equal("Music\\Private\\Album", result.LockedDirectories.Last().Name);
            Assert.Equal(1, result.LockedDirectories.Last().FileCount);
            Assert.Equal(1, result.Info.Directories);
            Assert.Equal(1, result.Info.Files);
            Assert.Equal(2, result.Info.LockedDirectories);
            Assert.Equal(3, result.Info.LockedFiles);
            Assert.DoesNotContain(
                typeof(BrowseIndexDirectory).GetProperties().Select(p => p.Name),
                name => name == "Files");
        }
    }
}
