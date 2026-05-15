using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class GuidAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(object value)
    {
        var attribute = new GuidAttribute();
        var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
        var result = attribute.GetValidationResult(value, context);
        return (result == null, result?.ErrorMessage);
    }

    // null is allowed; [Required] dictates whether the field must be defined
    [Fact]
    public void Null_Passes()
    {
        var (isValid, _) = Validate(null);
        Assert.True(isValid);
    }

    public class Passes
    {
        [Theory]
        [InlineData("d3b07384-d9a0-4b9b-abcd-012345678901")]    // lowercase with hyphens
        [InlineData("D3B07384-D9A0-4B9B-ABCD-012345678901")]    // uppercase with hyphens
        [InlineData("d3b07384d9a04b9babcd012345678901")]         // no hyphens (N format)
        [InlineData("{d3b07384-d9a0-4b9b-abcd-012345678901}")]  // braces format
        [InlineData("(d3b07384-d9a0-4b9b-abcd-012345678901)")]  // parentheses format
        [InlineData("00000000-0000-0000-0000-000000000000")]     // all zeros
        [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]     // all f's
        public void ValidGuidString_Passes(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }
    }

    public class Fails
    {
        [Fact]
        public void EmptyString_Fails()
        {
            var (isValid, errorMessage) = Validate(string.Empty);
            Assert.False(isValid);
            Assert.Equal("The Field field must be a valid GUID/UUIDv4", errorMessage);
        }

        [Theory]
        [InlineData("not-a-guid")]                                      // arbitrary string
        [InlineData("d3b07384-d9a0-4b9b-abcd")]                        // too short
        [InlineData("d3b07384-d9a0-4b9b-abcd-01234567890Z")]           // invalid character Z
        [InlineData("d3b07384-d9a0-4b9b-abcd-0123456789012")]          // one char too long
        [InlineData("g3b07384-d9a0-4b9b-abcd-012345678901")]           // invalid hex char g
        [InlineData("d3b07384 d9a0 4b9b abcd 012345678901")]           // spaces instead of hyphens
        public void InvalidGuidString_Fails(string value)
        {
            var (isValid, errorMessage) = Validate(value);
            Assert.False(isValid);
            Assert.Equal("The Field field must be a valid GUID/UUIDv4", errorMessage);
        }

        [Fact]
        public void NonStringValue_Fails()
        {
            var (isValid, errorMessage) = Validate(42);
            Assert.False(isValid);
            Assert.Equal("The Field field must be a valid GUID/UUIDv4", errorMessage);
        }
    }
}
