using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

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
            var result = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: true, os: OSPlatform.Windows);

            Assert.Equal("file_name.flac", result);
        }

        [Fact]
        public void Sanitize_True_Replaces_InvalidCharacters_On_Linux()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file\0name.flac", sanitize: true, os: OSPlatform.Linux);

            Assert.Equal("file_name.flac", result);
        }

        [Fact]
        public void Sanitize_False_Preserves_InvalidCharacters_On_Windows()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: false, os: OSPlatform.Windows);

            Assert.Equal("file:name.flac", result);
        }

        [Fact]
        public void Sanitize_False_Preserves_InvalidCharacters_On_Linux()
        {
            var result = FileSafety.GetFileNameSafely("Music\\file\0name.flac", sanitize: false, os: OSPlatform.Linux);

            Assert.Equal("file\0name.flac", result);
        }

        [Fact]
        public void Default_Sanitize_Is_True()
        {
            var resultDefault = FileSafety.GetFileNameSafely("Music\\file:name.flac", os: OSPlatform.Windows);
            var resultExplicit = FileSafety.GetFileNameSafely("Music\\file:name.flac", sanitize: true, os: OSPlatform.Windows);

            Assert.Equal(resultExplicit, resultDefault);
        }
    }
}
