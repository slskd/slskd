using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class SanitizePathTests
    {
        [Fact]
        public void NullOs_UsesPlatformDefault_DoesNotThrow()
        {
            var result = "foo/bar".SanitizePath();

            Assert.NotNull(result);
        }

        [Theory]
        [InlineData("foo/bar", "foo/bar")]
        [InlineData("foo\\bar", "foo/bar")]
        [InlineData("Artist/Album/song.flac", "Artist/Album/song.flac")]
        [InlineData("Ünïcödé/пользователь", "Ünïcödé/пользователь")]
        public void Linux_NormalizesSlashes(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/bar", "foo\\bar")]
        [InlineData("foo\\bar", "foo\\bar")]
        [InlineData("Artist/Album/song.flac", "Artist\\Album\\song.flac")]
        public void Windows_NormalizesSlashes(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "Music\\Artist")]
        [InlineData("C:/Music/Artist", "Music\\Artist")]
        [InlineData("D:\\path\\to\\file", "path\\to\\file")]
        [InlineData("Z:\\", "")]
        public void Windows_Strips_DriveRoot(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "Music/Artist")]
        [InlineData("C:/Music/Artist", "Music/Artist")]
        [InlineData("D:\\path", "path")]
        public void Linux_Strips_DriveRoot(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\\\server\\share\\Music", "share\\Music")]
        [InlineData("\\\\server\\Music", "Music")]
        [InlineData("\\\\192.168.1.1\\share\\folder", "share\\folder")]
        public void Windows_Strips_UncRoot(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("//server/share/Music", "share/Music")]
        [InlineData("//server/Music", "Music")]
        [InlineData("//192.168.1.1/share/folder", "share/folder")]
        public void Linux_Strips_UncRoot(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde/Music/Artist", "Music/Artist")]
        [InlineData("@@abcdefgh/Music", "Music")]
        [InlineData("@@abcde\\Music\\Artist", "Music/Artist")]
        public void Linux_Strips_SoulseekQtPrefix(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde\\Music\\Artist", "Music\\Artist")]
        [InlineData("@@abcdefgh\\Music", "Music")]
        [InlineData("@@abcde/Music/Artist", "Music\\Artist")]
        public void Windows_Strips_SoulseekQtPrefix(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abc/Music")]    // only 3 alphanumeric chars — does not match
        [InlineData("@@abcd/Music")]   // only 4 — does not match
        public void DoesNotStrip_SoulseekQtPrefix_When_Prefix_Too_Short(string input)
        {
            var result = input.SanitizePath(os: OSPlatform.Linux);

            Assert.StartsWith("@@", result);
        }

        [Theory]
        [InlineData("foo/../bar", "foo/_/bar")]
        [InlineData("foo/./bar", "foo/_/bar")]
        [InlineData("../etc", "_/etc")]
        [InlineData("./tmp", "_/tmp")]
        [InlineData("foo/..", "foo/_")]
        [InlineData("foo/.", "foo/_")]
        public void Linux_Replaces_TraversalSegments(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo\\..\\bar", "foo\\_\\bar")]
        [InlineData("foo\\.\\bar", "foo\\_\\bar")]
        [InlineData("..\\etc", "_\\etc")]
        public void Windows_Replaces_TraversalSegments(string input, string expected)
        {
            var result = input.SanitizePath(os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Linux_Replaces_InvalidFilenameChars_In_Segments()
        {
            var result = "seg\0ment/file\0name".SanitizePath(os: OSPlatform.Linux);

            Assert.Equal("seg_ment/file_name", result);
        }

        [Fact]
        public void Windows_Replaces_InvalidFilenameChars_In_Segments()
        {
            var result = "seg*ment\\file:name".SanitizePath(os: OSPlatform.Windows);

            Assert.Equal("seg_ment\\file_name", result);
        }

        [Fact]
        public void Uses_Custom_Replacement()
        {
            var result = "foo/../bar".SanitizePath(replacement: '-', os: OSPlatform.Linux);

            Assert.Equal("foo/-/bar", result);
        }
    }
}
