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
        // Drive-letter roots
        [InlineData("C:\\Music\\Artist", "Music\\Artist")]
        [InlineData("C:/Music/Artist", "Music\\Artist")]
        [InlineData("D:\\path\\to\\file", "path\\to\\file")]
        [InlineData("Z:\\", "")]
        // UNC roots
        [InlineData("\\\\server\\share\\Music", "share\\Music")]
        [InlineData("\\\\server\\Music", "Music")]
        [InlineData("\\\\192.168.1.1\\share\\folder", "share\\folder")]
        // Soulseek Qt prefix
        [InlineData("@@abcde\\Music\\Artist", "Music\\Artist")]
        [InlineData("@@abcdefgh\\Music", "Music")]
        [InlineData("@@abcde/Music/Artist", "Music\\Artist")]
        // Drive-relative (single leading slash) — not matched by StripPathRoot regexes;
        // the leading slash is removed by empty-segment filtering during Split/join
        [InlineData("\\Music", "Music")]
        [InlineData("/Music", "Music")]
        public void Windows_Strips_Root_When_StripRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: true, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        // Drive-letter roots
        [InlineData("C:\\Music\\Artist", "Music/Artist")]
        [InlineData("C:/Music/Artist", "Music/Artist")]
        [InlineData("D:\\path\\to\\file", "path/to/file")]
        [InlineData("Z:\\", "")]
        // UNC roots
        [InlineData("//server/share/Music", "share/Music")]
        [InlineData("//server/Music", "Music")]
        [InlineData("//192.168.1.1/share/folder", "share/folder")]
        // Soulseek Qt prefix
        [InlineData("@@abcde/Music/Artist", "Music/Artist")]
        [InlineData("@@abcdefgh/Music", "Music")]
        [InlineData("@@abcde\\Music\\Artist", "Music/Artist")]
        // Unix absolute single-slash — not matched by StripPathRoot regexes;
        // the leading slash is removed by empty-segment filtering during Split/join
        [InlineData("/Music", "Music")]
        [InlineData("/Music/Artist", "Music/Artist")]
        [InlineData("/absolute/path/to/file", "absolute/path/to/file")]
        public void Linux_Strips_Root_When_StripRoot_True(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: true, os: OperatingSystem.Linux);

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

        [Fact]
        public void Returns_EmptyString_Given_EmptyString()
        {
            var result = FileSafety.SanitizePath(string.Empty);

            Assert.Equal(string.Empty, result);
        }

        [Theory]
        // Drive-letter roots — ':' is valid on Linux so it is preserved
        [InlineData("C:\\Music\\Artist", "C:/Music/Artist")]
        [InlineData("C:/Music/Artist", "C:/Music/Artist")]
        [InlineData("D:\\path\\to\\file", "D:/path/to/file")]
        [InlineData("Z:\\", "Z:")]
        // UNC roots
        [InlineData("//server/share/Music", "//server/share/Music")]
        [InlineData("//server/Music", "//server/Music")]
        [InlineData("\\\\server\\share\\Music", "//server/share/Music")]
        [InlineData("\\\\192.168.1.1\\share\\folder", "//192.168.1.1/share/folder")]
        // Soulseek Qt prefix
        [InlineData("@@abcde/Music/Artist", "@@abcde/Music/Artist")]
        [InlineData("@@abcdefgh/Music", "@@abcdefgh/Music")]
        [InlineData("@@abcde\\Music\\Artist", "@@abcde/Music/Artist")]
        // Unix absolute single-slash prefix
        [InlineData("/Music", "/Music")]
        [InlineData("/Music/Artist", "/Music/Artist")]
        [InlineData("/absolute/path/to/file", "/absolute/path/to/file")]
        public void Linux_Retains_Root_When_StripRoot_False(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        // Drive-letter roots — ':' is invalid on Windows, sanitized to '_'
        [InlineData("C:\\Music\\Artist", "C_\\Music\\Artist")]
        [InlineData("C:/Music/Artist", "C_\\Music\\Artist")]
        [InlineData("D:\\path\\to\\file", "D_\\path\\to\\file")]
        [InlineData("Z:\\", "Z_")]
        // UNC roots — no invalid characters, preserved as-is
        [InlineData("\\\\server\\share\\Music", "\\\\server\\share\\Music")]
        [InlineData("\\\\server\\Music", "\\\\server\\Music")]
        [InlineData("//server/share/Music", "\\\\server\\share\\Music")]
        [InlineData("\\\\192.168.1.1\\share\\folder", "\\\\192.168.1.1\\share\\folder")]
        // Soulseek Qt prefix
        [InlineData("@@abcde\\Music\\Artist", "@@abcde\\Music\\Artist")]
        [InlineData("@@abcdefgh\\Music", "@@abcdefgh\\Music")]
        [InlineData("@@abcde/Music/Artist", "@@abcde\\Music\\Artist")]
        // Drive-relative (single leading slash) — leading slash is retained
        [InlineData("\\absolute\\Music", "\\absolute\\Music")]
        [InlineData("/absolute/Music", "\\absolute\\Music")]
        public void Windows_Retains_Root_When_StripRoot_False(string input, string expected)
        {
            var result = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/bar")]
        [InlineData("relative/path/to/file")]
        [InlineData("sub\\dir")]
        public void StripRoot_IsNoOp_On_RelativePath(string input)
        {
            var withStrip = FileSafety.SanitizePath(input, stripRoot: true, os: OperatingSystem.Linux);
            var withoutStrip = FileSafety.SanitizePath(input, stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(withoutStrip, withStrip);
        }

        [Fact]
        public void StripRoot_Defaults_To_False()
        {
            var withDefault = FileSafety.SanitizePath("C:\\Music\\Artist", os: OperatingSystem.Linux);
            var withExplicitFalse = FileSafety.SanitizePath("C:\\Music\\Artist", stripRoot: false, os: OperatingSystem.Linux);

            Assert.Equal(withExplicitFalse, withDefault);
        }
    }
}