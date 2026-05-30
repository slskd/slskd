using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class GetFileNameSafelyTests
    {
        [Fact]
        public void Throws_Given_Null()
        {
            var ex = Record.Exception(() => FileSafety.GetFileNameSafely(null));

            Assert.NotNull(ex);
        }

        [Fact]
        public void Returns_Empty_Given_Empty()
        {
            var result = FileSafety.GetFileNameSafely("");

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("\\")]
        [InlineData("C:\\")]
        [InlineData("C:/")]
        [InlineData("//")]
        [InlineData("\\\\")]
        [InlineData("foo\\")]
        [InlineData("foo//")]
        [InlineData("C:\\foo//bar\\")]
        [InlineData("C:/foo\\bar/")]
        public void Returns_Empty_Given_Path_Ending_In_Separator(string input)
        {
            var result = FileSafety.GetFileNameSafely(input);

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("//server", "server")]
        [InlineData("\\\\server", "server")]
        public void Returns_LastSegment_Given_UncRoot(string input, string expected)
        {
            // StripPathRoot is not called; the last segment of the localized + split path is returned
            var result = FileSafety.GetFileNameSafely(input);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo", "foo")]
        [InlineData("foo.bar", "foo.bar")]
        [InlineData("foo\\bar", "bar")]
        [InlineData("foo/bar", "bar")]
        [InlineData("C:\\Music\\song.flac", "song.flac")]
        [InlineData("C:/Music/song.flac", "song.flac")]
        [InlineData("Music\\Artist\\song.flac", "song.flac")]
        [InlineData("//server/share/song.flac", "song.flac")]
        [InlineData("\\\\server\\share\\song.flac", "song.flac")]
        public void Returns_Filename_Given_Path_With_File(string input, string expected)
        {
            var result = FileSafety.GetFileNameSafely(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sanitize_True_Replaces_InvalidCharacters()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: true);

            Assert.Equal("file_name.flac", result);
        }

        [Fact]
        public void Sanitize_False_Preserves_InvalidCharacters()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: false);

            Assert.Equal("file:name.flac", result);
        }

        [Fact]
        public void Default_Sanitize_Is_True()
        {
            var resultDefault = FileSafety.GetFileNameSafely("Music\\file:name.flac");
            var resultExplicit = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: true);

            Assert.Equal(resultExplicit, resultDefault);
        }
    }
}
