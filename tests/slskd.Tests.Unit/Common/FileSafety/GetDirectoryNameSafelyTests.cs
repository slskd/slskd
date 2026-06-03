using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class GetDirectoryNameSafelyTests
    {
        [Fact]
        public void Returns_Null_Given_Null()
        {
            var result = FileSafety.GetDirectoryNameSafely(null);

            Assert.Null(result);
        }

        [Fact]
        public void Returns_Null_Given_Empty()
        {
            var result = FileSafety.GetDirectoryNameSafely("");

            Assert.Null(result);
        }

        [Theory]
        [InlineData("\\")]
        [InlineData("\\\\")]
        [InlineData("\\\\\\")]
        [InlineData("/")]
        [InlineData("//")]
        [InlineData("///")]
        [InlineData("/\\/\\\\//////\\")]
        public void Returns_Null_Given_Only_Slashes(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo.bar")]
        public void Returns_Null_Given_Bare_File(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("C:")]
        [InlineData("C:\\")]
        [InlineData("C:/")]
        [InlineData("/")]
        [InlineData("\\")]
        [InlineData("//server")]
        [InlineData("\\\\server")]
        public void Returns_Null_Given_Root(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("C:\\file.txt")]
        [InlineData("C:/file.txt")]
        [InlineData("//server/file.txt")]
        [InlineData("\\\\server\\file.txt")]
        [InlineData("@@abcde/file.txt")]
        [InlineData("@@abcde\\file.txt")]
        public void Returns_Null_When_File_Is_Directly_In_Root_With_RetainRoot_False_Linux(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: false, os: OSPlatform.Linux);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("C:\\file.txt")]
        [InlineData("C:/file.txt")]
        [InlineData("//server/file.txt")]
        [InlineData("\\\\server\\file.txt")]
        [InlineData("@@abcde/file.txt")]
        [InlineData("@@abcde\\file.txt")]
        public void Returns_Null_When_File_Is_Directly_In_Root_With_RetainRoot_False_Windows(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: false, os: OSPlatform.Windows);

            Assert.Null(result);
        }

        [Theory]
        [InlineData("C:\\file.txt", "C:")]
        [InlineData("C:/file.txt", "C:")]
        [InlineData("//server/file.txt", "//server")]
        [InlineData("\\\\server\\file.txt", "//server")]
        [InlineData("@@abcde/file.txt", "@@abcde")]
        [InlineData("@@abcde\\file.txt", "@@abcde")]
        public void Returns_Root_When_File_Is_Directly_In_Root_With_RetainRoot_True_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\file.txt", "C_")]
        [InlineData("C:/file.txt", "C_")]
        [InlineData("//server/file.txt", "\\\\server")]
        [InlineData("\\\\server\\file.txt", "\\\\server")]
        [InlineData("@@abcde/file.txt", "@@abcde")]
        [InlineData("@@abcde\\file.txt", "@@abcde")]
        public void Returns_Sanitized_Root_When_File_Is_Directly_In_Root_With_RetainRoot_True_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Sanitizes_Path_When_Sanitize_True()
        {
            var result = FileSafety.GetDirectoryNameSafely("foo/b\0ar/baz", sanitize: true);

            Assert.DoesNotContain('\0', result);
        }

        [Fact]
        public void Does_Not_Sanitize_Path_When_Sanitize_False()
        {
            var result = FileSafety.GetDirectoryNameSafely("foo/b\0ar/baz", sanitize: false);

            Assert.Contains('\0', result);
        }

        [Fact]
        public void Sanitize_Defaults_To_True()
        {
            var result = FileSafety.GetDirectoryNameSafely("foo/b\0ar/baz");

            Assert.DoesNotContain('\0', result);
        }

        [Theory]
        [InlineData("\\foo\\\\bar\\\\baz", "foo\\bar")]
        [InlineData("/foo//bar//baz", "foo\\bar")]
        public void Removes_Empty_Segments_And_Leading_Slashes_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\foo\\\\bar\\\\baz", "foo/bar")]
        [InlineData("/foo//bar//baz", "foo/bar")]
        public void Removes_Empty_Segments_And_Leading_Slashes_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("fo\0o\\bar", "fo_o")]
        [InlineData("foo/bar", "foo")]
        [InlineData("C:\\Music\\song.flac", "Music")]
        [InlineData("C:/Music/song.flac", "Music")]
        [InlineData("C:\\M\0usic\\Artist\\song.flac", "M_usic/Artist")]
        [InlineData("C:\\foo.txt", null)]
        [InlineData("//server/share/song.flac", "share")]
        [InlineData("\\\\server\\share\\song.flac", "share")]
        [InlineData("/abs\0olute/path", "abs_olute")]
        [InlineData("\\abs\0olute\\path", "abs_olute")]
        [InlineData("@@abcde\\foo\\bar", "foo")]
        public void Returns_Unrooted_Sanitized_Directory_Given_Path_With_File_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("fo\0o\\bar", "fo_o")]
        [InlineData("foo/bar", "foo")]
        [InlineData("C:\\Music\\song.flac", "Music")]
        [InlineData("C:/Music/song.flac", "Music")]
        [InlineData("C:\\M\0usic\\Artist\\song.flac", "M_usic\\Artist")]
        [InlineData("C:\\foo.txt", null)]
        [InlineData("//server/share/song.flac", "share")]
        [InlineData("\\\\server\\share\\song.flac", "share")]
        [InlineData("/abs\0olute/path", "abs_olute")]
        [InlineData("\\abs\0olute\\path", "abs_olute")]
        [InlineData("@@abcde\\foo\\bar", "foo")]
        public void Returns_Unrooted_Sanitized_Directory_Given_Path_With_File_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo\\bar", "foo")]
        [InlineData("foo/bar", "foo")]
        [InlineData("C:\\Music\\song.flac", "C_\\Music")]
        [InlineData("C:/Music/song.flac", "C_\\Music")]
        [InlineData("C:\\Music\\Artist\\song.flac", "C_\\Music\\Artist")]
        [InlineData("//server/share/song.flac", "\\\\server\\share")]
        [InlineData("\\\\server\\share\\song.flac", "\\\\server\\share")]
        [InlineData("/absolute/path", "\\absolute")]
        [InlineData("\\absolute\\path", "\\absolute")]
        [InlineData("@@abcde\\foo\\bar", "@@abcde\\foo")]
        public void Retains_Path_Root_When_RetainRoot_True_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo\\bar", "foo")]
        [InlineData("foo/bar", "foo")]
        [InlineData("C:\\Music\\song.flac", "C:/Music")]
        [InlineData("C:/Music/song.flac", "C:/Music")]
        [InlineData("C:\\Music\\Artist\\song.flac", "C:/Music/Artist")]
        [InlineData("//server/share/song.flac", "//server/share")]
        [InlineData("\\\\server\\share\\song.flac", "//server/share")]
        [InlineData("/absolute/path", "/absolute")]
        [InlineData("\\absolute\\path", "/absolute")]
        [InlineData("@@abcde\\foo\\bar", "@@abcde/foo")]
        public void Retains_Path_Root_When_RetainRoot_True_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Mu\0sic\\song.flac", "C:\\Mu\0sic")]
        [InlineData("C:/Music/song.flac", "C:\\Music")]
        [InlineData("//server/share/song.flac", "\\\\server\\share")]
        [InlineData("\\\\serv?er\\share\\song.flac", "\\\\serv?er\\share")]
        [InlineData("/abs\0olute/path", "\\abs\0olute")]
        [InlineData("\\abs\0olute\\path", "\\abs\0olute")]
        [InlineData("@@abcde\\fo:o\\bar", "@@abcde\\fo:o")]
        [InlineData("@@abcde/fo*o/bar", "@@abcde\\fo*o")]
        public void Returns_Rooted_Unsanitized_If_Directed_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: false, retainRoot: true, os: OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\Mu\0sic\\song.flac", "C:/Mu\0sic")]
        [InlineData("C:/Music/song.flac", "C:/Music")]
        [InlineData("//server/share/song.flac", "//server/share")]
        [InlineData("\\\\serv?er\\share\\song.flac", "//serv?er/share")]
        [InlineData("/abs\0olute/path", "/abs\0olute")]
        [InlineData("\\abs\0olute\\path", "/abs\0olute")]
        [InlineData("@@abcde\\fo:o\\bar", "@@abcde/fo:o")]
        [InlineData("@@abcde/fo*o/bar", "@@abcde/fo*o")]
        public void Returns_Rooted_Unsanitized_If_Directed_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: false, retainRoot: true, os: OSPlatform.Linux);

            Assert.Equal(expected, result);
        }
    }
}