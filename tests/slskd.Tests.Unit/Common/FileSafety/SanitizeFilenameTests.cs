using System;
using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class SanitizeFilenameTests
    {
        [Theory]
        [InlineData('/')]
        [InlineData('\\')]
        public void Throws_Given_Slash_As_Replacement(char replacement)
        {
            var ex = Record.Exception(() => FileSafety.SanitizeFilename("file", replacement));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Contains("invalid in filenames", ex.Message);
        }

        [Fact]
        public void Throws_Given_OsInvalid_Replacement_On_Unix()
        {
            // '\0' is invalid on Unix but not caught by the slash guard
            var ex = Record.Exception(() => FileSafety.SanitizeFilename("file", '\0', OperatingSystem.Linux));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Contains("invalid on", ex.Message);
        }

        [Fact]
        public void Throws_Given_OsInvalid_Replacement_On_Windows()
        {
            // '*' is invalid on Windows but not caught by the slash guard
            var ex = Record.Exception(() => FileSafety.SanitizeFilename("file", '*', OperatingSystem.Windows));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Contains("invalid on", ex.Message);
        }

        [Fact]
        public void NullOs_UsesPlatformDefault_DoesNotThrow()
        {
            var result = FileSafety.SanitizeFilename("safe_file.flac");

            Assert.NotNull(result);
        }

        [Theory]
        [InlineData("clean", "clean")]
        [InlineData("file.flac", "file.flac")]
        [InlineData("My Artist - My Album", "My Artist - My Album")]
        [InlineData("Ünïcödé", "Ünïcödé")]
        [InlineData("пользователь", "пользователь")]
        public void Linux_ReturnsUnchanged_Given_Safe_Filename(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("file\0name", "file_name")]
        [InlineData("\0leading", "_leading")]
        [InlineData("trailing\0", "trailing_")]
        public void Linux_Replaces_NullByte(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("has/slash", "has_slash")]
        [InlineData("has\\backslash", "has_backslash")]
        [InlineData("has/both\\types", "has_both_types")]
        public void Linux_Replaces_Slashes(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/home/user/foo", "_home_user_foo")]
        [InlineData("//server/share", "__server_share")]
        [InlineData("C:\\Windows\\file.ext", "C:_Windows_file.ext")]
        [InlineData("\\\\server\\share", "__server_share")]
        public void Linux_Sanitizes_Full_Paths(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Linux);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Linux_Uses_Custom_Replacement()
        {
            var result = FileSafety.SanitizeFilename("has/slash", '-', OperatingSystem.Linux);

            Assert.Equal("has-slash", result);
        }

        [Theory]
        [InlineData("file\"name", "file_name")]
        [InlineData("file<name>", "file_name_")]
        [InlineData("file|name", "file_name")]
        [InlineData("file:name", "file_name")]
        [InlineData("file*name", "file_name")]
        [InlineData("file?name", "file_name")]
        [InlineData("file\\name", "file_name")]
        [InlineData("file/name", "file_name")]
        public void Windows_Replaces_InvalidCharacters(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/home/user/foo", "_home_user_foo")]
        [InlineData("//server/share", "__server_share")]
        [InlineData("C:\\Windows\\file.ext", "C__Windows_file.ext")]
        [InlineData("\\\\server\\share", "__server_share")]
        public void Windows_Sanitizes_Full_Paths(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Windows_Replaces_ControlCharacters()
        {
            var input = "file\x01\x1fname";
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Windows);

            Assert.Equal("file__name", result);
        }

        [Theory]
        [InlineData("clean", "clean")]
        [InlineData("file.flac", "file.flac")]
        [InlineData("My Artist", "My Artist")]
        public void Windows_ReturnsUnchanged_Given_Safe_Filename(string input, string expected)
        {
            var result = FileSafety.SanitizeFilename(input, '_', OperatingSystem.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Linux_Accepts_Period_As_Replacement()
        {
            var result = FileSafety.SanitizeFilename("file\0name", '.', OperatingSystem.Linux);

            Assert.Equal("file.name", result);
        }

        [Fact]
        public void Windows_Accepts_Period_As_Replacement()
        {
            var result = FileSafety.SanitizeFilename("file*name", '.', OperatingSystem.Windows);

            Assert.Equal("file.name", result);
        }

        [Fact]
        public void Returns_Null_Given_Null()
        {
            var result = FileSafety.SanitizeFilename(null);

            Assert.Null(result);
        }

        [Fact]
        public void Returns_EmptyString_Given_EmptyString()
        {
            var result = FileSafety.SanitizeFilename(string.Empty);

            Assert.Equal(string.Empty, result);
        }
    }
}