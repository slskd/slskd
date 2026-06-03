using System;
using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class SanitizePathSegmentTests
    {
        [Theory]
        [InlineData('/')]
        [InlineData('\\')]
        public void Throws_Given_Slash_As_Replacement(char replacement)
        {
            var ex = Record.Exception(() => FileSafety.SanitizePathSegment("segment", replacement));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Contains("invalid in filenames", ex.Message);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void Replaces_Traversal_Segments_With_Replacement(string input)
        {
            var s = FileSafety.SanitizePathSegment(input, '_');

            Assert.Equal("_", s);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        public void Returns_Empty_String_When_Replacement_Is_Period_And_Result_Would_Be_Traversal(string input)
        {
            var s = FileSafety.SanitizePathSegment(input, '.');

            Assert.Equal("", s);
        }

        [Theory]
        [InlineData("a.")]
        [InlineData("..b")]
        [InlineData("c..d")]
        public void Preserves_Periods_In_Segment_If_Not_Traversal(string input)
        {
            var s = FileSafety.SanitizePathSegment(input, '_');

            Assert.Equal(input, s);
        }

        [Fact]
        public void Respects_Replacement_Character()
        {
            var s = FileSafety.SanitizePathSegment("\0\0\0", replacement: '!');

            Assert.Equal("!!!", s);
        }

        [Theory]
        [InlineData("clean", "clean")]
        [InlineData("Artist - Album", "Artist - Album")]
        [InlineData("Ünïcödé", "Ünïcödé")]
        [InlineData("пользователь", "пользователь")]
        public void Linux_ReturnsUnchanged_Given_Safe_Segment(string input, string expected)
        {
            var result = FileSafety.SanitizePathSegment(input, '_', OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("has/slash", "has_slash")]
        [InlineData("has\\backslash", "has_backslash")]
        [InlineData("has/both\\types", "has_both_types")]
        public void Linux_Replaces_Slashes(string input, string expected)
        {
            var result = FileSafety.SanitizePathSegment(input, '_', OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("file:name", "file_name")]
        [InlineData("file*name", "file_name")]
        [InlineData("file?name", "file_name")]
        [InlineData("file\"name", "file_name")]
        [InlineData("file/name", "file_name")]
        [InlineData("file\\name", "file_name")]
        public void Windows_Replaces_InvalidCharacters(string input, string expected)
        {
            var result = FileSafety.SanitizePathSegment(input, '_', OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Returns_Null_Given_Null()
        {
            var result = FileSafety.SanitizePathSegment(null);

            Assert.Null(result);
        }
    }
}