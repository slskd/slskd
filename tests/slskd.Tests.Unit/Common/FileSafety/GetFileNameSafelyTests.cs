namespace slskd.Tests.Unit.Common;

using Xunit;

public partial class FileSafetyTests
{
    public class GetFileNameSafelyTests
    {
        [Fact]
        public void Returns_Null_Given_Null()
        {
            var result = FileSafety.GetFileNameSafely(null);

            Assert.Null(result);
        }

        [Fact]
        public void Returns_Null_Given_Empty()
        {
            var result = FileSafety.GetFileNameSafely("");

            Assert.Null(result);
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
        public void Returns_Null_Given_Path_Ending_In_Separator(string input)
        {
            var result = FileSafety.GetFileNameSafely(input);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("//server")]
        [InlineData("\\\\server")]
        [InlineData("C:")]
        [InlineData("@@abcde")]
        public void Returns_Null_Given_Just_Rooted_Path(string input)
        {
            var result = FileSafety.GetFileNameSafely(input);

            Assert.Null(result);
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
        public void Sanitize_True_Replaces_InvalidCharacters_On_Windows()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: true, os: OperatingSystem.Windows);

            Assert.Equal("file_name.flac", result);
        }

        [Fact]
        public void Sanitize_True_Replaces_InvalidCharacters_On_Linux()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file\0name.flac", sanitize: true, os: OperatingSystem.Linux);

            Assert.Equal("file_name.flac", result);
        }

        [Fact]
        public void Sanitize_False_Preserves_InvalidCharacters_On_Windows()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: false, os: OperatingSystem.Windows);

            Assert.Equal("file:name.flac", result);
        }

        [Fact]
        public void Sanitize_False_Preserves_InvalidCharacters_On_Linux()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file\0name.flac", sanitize: false, os: OperatingSystem.Linux);

            Assert.Equal("file\0name.flac", result);
        }

        [Fact]
        public void Default_Sanitize_Is_True()
        {
            var resultDefault = FileSafety.GetFileNameSafely("Music\\file:name.flac", os: OperatingSystem.Windows);
            var resultExplicit = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: true, os: OperatingSystem.Windows);

            Assert.Equal(resultExplicit, resultDefault);
        }

        [Fact]
        public void NullOs_UsesPlatformDefault_DoesNotThrow()
        {
            var result = FileSafety.GetFileNameSafely("Music/song.flac");

            Assert.Equal("song.flac", result);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void Sanitize_True_Replaces_BareTraversalFilename(string input)
        {
            var result = FileSafety.GetFileNameSafely(input, sanitize: true);

            Assert.Equal("_", result);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void Sanitize_False_Preserves_BareTraversalFilename(string input)
        {
            var result = FileSafety.GetFileNameSafely(input, sanitize: false);

            Assert.Equal(input, result);
        }

        [Theory]
        [InlineData("foo/.")]
        [InlineData("foo/..")]
        [InlineData("foo\\..")]
        public void Sanitize_True_Replaces_Traversal_As_Last_Path_Segment(string input)
        {
            var result = FileSafety.GetFileNameSafely(input, sanitize: true);

            Assert.Equal("_", result);
        }

        [Theory]
        [InlineData("foo/.", ".")]
        [InlineData("foo/..", "..")]
        [InlineData("foo\\..", "..")]
        public void Sanitize_False_Preserves_Traversal_As_Last_Path_Segment(string input, string expected)
        {
            var result = FileSafety.GetFileNameSafely(input, sanitize: false);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo//bar.flac", "bar.flac")]
        [InlineData("foo\\\\bar.flac", "bar.flac")]
        public void Returns_Filename_Given_Double_Separator(string input, string expected)
        {
            var result = FileSafety.GetFileNameSafely(input);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde/song.flac", "song.flac")]
        [InlineData("@@abcde\\song.flac", "song.flac")]
        [InlineData("@@abcdefgh/Music/song.flac", "song.flac")]
        public void Returns_Filename_Given_SoulseekQt_Prefixed_Path_Linux(string input, string expected)
        {
            var result = FileSafety.GetFileNameSafely(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcde\\song.flac", "song.flac")]
        [InlineData("@@abcde/song.flac", "song.flac")]
        [InlineData("@@abcdefgh\\Music\\song.flac", "song.flac")]
        public void Returns_Filename_Given_SoulseekQt_Prefixed_Path_Windows(string input, string expected)
        {
            var result = FileSafety.GetFileNameSafely(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }
    }
}