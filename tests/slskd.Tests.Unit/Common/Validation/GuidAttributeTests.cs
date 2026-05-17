using System;
using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class GuidAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(object value, bool allowEmpty = false)
    {
        var attribute = new GuidAttribute(allowEmpty);
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

    [Fact]
    public void Empty_String_Fails_If_AllowEmpty_False()
    {
        var (isValid, message) = Validate("00000000-0000-0000-0000-000000000000", allowEmpty: false);
        Assert.False(isValid);
        Assert.Contains("empty", message);
    }

    [Fact]
    public void Empty_Guid_Fails_If_AllowEmpty_False()
    {
        var (isValid, message) = Validate(Guid.Empty, allowEmpty: false);
        Assert.False(isValid);
        Assert.Contains("empty", message);
    }

    [Fact]
    public void AllowEmpty_Guid_Defaults_False()
    {
        var (isValid, message) = Validate(Guid.Empty);
        Assert.False(isValid);
        Assert.Contains("empty", message);
    }

    [Fact]
    public void AllowEmpty_String_Defaults_False()
    {
        var (isValid, message) = Validate(Guid.Empty.ToString());
        Assert.False(isValid);
        Assert.Contains("empty", message);
    }

    [Fact]
    public void Empty_String_Passes_If_AllowEmpty_True()
    {
        var (isValid, _) = Validate(Guid.Empty.ToString(), allowEmpty: true);
        Assert.True(isValid);
    }

    [Fact]
    public void Empty_Guid_Passes_If_AllowEmpty_True()
    {
        var (isValid, _) = Validate(Guid.Empty, allowEmpty: true);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("d3b07384-d9a0-4b9b-abcd-012345678901")]    // lowercase with hyphens
    [InlineData("D3B07384-D9A0-4B9B-ABCD-012345678901")]    // uppercase with hyphens
    [InlineData("d3b07384d9a04b9babcd012345678901")]         // no hyphens (N format)
    [InlineData("{d3b07384-d9a0-4b9b-abcd-012345678901}")]  // braces format
    [InlineData("(d3b07384-d9a0-4b9b-abcd-012345678901)")]  // parentheses format
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]     // all f's
    public void ValidGuidString_Passes(string value)
    {
        var (isValid, _) = Validate(value);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("not-a-guid")]                                     // arbitrary string
    [InlineData("d3b07384-d9a0-4b9b-abcd")]                        // too short
    [InlineData("d3b07384-d9a0-4b9b-abcd-01234567890Z")]           // invalid character Z
    [InlineData("d3b07384-d9a0-4b9b-abcd-0123456789012")]          // one char too long
    [InlineData("g3b07384-d9a0-4b9b-abcd-012345678901")]           // invalid hex char g
    [InlineData("d3b07384 d9a0 4b9b abcd 012345678901")]           // spaces instead of hyphens
    public void InvalidGuidString_Fails(string value)
    {
        var (isValid, errorMessage) = Validate(value);
        Assert.False(isValid);
        Assert.Equal("The Field field must be a valid GUID/UUID", errorMessage);
    }

    [Fact]
    public void NonStringValue_Fails()
    {
        var (isValid, errorMessage) = Validate(42);
        Assert.False(isValid);
        Assert.Equal("The Field field must be a valid GUID/UUID", errorMessage);
    }
}
