using System;
using System.IO;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class IsRootDirectoryOfTests
    {
        [Fact]
        public void Root_Null_Throws_ArgumentNullException()
        {
            string root = null;

            var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf(root, "C:\\foo"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentNullException>(ex);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Root_Empty_Or_Whitespace_Throws_ArgumentException(string root)
        {
            var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf(root, "C:\\foo"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
        }

        public class Windows
        {
            [Theory]
            [InlineData("foo")]
            [InlineData("foo\\bar")]
            [InlineData("foo/bar")]
            [InlineData("\\foo")]
            [InlineData("C:")]
            [InlineData("C:foo")]
            public void Relative_Root_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf(root, "C:\\foo", OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("C:\\foo\\..")]
            [InlineData("C:\\foo\\..\\bar")]
            [InlineData("C:\\foo\\.")]
            [InlineData("C:\\..")]
            [InlineData("\\\\foo\\..")]
            public void Root_With_Traversal_Segments_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf(root, "C:\\foo", OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("foo")]
            [InlineData("foo\\bar")]
            [InlineData("\\foo")]
            [InlineData("C:")]
            [InlineData("C:foo")]
            public void Relative_Subdirectory_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf("C:\\foo", subdirectory, OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("C:\\foo\\..")]
            [InlineData("C:\\foo\\..\\bar")]
            [InlineData("C:\\foo\\.")]
            public void Subdirectory_With_Traversal_Segments_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf("C:\\foo", subdirectory, OperatingSystem.Windows));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("C:\\foo", "C:\\foo")]
            [InlineData("C:\\FOO", "C:\\foo")]
            [InlineData("C:\\foo", "C:\\FOO")]
            [InlineData("\\\\foo\\bar", "\\\\foo\\bar")]
            [InlineData("\\\\FOO\\bar", "\\\\foo\\BAR")]
            public void Equal_Paths_Are_A_Case_Insensitive_Match(string root, string subdirectory)
            {
                Assert.True(FileSafety.IsRootDirectoryOf(root, subdirectory, OperatingSystem.Windows));
            }

            // the character joining root and subdirectory is always the runtime OS separator
            // (Path.DirectorySeparatorChar) regardless of the `os` override passed to force
            // Windows-style absolute-path and case-sensitivity rules, so the expected result
            // is computed from the actual runtime separator rather than hardcoded - this keeps
            // the case correct on whichever OS the suite runs on, while still exercising both
            // slash styles, mixed styles, drive letters, and UNC-style roots via inline data.
            [Theory]
            [InlineData("C:\\foo", "C:\\foo\\bar", '\\')]
            [InlineData("C:\\foo", "C:\\foo/bar", '/')]
            [InlineData("C:/foo", "C:/foo/bar", '/')]
            [InlineData("C:/foo", "C:/foo\\bar", '\\')]
            [InlineData("\\\\foo\\bar", "\\\\foo\\bar\\baz", '\\')]
            [InlineData("\\\\foo\\bar", "\\\\foo\\bar/baz", '/')]
            [InlineData("C:\\foo\\", "C:\\foo\\bar", '\\')]
            [InlineData("C:\\foo/", "C:\\foo\\bar", '\\')]
            public void Subdirectory_Match_Depends_On_Native_Separator_At_The_Boundary(string root, string subdirectory, char boundarySeparator)
            {
                var expected = boundarySeparator == Path.DirectorySeparatorChar;

                Assert.Equal(expected, FileSafety.IsRootDirectoryOf(root, subdirectory, OperatingSystem.Windows));
            }

            [Theory]
            [InlineData("C:\\foo", "C:\\foobar")]
            [InlineData("C:\\foo\\bar", "C:\\foo")]
            [InlineData("C:\\foo", "C:\\bar")]
            [InlineData("C:\\foo\\bar", "C:\\foo\\baz")]
            [InlineData("C:\\foo", "D:\\foo\\bar")]
            [InlineData("\\\\foo\\bar", "\\\\foo\\barbaz")]
            public void Non_Subdirectory_Returns_False(string root, string subdirectory)
            {
                Assert.False(FileSafety.IsRootDirectoryOf(root, subdirectory, OperatingSystem.Windows));
            }

            [Fact]
            public void StartsWith_Check_Is_Case_Insensitive()
            {
                var subdirectory = "C:\\FOO" + Path.DirectorySeparatorChar + "bar";

                Assert.True(FileSafety.IsRootDirectoryOf("C:\\foo", subdirectory, OperatingSystem.Windows));
            }
        }

        public class Linux
        {
            [Theory]
            [InlineData("foo")]
            [InlineData("foo/bar")]
            [InlineData("foo\\bar")]
            [InlineData("C:\\foo")]
            [InlineData("\\\\foo\\bar")]
            public void Relative_Root_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf(root, "/foo", OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("/foo/..")]
            [InlineData("/foo/../bar")]
            [InlineData("/foo/.")]
            [InlineData("/..")]
            [InlineData("//foo/..")]
            public void Root_With_Traversal_Segments_Throws_ArgumentException(string root)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf(root, "/foo", OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("foo")]
            [InlineData("foo/bar")]
            [InlineData("C:\\foo")]
            public void Relative_Subdirectory_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf("/foo", subdirectory, OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("/foo/..")]
            [InlineData("/foo/../bar")]
            [InlineData("/foo/.")]
            public void Subdirectory_With_Traversal_Segments_Throws_ArgumentException(string subdirectory)
            {
                var ex = Record.Exception(() => FileSafety.IsRootDirectoryOf("/foo", subdirectory, OperatingSystem.Linux));

                Assert.NotNull(ex);
                Assert.IsType<ArgumentException>(ex);
            }

            [Theory]
            [InlineData("/foo", "/foo")]
            [InlineData("//foo/bar", "//foo/bar")]
            public void Equal_Paths_Match(string root, string subdirectory)
            {
                Assert.True(FileSafety.IsRootDirectoryOf(root, subdirectory, OperatingSystem.Linux));
            }

            [Fact]
            public void Equal_Paths_Different_Case_Do_Not_Match()
            {
                Assert.False(FileSafety.IsRootDirectoryOf("/FOO", "/foo", OperatingSystem.Linux));
            }

            [Theory]
            [InlineData("/foo", "/foo/bar", '/')]
            [InlineData("/foo", "/foo\\bar", '\\')]
            [InlineData("//foo/bar", "//foo/bar/baz", '/')]
            [InlineData("//foo/bar", "//foo/bar\\baz", '\\')]
            [InlineData("/foo/", "/foo/bar", '/')]
            [InlineData("/foo\\", "/foo/bar", '/')]
            public void Subdirectory_Match_Depends_On_Native_Separator_At_The_Boundary(string root, string subdirectory, char boundarySeparator)
            {
                var expected = boundarySeparator == Path.DirectorySeparatorChar;

                Assert.Equal(expected, FileSafety.IsRootDirectoryOf(root, subdirectory, OperatingSystem.Linux));
            }

            [Theory]
            [InlineData("/foo", "/foobar")]
            [InlineData("/foo/bar", "/foo")]
            [InlineData("/foo", "/bar")]
            [InlineData("/foo/bar", "/foo/baz")]
            public void Non_Subdirectory_Returns_False(string root, string subdirectory)
            {
                Assert.False(FileSafety.IsRootDirectoryOf(root, subdirectory, OperatingSystem.Linux));
            }

            [Fact]
            public void StartsWith_Check_Is_Case_Sensitive()
            {
                var subdirectory = "/FOO" + Path.DirectorySeparatorChar + "bar";

                Assert.False(FileSafety.IsRootDirectoryOf("/foo", subdirectory, OperatingSystem.Linux));
            }
        }
    }
}
