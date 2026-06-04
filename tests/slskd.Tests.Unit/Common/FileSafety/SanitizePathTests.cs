using System;
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
            var result = FileSafety.SanitizePath("foo/bar");

            Assert.NotNull(result);
        }

        [Theory]
        [InlineData("foo/bar", "foo/bar")]
        [InlineData("foo\\bar", "foo/bar")]
        [InlineData("Artist/Album/song.flac", "Artist/Album/song.flac")]
        [InlineData("Ünïcödé/пользователь", "Ünïcödé/пользователь")]
        public void Linux_NormalizesSlashes(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/bar", "foo\\bar")]
        [InlineData("foo\\bar", "foo\\bar")]
        [InlineData("Artist/Album/song.flac", "Artist\\Album\\song.flac")]
        public void Windows_NormalizesSlashes(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "Music\\Artist")]
        [InlineData("C:/Music/Artist", "Music\\Artist")]
        [InlineData("D:\\path\\to\\file", "path\\to\\file")]
        [InlineData("Z:\\", "")]
        public void Windows_Strips_DriveRoot(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "Music/Artist")]
        [InlineData("C:/Music/Artist", "Music/Artist")]
        [InlineData("D:\\path", "path")]
        public void Linux_Strips_DriveRoot(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\\\server\\share\\Music", "share\\Music")]
        [InlineData("\\\\server\\Music", "Music")]
        [InlineData("\\\\192.168.1.1\\share\\folder", "share\\folder")]
        public void Windows_Strips_UncRoot(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("//server/share/Music", "share/Music")]
        [InlineData("//server/Music", "Music")]
        [InlineData("//192.168.1.1/share/folder", "share/folder")]
        public void Linux_Strips_UncRoot(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde/Music/Artist", "Music/Artist")]
        [InlineData("@@abcdefgh/Music", "Music")]
        [InlineData("@@abcde\\Music\\Artist", "Music/Artist")]
        public void Linux_Strips_SoulseekQtPrefix(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde\\Music\\Artist", "Music\\Artist")]
        [InlineData("@@abcdefgh\\Music", "Music")]
        [InlineData("@@abcde/Music/Artist", "Music\\Artist")]
        public void Windows_Strips_SoulseekQtPrefix(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abc/Music")]    // only 3 alphanumeric chars — does not match
        [InlineData("@@abcd/Music")]   // only 4 — does not match
        public void DoesNotStrip_SoulseekQtPrefix_When_Prefix_Too_Short(string input)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

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
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo\\..\\bar", "foo\\_\\bar")]
        [InlineData("foo\\.\\bar", "foo\\_\\bar")]
        [InlineData("..\\etc", "_\\etc")]
        public void Windows_Replaces_TraversalSegments(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Linux_Replaces_InvalidFilenameChars_In_Segments()
        {
            var result = FileSafety.SanitizePath("seg\0ment/file\0name", os: OperatingSystem.Linux);

            Assert.Equal("seg_ment/file_name", result);
        }

        [Fact]
        public void Windows_Replaces_InvalidFilenameChars_In_Segments()
        {
            var result = FileSafety.SanitizePath("seg*ment\\file:name", os: OperatingSystem.Windows);

            Assert.Equal("seg_ment\\file_name", result);
        }

        [Fact]
        public void Uses_Custom_Replacement()
        {
            var result = FileSafety.SanitizePath("foo/../bar", replacement: '-', os: OperatingSystem.Linux);

            Assert.Equal("foo/-/bar", result);
        }

        [Fact]
        public void Returns_Null_Given_Null_Path()
        {
            var result = FileSafety.SanitizePath(null);

            Assert.Null(result);
        }

        // --- retainRoot: true ---
        // These verify that SanitizePath honours retainRoot. The UNC and single-slash absolute
        // cases are EXPECTED TO FAIL: SanitizePath filters empty segments produced by splitting on
        // leading slashes, so "//server/share" becomes "server/share" — the root is silently lost.
        // Drive-letter and @@-prefix cases pass because they produce no empty segments.


        [Theory]
        [InlineData("C:/Music/Artist", "C:/Music/Artist")]
        [InlineData("C:\\Music\\Artist", "C:/Music/Artist")]
        public void Linux_RetainsDriveRoot_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Music\\Artist", "C_\\Music\\Artist")]
        [InlineData("C:/Music/Artist", "C_\\Music\\Artist")]
        public void Windows_RetainsDriveRoot_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde/Music/Artist", "@@abcde/Music/Artist")]
        [InlineData("@@abcde\\Music\\Artist", "@@abcde/Music/Artist")]
        public void Linux_RetainsSoulseekQtPrefix_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde\\Music\\Artist", "@@abcde\\Music\\Artist")]
        [InlineData("@@abcde/Music/Artist", "@@abcde\\Music\\Artist")]
        public void Windows_RetainsSoulseekQtPrefix_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("//server/share/Music", "//server/share/Music")]
        [InlineData("//server/Music", "//server/Music")]
        [InlineData("\\\\server\\share\\Music", "//server/share/Music")]
        [InlineData("\\\\server\\Music", "//server/Music")]
        public void Linux_RetainsUncRoot_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/absolute/Music", "/absolute/Music")]
        [InlineData("/single", "/single")]
        [InlineData("\\absolute\\Music", "/absolute/Music")]
        [InlineData("\\single", "/single")]
        public void Linux_RetainsAbsoluteRoot_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\\\server\\share\\Music", "\\\\server\\share\\Music")]
        [InlineData("\\\\server\\Music", "\\\\server\\Music")]
        [InlineData("//server/share/Music", "\\\\server\\share\\Music")]
        [InlineData("//server/Music", "\\\\server\\Music")]
        public void Windows_RetainsUncRoot_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\absolute\\Music", "\\absolute\\Music")]
        [InlineData("/absolute/Music", "\\absolute\\Music")]
        public void Windows_RetainsDriveRelativeRoot_When_RetainRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }
    }
}