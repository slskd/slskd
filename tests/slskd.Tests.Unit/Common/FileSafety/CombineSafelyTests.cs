using System;
using System.IO;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class CombineSafelyTests
    {
        public static string Base = System.OperatingSystem.IsWindows() ? "C:\\base" : "/base";
        public static string ExpectedStartsWith = Base + Path.DirectorySeparatorChar;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Throws_ArgumentNullException_Given_NullOrWhiteSpaceRoot(string root)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(root));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
        }

        [Theory]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("./foo")]
        [InlineData("../bar")]
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
        public void Throws_Given_Null_Segment()
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, "foo", null, "bar"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
        }

        [Fact]
        public void Throws_Given_Null_Segments()
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, segments: null));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
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
        [InlineData("foo\\bar\\baz\\qux\\deeply\\nested")]
        [InlineData("foo/bar/baz/qux/deeply/nested")]
        [InlineData("foo/bar\\baz/qux\\deeply/nested")]
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
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, OperatingSystem.Linux, segment));

            Assert.NotNull(ex);
            Assert.Contains("Absolute", ex.Message);
        }

        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("\\\\server\\share")]
        public void Throws_ArgumentException_Given_AbsolutePath_On_Windows(string segment)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, OperatingSystem.Windows, segment));

            Assert.NotNull(ex);
            Assert.Contains("Absolute", ex.Message);
        }

        [Theory]
        [InlineData("foo:bar")]
        [InlineData("C:relative")]
        [InlineData("sub/foo:bar")]
        [InlineData("sub/foo/bar:qux")]
        public void Throws_ArgumentException_Given_Colon_In_Segment_On_Windows(string segment)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, OperatingSystem.Windows, segment));

            Assert.NotNull(ex);
            Assert.Contains("Colon", ex.Message);
        }

        [Theory]
        [InlineData("foo:bar")]
        [InlineData("sub/foo:bar")]
        [InlineData("sub/foo/bar:qux")]
        public void Accepts_Colon_In_Segment_On_Linux(string segment)
        {
            var result = Record.Exception(() => FileSafety.CombineSafely(Base, OperatingSystem.Linux, segment));

            Assert.Null(result);
        }

        [Theory]
        [InlineData("C:relative")]
        public void Accepts_Colon_In_Segment_On_Linux_When_Running_On_Windows(string segment)
        {
            var result = FileSafety.CombineSafely(Base, OperatingSystem.Linux, segment);

            Assert.Equal($"{Base}/C:relative", result);
        }

        [Theory]
        [InlineData("/Music")]
        [InlineData("\\Music")]
        public void Throws_ArgumentException_Given_Leading_Slash_In_Segment_On_Windows(string segment)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(Base, OperatingSystem.Windows, segment));

            Assert.NotNull(ex);
            Assert.Contains("Drive-relative", ex.Message);
        }

        [Theory]
        [InlineData("\\Music")]
        public void Accepts_Leading_Backslash_In_Segment_On_Linux(string segment)
        {
            var result = FileSafety.CombineSafely(Base, OperatingSystem.Linux, segment);

            Assert.Equal($"{Base}/\\Music", result);
        }

        [Fact]
        public void Returns_Root_Given_Single_Empty_Segment()
        {
            var result = FileSafety.CombineSafely(Base, "");

            Assert.Equal(Base, result);
        }

        [Fact]
        public void Returns_Root_Given_All_Empty_Segments()
        {
            var result = FileSafety.CombineSafely(Base, "", "");

            Assert.Equal(Base, result);
        }

        [Fact]
        public void Handles_Root_With_Trailing_Separator()
        {
            var rootWithTrailing = Base + Path.DirectorySeparatorChar;
            var result = FileSafety.CombineSafely(rootWithTrailing, "foo");

            Assert.StartsWith(ExpectedStartsWith, result);
            Assert.EndsWith("foo", result);
        }

        [Theory]
        [InlineData("./foo")]
        [InlineData("../bar")]
        [InlineData("foo/../bar")]
        public void Throws_ArgumentException_Given_TraversingRoot_With_Os_Override(string root)
        {
            var ex = Record.Exception(() => FileSafety.CombineSafely(root, OperatingSystem.Linux, "segment"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
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