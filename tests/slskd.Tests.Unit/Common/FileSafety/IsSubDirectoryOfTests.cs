using System;
using System.IO;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class IsSubDirectoryOfTests
    {
        // IsSubDirectoryOf(subdirectory, root) forwards to IsRootDirectoryOf(root, subdirectory),
        // so `root` (the second parameter here) lands on IsRootDirectoryOf's first parameter and
        // is the one validated with ArgumentNullException.ThrowIfNullOrWhiteSpace.
        [Fact]
        public void Root_Null_Throws_ArgumentNullException()
        {
            string root = null;

            var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf("C:\\foo", root));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Root_Empty_Or_Whitespace_Throws_ArgumentException(string root)
        {
            var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf("C:\\foo", root));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
        }

        // subdirectory (the first parameter here) is never null-checked directly; it only fails
        // the "must be absolute" check, so null/empty/whitespace all throw plain ArgumentException.
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Subdirectory_Null_Empty_Or_Whitespace_Throws_ArgumentException_Not_ArgumentNullException(string subdirectory)
        {
            var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf(subdirectory, "C:\\foo", OperatingSystem.Windows));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
        }

        public class Windows
        {
            [Theory]
            [InlineData("foo")]
            [InlineData("\\foo")]
            [InlineData("C:")]
            [InlineData("C:foo")]
            public void Relative_Root_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf("C:\\foo\\bar", root, OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("foo")]
            [InlineData("\\foo")]
            [InlineData("C:")]
            [InlineData("C:foo")]
            public void Relative_Subdirectory_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf(subdirectory, "C:\\foo", OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("C:\\foo\\..")]
            [InlineData("C:\\foo\\..\\bar")]
            [InlineData("C:\\foo\\.")]
            public void Root_With_Traversal_Segments_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf("C:\\foo\\bar", root, OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("C:\\foo\\..\\bar")]
            [InlineData("C:\\foo\\.")]
            public void Subdirectory_With_Traversal_Segments_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf(subdirectory, "C:\\foo", OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("C:\\foo", "C:\\foo")]
            [InlineData("C:\\FOO", "C:\\foo")]
            [InlineData("C:\\foo", "C:\\FOO")]
            public void Equal_Paths_Are_A_Case_Insensitive_Match(string subdirectory, string root)
            {
                Assert.True(FileSafety.IsSubDirectoryOf(subdirectory, root, OperatingSystem.Windows));
            }

            [Theory]
            [InlineData("C:\\foo\\bar", "C:\\foo", '\\')]
            [InlineData("C:\\foo/bar", "C:\\foo", '/')]
            [InlineData("\\\\foo\\bar\\baz", "\\\\foo\\bar", '\\')]
            [InlineData("\\\\foo\\bar/baz", "\\\\foo\\bar", '/')]
            [InlineData("C:\\foo\\bar", "C:\\foo\\", '\\')]
            public void Match_Depends_On_Native_Separator_At_The_Boundary(string subdirectory, string root, char boundarySeparator)
            {
                var expected = boundarySeparator == Path.DirectorySeparatorChar;

                Assert.Equal(expected, FileSafety.IsSubDirectoryOf(subdirectory, root, OperatingSystem.Windows));
            }

            [Theory]
            [InlineData("C:\\foobar", "C:\\foo")]
            [InlineData("C:\\foo", "C:\\foo\\bar")]
            [InlineData("C:\\bar", "C:\\foo")]
            [InlineData("C:\\foo\\baz", "C:\\foo\\bar")]
            public void Not_A_Child_Of_Root_Returns_False(string subdirectory, string root)
            {
                Assert.False(FileSafety.IsSubDirectoryOf(subdirectory, root, OperatingSystem.Windows));
            }
        }

        public class Linux
        {
            [Theory]
            [InlineData("foo")]
            [InlineData("C:\\foo")]
            public void Relative_Root_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf("/foo/bar", root, OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("foo")]
            [InlineData("C:\\foo")]
            public void Relative_Subdirectory_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf(subdirectory, "/foo", OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("/foo/../bar")]
            [InlineData("/foo/.")]
            public void Root_With_Traversal_Segments_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf("/foo/bar", root, OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("/foo/../bar")]
            [InlineData("/foo/.")]
            public void Subdirectory_With_Traversal_Segments_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsSubDirectoryOf(subdirectory, "/foo", OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Fact]
            public void Equal_Paths_Match()
            {
                Assert.True(FileSafety.IsSubDirectoryOf("/foo", "/foo", OperatingSystem.Linux));
            }

            [Fact]
            public void Equal_Paths_Different_Case_Do_Not_Match()
            {
                Assert.False(FileSafety.IsSubDirectoryOf("/FOO", "/foo", OperatingSystem.Linux));
            }

            [Theory]
            [InlineData("/foo/bar", "/foo", '/')]
            [InlineData("/foo\\bar", "/foo", '\\')]
            [InlineData("//foo/bar/baz", "//foo/bar", '/')]
            [InlineData("//foo/bar\\baz", "//foo/bar", '\\')]
            public void Match_Depends_On_Native_Separator_At_The_Boundary(string subdirectory, string root, char boundarySeparator)
            {
                var expected = boundarySeparator == Path.DirectorySeparatorChar;

                Assert.Equal(expected, FileSafety.IsSubDirectoryOf(subdirectory, root, OperatingSystem.Linux));
            }

            [Theory]
            [InlineData("/foobar", "/foo")]
            [InlineData("/foo", "/foo/bar")]
            [InlineData("/bar", "/foo")]
            [InlineData("/foo/baz", "/foo/bar")]
            public void Not_A_Child_Of_Root_Returns_False(string subdirectory, string root)
            {
                Assert.False(FileSafety.IsSubDirectoryOf(subdirectory, root, OperatingSystem.Linux));
            }
        }
    }
}
