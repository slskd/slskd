using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class GetDirectoryNameSafelyTests
    {
        [Fact]
        public void Throws_Given_Null()
        {
            var ex = Record.Exception(() => FileSafety.GetDirectoryNameSafely(null));

            Assert.NotNull(ex);
        }

        [Fact]
        public void Returns_Empty_Given_Empty()
        {
            var result = FileSafety.GetDirectoryNameSafely("");

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo.bar")]
        public void Returns_Empty_Given_Bare_File(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input);

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("C:")]         // bare drive letter — StripPathRoot removes it, nothing left
        [InlineData("C:\\")]       // drive root with trailing backslash
        [InlineData("C:/")]        // drive root with trailing forward slash
        [InlineData("/")]          // Unix-style root — no double-slash, not stripped; splits to ["", ""]
        [InlineData("\\")]         // single backslash root
        [InlineData("//server")]   // UNC root — StripPathRoot removes server, nothing left
        [InlineData("\\\\server")] // UNC root, backslashes
        public void Returns_Empty_Given_Root(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input);

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("foo\\bar", "foo")]
        [InlineData("foo/bar", "foo")]
        [InlineData("C:\\Music\\song.flac", "Music")]
        [InlineData("C:/Music/song.flac", "Music")]
        [InlineData("C:\\Music\\Artist\\song.flac", "Music\\Artist")]
        [InlineData("//server/share/song.flac", "share")]
        [InlineData("\\\\server\\share\\song.flac", "share")]
        public void Returns_Directory_Given_Path_With_File(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sanitize_True_Replaces_InvalidCharacters_In_Segments()
        {
            var result = FileSafety.GetDirectoryNameSafely("Music*\\Artist\\song.flac", sanitize: true);

            Assert.Equal("Music_\\Artist", result);
        }

        [Fact]
        public void Sanitize_False_Preserves_InvalidCharacters_In_Segments()
        {
            var result = FileSafety.GetDirectoryNameSafely("Music*\\Artist\\song.flac", sanitize: false);

            Assert.Equal("Music*\\Artist", result);
        }

        [Fact]
        public void Default_Sanitize_Is_True()
        {
            var resultDefault = FileSafety.GetDirectoryNameSafely("Music*\\Artist\\song.flac");
            var resultExplicit = FileSafety.GetDirectoryNameSafely("Music*\\Artist\\song.flac", sanitize: true);

            Assert.Equal(resultExplicit, resultDefault);
        }
    }
}
