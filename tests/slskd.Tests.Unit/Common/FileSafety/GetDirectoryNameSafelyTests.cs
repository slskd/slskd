namespace slskd.Tests.Unit.Common;

using Xunit;

public partial class FileSafetyTests
{
    public class GetDirectoryNameSafelyTests
    {
        [Fact]
        public void NullOs_UsesPlatformDefault_DoesNotThrow()
        {
            var result = FileSafety.GetDirectoryNameSafely("foo/bar");

            Assert.Equal("foo", result);
        }

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

        [Fact]
        public void Returns_Null_Given_Whitespace_Only()
        {
            var result = FileSafety.GetDirectoryNameSafely("   ");

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
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: false, os: OperatingSystem.Linux);

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
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: false, os: OperatingSystem.Windows);

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
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OperatingSystem.Linux);

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
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OperatingSystem.Windows);

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
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\\foo\\\\bar\\\\baz", "foo/bar")]
        [InlineData("/foo//bar//baz", "foo/bar")]
        public void Removes_Empty_Segments_And_Leading_Slashes_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/../bar/file.txt", "foo/_/bar")]
        [InlineData("../bar/file.txt", "_/bar")]
        [InlineData("a/./b/file.txt", "a/_/b")]
        public void Sanitizes_Traversal_Segments_In_Directory_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo\\..\\bar\\file.txt", "foo\\_\\bar")]
        [InlineData("..\\bar\\file.txt", "_\\bar")]
        [InlineData("a\\.\\b\\file.txt", "a\\_\\b")]
        public void Sanitizes_Traversal_Segments_In_Directory_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Artist/Album/", "Artist")]
        [InlineData("Artist\\Album\\", "Artist")]
        [InlineData("foo/bar/baz/", "foo/bar")]
        public void Treats_Trailing_Slash_As_Directory_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Artist/Album/", "Artist")]
        [InlineData("Artist\\Album\\", "Artist")]
        [InlineData("foo/bar/baz/", "foo\\bar")]
        public void Treats_Trailing_Slash_As_Directory_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("fo\0o/bar", "fo\0o")]
        [InlineData("C:/Mu\0sic/song.flac", "Mu\0sic")]
        public void Returns_Unrooted_Unsanitized_Directory_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: false, sanitize: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("fo\0o\\bar", "fo\0o")]
        [InlineData("C:\\Mu\0sic\\song.flac", "Mu\0sic")]
        public void Returns_Unrooted_Unsanitized_Directory_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: false, sanitize: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/../bar/file.txt", "foo/../bar")]
        [InlineData("../etc/file.txt", "../etc")]
        public void Preserves_Traversal_Segments_When_Sanitize_False_Linux(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: false, retainRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo\\..\\bar\\file.txt", "foo\\..\\bar")]
        [InlineData("..\\etc\\file.txt", "..\\etc")]
        public void Preserves_Traversal_Segments_When_Sanitize_False_Windows(string input, string expected)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: false, retainRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("@@abcd/foo/bar/file.txt")]
        public void Short_Qt_Prefix_Treated_As_Segment_Linux(string input)
        {
            var result = FileSafety.GetDirectoryNameSafely(input, os: OperatingSystem.Linux);

            Assert.StartsWith("@@abcd", result);
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
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OperatingSystem.Linux);

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
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: true, retainRoot: false, os: OperatingSystem.Windows);

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
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OperatingSystem.Windows);

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
            var result = FileSafety.GetDirectoryNameSafely(input, retainRoot: true, os: OperatingSystem.Linux);

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
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: false, retainRoot: true, os: OperatingSystem.Windows);

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
            var result = FileSafety.GetDirectoryNameSafely(input, sanitize: false, retainRoot: true, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }
    }
}