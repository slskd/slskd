using System;
using System.IO;
using Xunit;

namespace slskd.Tests.Unit.Files;

internal static class CombineSafelyTestHelpers
{
    // Use a platform-appropriate absolute base path. Backslash roots are not recognised on Linux/macOS,
    // so a Windows-style base would make containment assertions meaningless on those platforms.
    public static readonly string Base = OperatingSystem.IsWindows() ? "C:\\base" : "/base";

    public static void AssertContainedInBase(string result)
    {
        var resolvedBase = Path.GetFullPath(Base);
        var resolvedResult = Path.GetFullPath(result);
        Assert.True(
            resolvedResult == resolvedBase ||
            resolvedResult.StartsWith(resolvedBase + Path.DirectorySeparatorChar),
            $"Result '{resolvedResult}' escapes base '{resolvedBase}'");
    }
}

public class CombineSafely_Returns
{
    [Fact]
    public void NoSegments_ReturnsBasePath()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base);
        Assert.Equal(CombineSafelyTestHelpers.Base, result);
    }

    [Fact]
    public void EmptySegment_ReturnsBasePath()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, string.Empty);
        Assert.Equal(CombineSafelyTestHelpers.Base, result);
    }

    [Fact]
    public void SingleSegment_ReturnsBaseAndSegment()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "subdir");
        Assert.Equal(Path.Combine(CombineSafelyTestHelpers.Base, "subdir"), result);
    }

    [Fact]
    public void MultipleSegments_CombinesAll()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub", "dir", "file.flac");
        Assert.Equal(Path.Combine(CombineSafelyTestHelpers.Base, "sub", "dir", "file.flac"), result);
    }

    [Fact]
    public void SegmentWithForwardSlash_CombinesCorrectly()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub/dir");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void SegmentWithBackslash_CombinesCorrectly()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub\\dir");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void SegmentWithDoubleSlash_CollapsedAndContained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub//dir");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void SegmentWithDoubleBackslash_CollapsedAndContained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub\\\\dir");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void SegmentWithSpaces_Contained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "My Artist", "My Album");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void SegmentWithUnicode_Contained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "Ünïcödé", "пользователь", "音楽");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void DotfileSegment_Contained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, ".hidden");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void ThreeDotsSegment_Contained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "...dir");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }

    [Fact]
    public void DotDotPrefixedName_Contained()
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "..hidden");
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }
}

public class CombineSafely_AlwaysContainedInBase
{
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
    public void SingleSegment_IsContained(string segment)
    {
        var result = FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, segment);
        CombineSafelyTestHelpers.AssertContainedInBase(result);
    }
}

public class CombineSafely_Throws_ForRootedSegment
{
    [Theory]
    [InlineData("/Music")]              // Unix absolute / Windows root-relative, forward slash
    [InlineData("//server/share")]      // Unix-style UNC
    [InlineData("C:\\Music")]           // Windows drive-letter, backslash
    [InlineData("C:/Music")]            // Windows drive-letter, forward slash
    [InlineData("D:\\path")]            // alternate drive letter
    [InlineData("C:relative")]          // Windows drive-relative
    [InlineData("\\Music")]             // Windows root-relative, backslash
    [InlineData("\\\\server\\share")]   // Windows UNC
    public void Throws_ArgumentException(string segment)
    {
        var ex = Assert.Throws<ArgumentException>(() => FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, segment));
        Assert.Contains("Rooted", ex.Message);
    }

    [Fact]
    public void Throws_WhenLaterSegmentIsRooted()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "valid", "/escape"));
        Assert.Contains("Rooted", ex.Message);
    }

    [Fact]
    public void Throws_WhenLaterSegmentIsRooted_Windows()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "valid", "C:\\escape"));
        Assert.Contains("Rooted", ex.Message);
    }

    [Fact]
    public void Throws_WhenLastSegmentIsRooted()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub", "dir", "/escape"));
        Assert.Contains("Rooted", ex.Message);
    }

    [Fact]
    public void Throws_WhenLastSegmentIsRooted_Windows()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub", "dir", "C:\\escape"));
        Assert.Contains("Rooted", ex.Message);
    }
}

public class CombineSafely_Throws_ForTraversalSegment
{
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
    // Unicode path components mixed with traversal
    [InlineData("Ünïcödé/../escape")]
    [InlineData("пользователь/../../escape")]
    [InlineData("用户\\..\\escape")]
    public void Throws_ArgumentException(string segment)
    {
        var ex = Assert.Throws<ArgumentException>(() => FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, segment));
        Assert.Contains("traversal", ex.Message);
    }

    [Fact]
    public void Throws_WhenLaterSegmentContainsTraversal()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "valid", "../escape"));
        Assert.Contains("traversal", ex.Message);
    }

    [Fact]
    public void Throws_WhenLastSegmentIsTraversal()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, "sub", "dir", ".."));
        Assert.Contains("traversal", ex.Message);
    }
}

public class CombineSafely_NullAndEdgeCases
{
    [Fact]
    public void NullSegmentsArray_Throws()
    {
        // Passing null without an explicit cast is interpreted as a null params array.
        // foreach over a null array throws NullReferenceException.
        Assert.ThrowsAny<Exception>(() => FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, null));
    }

    [Fact]
    public void NullSegment_Throws()
    {
        // A null element inside the params array reaches Path.IsPathRooted(null),
        // which throws ArgumentNullException.
        Assert.Throws<ArgumentNullException>(() => FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, null, "subdir"));
    }

    [Fact]
    public void NullSegmentAlone_Throws()
    {
        // Explicit cast to string forces a single-element array containing null.
        // Path.IsPathRooted(null) throws ArgumentNullException.
        Assert.Throws<ArgumentNullException>(() => FileSafety.CombineSafely(CombineSafelyTestHelpers.Base, (string)null));
    }

    [Fact]
    public void NullBasePath_Throws()
    {
        Assert.ThrowsAny<Exception>(() => FileSafety.CombineSafely(null, "subdir"));
    }

    [Fact]
    public void TraversalInRoot_IsNotValidated()
    {
        // CombineSafely only validates segments, not the root parameter — root is assumed
        // to be a known-good value supplied by the application, not untrusted input.
        // This test documents that a traversal-containing root is NOT rejected.
        var root = OperatingSystem.IsWindows() ? "C:\\base\\..\\etc" : "/base/../etc";
        var result = FileSafety.CombineSafely(root, "subdir");
        Assert.NotNull(result);
    }
}
