using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class CombineSafelyTests
    {
        public static string Base = OperatingSystem.IsWindows() ? "C:\\base" : "/base";
        public static string ExpectedStartsWith = Base + Path.DirectorySeparatorChar;

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void Throws_ArgumentException_Given_NullOrWhiteSpaceRoot(string root)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(root));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
        }

        [Theory]
        [InlineData("foo/../bar")]
        [InlineData("foo\\..\\bar")]
        public void Throws_ArgumentException_Given_TraversingRoot(string root)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(root));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
        }

        [Fact]
        public void Returns_Root_Given_No_Segments()
        {
            var result = FileSafety.CombineSafely(Base);

            Assert.StartsWith(ExpectedStartsWith.TrimEnd(Path.DirectorySeparatorChar), result);
        }

        [Fact]
        public void Drops_Empty_Segments()
        {
            var result = FileSafety.CombineSafely(Base, string.Empty, "foo", string.Empty, "bar");

            Assert.StartsWith(ExpectedStartsWith, result);
            Assert.EndsWith(Path.Combine("foo", "bar"), result);
        }

        [Fact]
        public void SingleSegment_ReturnsBaseAndSegment()
        {
            var result = FileSafety.CombineSafely(Base, "subdir");

            Assert.Equal(Path.Combine(Base, "subdir"), result);
        }

        [Fact]
        public void MultipleSegments_CombinesAll()
        {
            var result = FileSafety.CombineSafely(Base, "sub", "dir", "file.flac");

            Assert.Equal(Path.Combine(Base, "sub", "dir", "file.flac"), result);
            Assert.StartsWith(ExpectedStartsWith, result);
        }

        [Theory]
        [InlineData("subdir")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("sub//dir")]
        [InlineData("sub\\\\dir")]
        [InlineData("My Artist")]
        [InlineData("Artist.Name")]
        [InlineData("...dir")]
        [InlineData("..hidden")]
        [InlineData(".hidden")]
        [InlineData("dir/..dir")]
        [InlineData("Ünïcödé")]
        [InlineData("пользователь/Музыка")]
        [InlineData("用户/音乐")]
        [InlineData("ユーザー/音楽")]
        [InlineData("사용자/음악")]
        public void Combines_Safe_Segments(string segment)
        {
            var result = FileSafety.CombineSafely(Base, segment);

            Assert.Equal(Path.Combine(Base, segment), result);
            Assert.StartsWith(ExpectedStartsWith, result);
        }

        [Theory]
        [InlineData("/Music")]
        [InlineData("//server/share")]
        public void Throws_ArgumentException_Given_AbsolutePath_On_Unix(string segment)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, OSPlatform.Linux, segment));

            Assert.NotNull(ex);
            Assert.Contains("Absolute", ex.Message);
        }

        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("\\\\server\\share")]
        public void Throws_ArgumentException_Given_AbsolutePath_On_Windows(string segment)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, OSPlatform.Windows, segment));

            Assert.NotNull(ex);
            Assert.Contains("Absolute", ex.Message);
        }

        [Theory]
        // Bare traversal
        [InlineData("..")]
        [InlineData(".")]
        // Traversal as first component
        [InlineData("../escape")]
        [InlineData("./dir")]
        // Traversal in middle
        [InlineData("sub/../escape")]
        [InlineData("sub/./dir")]
        // Traversal at end
        [InlineData("sub/..")]
        [InlineData("sub/.")]
        // Double traversal
        [InlineData("sub/../../escape")]
        [InlineData("a/b/c/../../..")]
        // Via backslash
        [InlineData("sub\\..")]
        [InlineData("sub\\.")]
        [InlineData("sub\\..\\escape")]
        public void Throws_ArgumentException_Given_Traversing_Segment(string segment)
        {
            var ex = Assert.Throws<ArgumentException>(() => FileSafety.CombineSafely(Base, segment));

            Assert.NotNull(ex);
            Assert.Contains("traversal", ex.Message);
        }
    }
}