using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class AbsolutePathAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(string value, OperatingSystem os = OperatingSystem.Any)
    {
        var attribute = new AbsolutePathAttribute(os);
        var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
        var result = attribute.GetValidationResult(value, context);
        return (result == null, result?.ErrorMessage);
    }

    // paths can be null or empty; [Required] dictates whether they must be defined
    [Fact]
    public void Null_Passes()
    {
        var (isValid, _) = Validate(null);
        Assert.True(isValid);
    }

    [Fact]
    public void EmptyString_Passes()
    {
        var (isValid, _) = Validate(string.Empty);
        Assert.True(isValid);
    }

    public class Windows
    {
        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("C:\\Music\\Artist\\Album")]
        [InlineData("C:\\Music\\Artist/Album")]
        [InlineData("C:/Music/My Artist")]
        public void Windows_DriveLetterPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Windows);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("C:\\")]
        [InlineData("Z:\\")]
        [InlineData("C:/")]
        public void Windows_RootPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Windows);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("\\\\server\\share")]
        [InlineData("\\\\server\\share\\folder")]
        [InlineData("\\\\192.168.1.1\\share")]
        public void Windows_UNCPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Windows);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("\\Music")]
        [InlineData("/Music")]
        [InlineData("/Music/Artist/Album")]
        [InlineData("C:")]
        [InlineData("C:Music")]
        public void Windows_RootRelativePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Windows);
            Assert.False(isValid);
        }
    }

    public class Unix
    {
        [Theory]
        [InlineData("/")]
        [InlineData("/home")]
        [InlineData("/home/user")]
        public void Unix_Paths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Linux);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("//server/share")]
        [InlineData("//server/share/folder")]
        [InlineData("//192.168.1.1/share")]
        public void Unix_UNCPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Linux);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("\\")]
        [InlineData("\\home")]
        public void Unix_Backslash_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Linux);
            Assert.False(isValid);
        }
    }

    public class Relative
    {
        [Theory]
        [InlineData("subdir")]              // simple name
        [InlineData("sub/dir")]             // forward slash
        [InlineData("sub\\dir")]            // backslash
        [InlineData("Artist\\Album")]       // multi-level
        [InlineData("music.flac")]          // file name with extension
        [InlineData("My Music")]            // spaces
        [InlineData("Ünïcödé")]             // latin extended, still relative
        [InlineData("Artist/Album/Song")]   // deep relative
        [InlineData("...dir")]              // three dots — relative, not traversal
        [InlineData(".hidden")]             // dotfile — relative, not traversal
        [InlineData("..")]                  // traversal-like but fundamentally not absolute
        [InlineData(".")]                   // current-dir but fundamentally not absolute
        public void RelativePath_Fails(string value)
        {
            var (isValid, errorMessage) = Validate(value, OperatingSystem.Linux);

            Assert.False(isValid);
            Assert.Equal("The Field field must be an absolute file path.", errorMessage);

            var (isValid2, errorMessage2) = Validate(value, OperatingSystem.Windows);

            Assert.False(isValid2);
            Assert.Equal("The Field field must be an absolute file path.", errorMessage2);
        }
    }
}
