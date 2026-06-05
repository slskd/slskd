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
    }
}