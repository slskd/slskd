using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class StripPathRootTests
    {
        [Fact]
        public void NullOs_UsesPlatformDefault_DoesNotThrow()
        {
            var result = FileSafety.StripPathRoot("foo/bar");

            Assert.NotNull(result);
        }

        [Fact]
        public void Returns_Null_Given_Null_Path()
        {
            var result = FileSafety.StripPathRoot(null);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "Music/Artist")]
        [InlineData("C:/Music/Artist", "Music/Artist")]
        [InlineData("D:\\path\\to\\file", "path/to/file")]
        [InlineData("Z:\\", "")]
        [InlineData("Z:/", "")]
        [InlineData("Z:", "")]              // bare drive letter, no separator
        [InlineData("c:\\Music", "Music")]  // lowercase drive letter
        [InlineData("A:/single", "single")]
        public void Linux_Strips_DriveRoot(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "Music\\Artist")]
        [InlineData("C:/Music/Artist", "Music\\Artist")]
        [InlineData("D:\\path\\to\\file", "path\\to\\file")]
        [InlineData("Z:\\", "")]
        [InlineData("Z:", "")]
        [InlineData("c:\\Music", "Music")]
        public void Windows_Strips_DriveRoot(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("//server/share/Music", "share/Music")]
        [InlineData("//server/Music", "Music")]
        [InlineData("//192.168.1.1/share/folder", "share/folder")]
        [InlineData("//server", "")]   // no path after server
        public void Linux_Strips_UncRoot(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\\\server\\share\\Music", "share\\Music")]
        [InlineData("\\\\server\\Music", "Music")]
        [InlineData("\\\\192.168.1.1\\share\\folder", "share\\folder")]
        [InlineData("\\\\server", "")]    // no path after server
        public void Windows_Strips_UncRoot(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("//server/share/Music", "share\\Music")]
        [InlineData("//server/Music", "Music")]
        [InlineData("//192.168.1.1/share/folder", "share\\folder")]
        [InlineData("//server", "")]
        public void Windows_Strips_ForwardSlash_UncRoot(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde/Music/Artist", "Music/Artist")]
        [InlineData("@@abcdefgh/Music", "Music")]
        [InlineData("@@abcde\\Music\\Artist", "Music/Artist")]
        public void Linux_Strips_SoulseekQtPrefix(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde\\Music\\Artist", "Music\\Artist")]
        [InlineData("@@abcdefgh\\Music", "Music")]
        [InlineData("@@abcde/Music/Artist", "Music\\Artist")]
        public void Windows_Strips_SoulseekQtPrefix(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abc/Music")]    // only 3 alphanumeric chars — does not match
        [InlineData("@@abcd/Music")]   // only 4 — does not match
        public void DoesNotStrip_SoulseekQtPrefix_When_Prefix_Too_Short(string input)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.StartsWith("@@", result);
        }

        [Fact]
        public void Linux_Strips_Bare_SoulseekQtPrefix_Returning_Empty()
        {
            var result = FileSafety.StripPathRoot("@@abcde", OperatingSystem.Linux);

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("//")]
        public void Linux_ReturnsUnchanged_Given_Slash_Only_Path(string input)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.Equal(input, result);
        }

        [Theory]
        [InlineData("Music/Artist", "Music/Artist")]
        [InlineData("Artist/Album/song.flac", "Artist/Album/song.flac")]
        [InlineData("just_a_filename.flac", "just_a_filename.flac")]
        [InlineData("", "")]
        [InlineData("foo", "foo")]
        public void Linux_ReturnsUnchanged_Given_Relative_Path(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Music\\Artist", "Music\\Artist")]
        [InlineData("Artist\\Album\\song.flac", "Artist\\Album\\song.flac")]
        [InlineData("just_a_filename.flac", "just_a_filename.flac")]
        [InlineData("foo", "foo")]
        public void Windows_ReturnsUnchanged_Given_Relative_Path(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/home/user/Music", "/home/user/Music")]
        [InlineData("/Music", "/Music")]
        [InlineData("/single", "/single")]
        public void Linux_ReturnsUnchanged_Given_Single_ForwardSlash_Prefix(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\Music", "\\Music")]
        [InlineData("\\home\\user", "\\home\\user")]
        public void Windows_ReturnsUnchanged_Given_Single_BackwardSlash_Prefix(string input, string expected)
        {
            var result = FileSafety.StripPathRoot(input, OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }
    }
}