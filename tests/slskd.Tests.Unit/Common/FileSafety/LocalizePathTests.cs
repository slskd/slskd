using System.Runtime.InteropServices;
using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class LocalizePathTests
    {
        [Theory]
        [InlineData("foo/bar", "foo/bar")]
        [InlineData("foo\\bar", "foo/bar")]
        [InlineData("foo/bar/baz", "foo/bar/baz")]
        [InlineData("foo\\bar/baz", "foo/bar/baz")]
        [InlineData("foo\\\\bar", "foo//bar")]
        [InlineData("@@abcde\\foo\\bar\\", "@@abcde/foo/bar/")]
        [InlineData("@@abcde/foo/bar/", "@@abcde/foo/bar/")]
        [InlineData("\\\\server\\foo\\", "//server/foo/")]
        [InlineData("//server/foo/", "//server/foo/")]
        [InlineData("C:\\Windows\\foo", "C:/Windows/foo")]
        [InlineData("C:/Windows/foo", "C:/Windows/foo")]
        [InlineData("", "")]
        public void Linux_NormalizesToForwardSlash(string input, string expected)
        {
            var result = input.LocalizePath(OSPlatform.Linux);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("foo/bar", "foo\\bar")]
        [InlineData("foo\\bar", "foo\\bar")]
        [InlineData("foo/bar/baz", "foo\\bar\\baz")]
        [InlineData("foo\\bar/baz", "foo\\bar\\baz")]
        [InlineData("foo//bar", "foo\\\\bar")]
        [InlineData("@@abcde\\foo\\bar\\", "@@abcde\\foo\\bar\\")]
        [InlineData("@@abcde/foo/bar/", "@@abcde\\foo\\bar\\")]
        [InlineData("\\\\server\\foo\\", "\\\\server\\foo\\")]
        [InlineData("//server/foo/", "\\\\server\\foo\\")]
        [InlineData("C:\\Windows\\foo", "C:\\Windows\\foo")]
        [InlineData("C:/Windows/foo", "C:\\Windows\\foo")]
        [InlineData("", "")]
        public void Windows_NormalizesToBackslash(string input, string expected)
        {
            var result = input.LocalizePath(OSPlatform.Windows);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void NullOs_UsesPlatformDefault_DoesNotThrow()
        {
            var result = "foo/bar".LocalizePath();

            Assert.False(string.IsNullOrEmpty(result));
        }
    }
}
