namespace slskd.Tests.Unit.Common;

using Xunit;

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
        [InlineData("foo//bar", "foo/bar")]
        [InlineData("foo\\\\bar", "foo/bar")]
        public void Linux_NormalizesSlashes(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/bar", "foo\\bar")]
        [InlineData("foo\\bar", "foo\\bar")]
        [InlineData("Artist/Album/song.flac", "Artist\\Album\\song.flac")]
        [InlineData("foo//bar", "foo\\bar")]
        [InlineData("foo\\\\bar", "foo\\bar")]
        public void Windows_NormalizesSlashes(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
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

        [Fact]
        public void Returns_EmptyString_Given_EmptyString()
        {
            var result = FileSafety.SanitizePath(string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("/foo/bar")]
        [InlineData("//foo/bar")]
        [InlineData("\\foo\\bar")]
        [InlineData("\\\\foo\\bar")]
        public void Linux_NeverReturns_LeadingSlash(string input)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.False(result.StartsWith('/') || result.StartsWith('\\'));
        }

        [Theory]
        [InlineData("/foo/bar")]
        [InlineData("//foo/bar")]
        [InlineData("\\foo\\bar")]
        [InlineData("\\\\foo\\bar")]
        public void Windows_NeverReturns_LeadingSlash(string input)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Windows);

            Assert.False(result.StartsWith('/') || result.StartsWith('\\'));
        }

        [Fact]
        public void Linux_NeverReturns_LeadingSlash_When_First_Segment_Sanitizes_To_Empty()
        {
            // '\0' replaced with '.' produces "." — SanitizePathSegment converts that to ""
            // joining ["", "foo"] with '/' would yield "/foo" without a guard
            var result = FileSafety.SanitizePath("\0/foo", replacement: '.', os: OperatingSystem.Linux);

            Assert.False(result.StartsWith('/') || result.StartsWith('\\'));
        }

        [Theory]
        [InlineData("../..")]
        [InlineData("./.")]
        public void Linux_Replaces_All_Traversal_Segments(string input)
        {
            var result = FileSafety.SanitizePath(input, os: OperatingSystem.Linux);

            Assert.Equal("_/_", result);
        }

        [Fact]
        public void Linux_Handles_SoulseekQt_Prefixed_Path()
        {
            var result = FileSafety.SanitizePath("@@abcde/foo/bar", os: OperatingSystem.Linux);

            Assert.Equal("@@abcde/foo/bar", result);
        }

        [Fact]
        public void Windows_Sanitizes_Colon_In_Drive_Like_Segment()
        {
            var result = FileSafety.SanitizePath("C:/Music/song.flac", os: OperatingSystem.Windows);

            Assert.Equal("C_\\Music\\song.flac", result);
        }
    }
}