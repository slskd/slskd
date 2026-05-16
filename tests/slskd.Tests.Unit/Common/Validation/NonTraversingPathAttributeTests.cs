using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class NonTraversingPathAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(string value)
    {
        var attribute = new NonTraversingPathAttribute();
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

    public class NonTraversal_Passes
    {
        [Theory]
        // Windows-style
        [InlineData("C:\\Music\\foo\\Windows")]
        [InlineData("C:/Music/foo/Windows")]
        // Unix-style
        [InlineData("/home/user/foo/etc")]
        [InlineData("\\home\\user\\foo\\etc")]
        // UNC (backslash)
        [InlineData("\\\\server\\share\\foo\\folder")]
        // UNC (forward slash)
        [InlineData("//server/foo/share")]
        // soulseek
        [InlineData("@foo/bar/baz")]
        [InlineData("@foo\\bar\\baz")]
        // filenames
        [InlineData("@foo/bar/baz/baz../..qux/file.ext")]
        public void NonTraversalSegment_Passes(string value)
        {
            var (isValid, errorMessage) = Validate(value);
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }
    }

    // Paths containing . or .. segments, which are valid directory names in isolation
    // but indicate traversal or ambiguity and must be rejected regardless of whether
    // the path is otherwise absolute or relative.
    public class Traversal_Fails
    {
        [Theory]
        // Windows-style
        [InlineData("C:\\Music\\..\\Windows")]
        [InlineData("C:\\Music\\.\\Artist")]
        [InlineData("C:/Music/../Windows")]
        [InlineData("C:/Music/./Artist")]
        [InlineData("C:\\..")]
        [InlineData("D:\\a\\b\\..\\c")]
        // Unix-style
        [InlineData("/../etc")]
        [InlineData("/home/user/../etc")]
        [InlineData("/home/user/./Music")]
        [InlineData("/home/../../etc")]
        // UNC (backslash)
        [InlineData("\\\\server\\..\\Windows")]
        [InlineData("\\\\server\\share\\..\\other")]
        [InlineData("\\\\server\\share\\.\\folder")]
        // UNC (forward slash)
        [InlineData("//server/../share")]
        [InlineData("//server/share/../other")]
        // soulseek
        [InlineData("@foo/bar/baz/baz/../file.ext")]
        [InlineData("@foo/bar/baz/./../file.ext")]
        [InlineData("@foo/bar/baz/./baz/file.ext")]
        public void TraversalSegment_Fails(string value)
        {
            var (isValid, errorMessage) = Validate(value);
            Assert.False(isValid);
            Assert.Contains("traversal segments", errorMessage);
        }
    }
}
