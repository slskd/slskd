using System;
using System.IO;
using Xunit;

namespace slskd.Tests.Unit.Files;

public class FileSafetyTests
{
    // Use a platform-appropriate absolute base path. Backslash roots are not recognised on Linux/macOS,
    // so a Windows-style base would make containment assertions meaningless on those platforms.
    private static readonly string Base = OperatingSystem.IsWindows() ? "C:\\base" : "/base";

    private static void AssertContainedInBase(string result)
    {
        var resolvedBase = Path.GetFullPath(Base);
        var resolvedResult = Path.GetFullPath(result);
        Assert.True(
            resolvedResult == resolvedBase ||
            resolvedResult.StartsWith(resolvedBase + Path.DirectorySeparatorChar),
            $"Result '{resolvedResult}' escapes base '{resolvedBase}'");
    }

    public class ContainsTraversalSegments_ReturnsTrue
    {
        [Theory]
        // Bare traversal segments
        [InlineData("..")]
        [InlineData(".")]
        // Traversal as first component — forward slash
        [InlineData("../escape")]
        [InlineData("./dir")]
        // Traversal at end — forward slash
        [InlineData("sub/..")]
        [InlineData("sub/.")]
        // Traversal in middle — forward slash
        [InlineData("sub/../escape")]
        [InlineData("sub/./dir")]
        // Double traversal — forward slash
        [InlineData("sub/../../escape")]
        [InlineData("a/b/c/../../..")]
        // Traversal via backslash
        [InlineData("sub\\..")]
        [InlineData("sub\\.")]
        [InlineData("sub\\..\\escape")]
        [InlineData("sub\\.\\dir")]
        // Mixed separators
        [InlineData("sub/..\\escape")]
        [InlineData("sub\\./../escape")]
        // Unicode path components mixed with forward-slash traversal
        [InlineData("Ünïcödé/../escape")]
        [InlineData("Ünïcödé/./escape")]
        [InlineData("пользователь/../etc")]
        [InlineData("пользователь/./etc")]
        [InlineData("用户/../escape")]
        [InlineData("ユーザー/../../escape")]
        [InlineData("사용자/../escape")]
        [InlineData("مستخدم/../escape")]
        [InlineData("χρήστης/../μουσική")]
        [InlineData("משתמש/../מוזיקה")]
        // Unicode path components mixed with backslash traversal
        [InlineData("用户\\..\\escape")]
        [InlineData("ユーザー\\..\\escape")]
        [InlineData("пользователь\\..\\Музыка")]
        // Traversal between two valid Unicode segments
        [InlineData("Ünïcödé/../пользователь")]
        [InlineData("用户/music/../escape")]
        // Traversal at end after unicode
        [InlineData("Ünïcödé/..")]
        [InlineData("пользователь/..")]
        [InlineData("用户\\..")]
        public void Path_ContainsTraversal(string path)
        {
            Assert.True(FileSafety.ContainsTraversalSegments(path));
        }
    }

    public class ContainsTraversalSegments_ReturnsFalse
    {
        [Theory]
        // Simple names — no separators, no dots
        [InlineData("subdir")]
        [InlineData("file.flac")]
        [InlineData("My Artist")]
        [InlineData("Artist.Name")]
        // Dotfile and dot-prefixed names — not exactly . or ..
        [InlineData(".hidden")]
        [InlineData("..hidden")]
        [InlineData("...")]
        [InlineData("...dir")]
        // Segments that start with .. but are not exactly ..
        [InlineData("dir/..dir")]
        [InlineData("dir/...")]
        // Forward-slash paths — no traversal
        [InlineData("sub/dir")]
        [InlineData("Artist/Album")]
        [InlineData("Artist/Album/Song.flac")]
        // Backslash paths — no traversal
        [InlineData("sub\\dir")]
        [InlineData("Artist\\Album")]
        [InlineData("Artist\\Album\\Song.flac")]
        // Mixed separators — no traversal
        [InlineData("Artist/Album\\Song.flac")]
        // Double separators — empty segment is not . or ..
        [InlineData("sub//dir")]
        [InlineData("sub\\\\dir")]
        // Null and empty string — both return false without throwing
        [InlineData(null)]
        [InlineData("")]
        // Unicode — no traversal, forward slash
        [InlineData("Ünïcödé")]
        [InlineData("Ünïcödé/Àrtïst/Àlbüm")]
        [InlineData("пользователь/Музыка")]
        [InlineData("用户/音乐/专辑")]
        [InlineData("ユーザー/音楽/アルバム")]
        [InlineData("사용자/음악/앨범")]
        [InlineData("مستخدم/موسيقى")]
        [InlineData("χρήστης/μουσική")]
        [InlineData("משתמש/מוזיקה")]
        // Unicode — no traversal, backslash
        [InlineData("пользователь\\Музыка\\Альбом")]
        [InlineData("用户\\音乐\\专辑")]
        [InlineData("ユーザー\\音楽\\アルバム")]
        [InlineData("사용자\\음악\\앨범")]
        // Unicode names that start with dots — not traversal
        [InlineData("Ünïcödé/..hidden")]
        [InlineData("пользователь/...dir")]
        [InlineData("用户/..名前")]
        public void Path_DoesNotContainTraversal(string path)
        {
            Assert.False(FileSafety.ContainsTraversalSegments(path));
        }
    }

    public class CombineSafely_Returns
    {
        [Fact]
        public void NoSegments_ReturnsBasePath()
        {
            var result = FileSafety.CombineSafely(Base);
            Assert.Equal(Base, result);
        }

        [Fact]
        public void EmptySegment_ReturnsBasePath()
        {
            var result = FileSafety.CombineSafely(Base, string.Empty);
            Assert.Equal(Base, result);
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
        }

        [Fact]
        public void SegmentWithForwardSlash_CombinesCorrectly()
        {
            var result = FileSafety.CombineSafely(Base, "sub/dir");
            AssertContainedInBase(result);
        }

        [Fact]
        public void SegmentWithBackslash_CombinesCorrectly()
        {
            var result = FileSafety.CombineSafely(Base, "sub\\dir");
            AssertContainedInBase(result);
        }

        [Fact]
        public void SegmentWithDoubleSlash_CollapsedAndContained()
        {
            var result = FileSafety.CombineSafely(Base, "sub//dir");
            AssertContainedInBase(result);
        }

        [Fact]
        public void SegmentWithDoubleBackslash_CollapsedAndContained()
        {
            var result = FileSafety.CombineSafely(Base, "sub\\\\dir");
            AssertContainedInBase(result);
        }

        [Fact]
        public void SegmentWithSpaces_Contained()
        {
            var result = FileSafety.CombineSafely(Base, "My Artist", "My Album");
            AssertContainedInBase(result);
        }

        [Fact]
        public void SegmentWithUnicode_Contained()
        {
            var result = FileSafety.CombineSafely(Base, "Ünïcödé", "пользователь", "音楽");
            AssertContainedInBase(result);
        }

        [Fact]
        public void DotfileSegment_Contained()
        {
            var result = FileSafety.CombineSafely(Base, ".hidden");
            AssertContainedInBase(result);
        }

        [Fact]
        public void ThreeDotsSegment_Contained()
        {
            var result = FileSafety.CombineSafely(Base, "...dir");
            AssertContainedInBase(result);
        }

        [Fact]
        public void DotDotPrefixedName_Contained()
        {
            var result = FileSafety.CombineSafely(Base, "..hidden");
            AssertContainedInBase(result);
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
            var result = FileSafety.CombineSafely(Base, segment);
            var resolvedBase = Path.GetFullPath(Base);
            var resolvedResult = Path.GetFullPath(result);
            Assert.True(
                resolvedResult == resolvedBase ||
                resolvedResult.StartsWith(resolvedBase + Path.DirectorySeparatorChar),
                $"Result '{resolvedResult}' escapes base '{resolvedBase}'");
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
            var ex = Assert.Throws<ArgumentException>(() => FileSafety.CombineSafely(Base, segment));
            Assert.Contains("Rooted", ex.Message);
        }

        [Fact]
        public void Throws_WhenLaterSegmentIsRooted()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                FileSafety.CombineSafely(Base, "valid", "/escape"));
            Assert.Contains("Rooted", ex.Message);
        }

        [Fact]
        public void Throws_WhenLaterSegmentIsRooted_Windows()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                FileSafety.CombineSafely(Base, "valid", "C:\\escape"));
            Assert.Contains("Rooted", ex.Message);
        }

        [Fact]
        public void Throws_WhenLastSegmentIsRooted()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                FileSafety.CombineSafely(Base, "sub", "dir", "/escape"));
            Assert.Contains("Rooted", ex.Message);
        }

        [Fact]
        public void Throws_WhenLastSegmentIsRooted_Windows()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                FileSafety.CombineSafely(Base, "sub", "dir", "C:\\escape"));
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
            var ex = Assert.Throws<ArgumentException>(() => FileSafety.CombineSafely(Base, segment));
            Assert.Contains("traversal", ex.Message);
        }

        [Fact]
        public void Throws_WhenLaterSegmentContainsTraversal()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                FileSafety.CombineSafely(Base, "valid", "../escape"));
            Assert.Contains("traversal", ex.Message);
        }

        [Fact]
        public void Throws_WhenLastSegmentIsTraversal()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                FileSafety.CombineSafely(Base, "sub", "dir", ".."));
            Assert.Contains("traversal", ex.Message);
        }
    }

    public class IsPathAbsolute_ReturnsTrue
    {
        // Unix/Linux/macOS absolute paths
        [Theory]
        [InlineData("/")]                               // filesystem root
        [InlineData("/home")]                           // single component
        [InlineData("/home/user/Music")]                // deep path
        [InlineData("/home/user/My Music")]             // spaces in component
        [InlineData("/etc/hosts")]                      // config file
        [InlineData("/tmp")]                            // temp directory
        [InlineData("/home/Ünïcödé")]                  // latin extended
        [InlineData("/home/пользователь/Музыка")]       // Cyrillic
        [InlineData("/home/用户/音乐")]                  // Chinese
        [InlineData("/home/ユーザー/音楽")]              // Japanese
        [InlineData("/home/사용자/음악")]                // Korean
        public void Unix_Paths(string path)
        {
            Assert.True(FileSafety.IsPathAbsolute(path));
        }

        // Unix-style UNC paths (// prefix)
        [Theory]
        [InlineData("//server/share")]
        [InlineData("//server/share/folder")]
        [InlineData("//192.168.1.1/share")]
        [InlineData("//server/Ünïcödé")]
        [InlineData("//server/share/пользователь")]
        public void Unix_UNCPaths(string path)
        {
            Assert.True(FileSafety.IsPathAbsolute(path));
        }

        // Windows root-relative paths (rooted to the current drive, no drive letter)
        [Theory]
        [InlineData("\\Music")]
        [InlineData("\\Music\\Artist\\Album")]
        [InlineData("/Music")]
        [InlineData("/Music/Artist/Album")]
        [InlineData("\\Ünïcödé")]
        [InlineData("\\пользователь\\Музыка")]
        public void Windows_RootRelativePaths(string path)
        {
            Assert.True(FileSafety.IsPathAbsolute(path));
        }

        // Windows UNC paths (\\ prefix)
        [Theory]
        [InlineData("\\\\server\\share")]
        [InlineData("\\\\server\\share\\folder")]
        [InlineData("\\\\server\\share\\My Music")]
        [InlineData("\\\\192.168.1.1\\share")]
        [InlineData("\\\\server\\Ünïcödé")]
        [InlineData("\\\\server\\share\\пользователь")]
        [InlineData("\\\\server\\share\\用户\\音乐")]
        public void Windows_UNCPaths(string path)
        {
            Assert.True(FileSafety.IsPathAbsolute(path));
        }

        // Windows drive-letter paths — with or without a separator after the colon
        [Theory]
        [InlineData("C:\\")]                                    // drive root, backslash
        [InlineData("C:/")]                                     // drive root, forward slash
        [InlineData("C:\\Music")]                               // basic, backslash
        [InlineData("C:/Music")]                                // basic, forward slash
        [InlineData("C:\\Music\\Artist\\Album")]                // deep, backslash
        [InlineData("D:\\path")]                                // different drive letter
        [InlineData("Z:\\downloads")]                           // drive letter Z
        [InlineData("C:")]                                      // bare drive letter
        [InlineData("C:Music")]                                 // drive-relative, no separator
        [InlineData("D:path")]                                  // different drive letter, no separator
        [InlineData("C:\\Users\\Ünïcödé\\Music")]              // latin extended
        [InlineData("C:\\Users\\пользователь\\Музыка")]        // Cyrillic
        [InlineData("C:\\Users\\用户\\音乐")]                   // Chinese
        public void Windows_DriveLetterPaths(string path)
        {
            Assert.True(FileSafety.IsPathAbsolute(path));
        }
    }

    public class IsPathAbsolute_ReturnsFalse
    {
        [Theory]
        [InlineData(null)]                  // null
        [InlineData("")]                    // empty string
        [InlineData("subdir")]              // simple name
        [InlineData("sub/dir")]             // forward slash
        [InlineData("sub\\dir")]            // backslash
        [InlineData("Artist\\Album")]       // multi-level backslash
        [InlineData("Artist/Album/Song")]   // deep forward slash
        [InlineData("music.flac")]          // file name with extension
        [InlineData("My Music")]            // spaces
        [InlineData(".hidden")]             // dotfile
        [InlineData("..hidden")]            // dot-prefixed name
        [InlineData("...dir")]              // three dots — relative, not traversal
        [InlineData("../escape")]           // traversal — still relative
        [InlineData("./dir")]               // current-dir segment — still relative
        [InlineData("..")]                  // bare double-dot
        [InlineData(".")]                   // bare single-dot
        [InlineData("Ünïcödé")]            // unicode, no root
        [InlineData("пользователь/Музыка")] // unicode with separator, no root
        public void Relative_Paths(string path)
        {
            Assert.False(FileSafety.IsPathAbsolute(path));
        }
    }

    public class IsPathRelative_ReturnsTrue
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("subdir")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("music.flac")]
        [InlineData(".hidden")]
        [InlineData("../escape")]
        public void Relative_Paths(string path)
        {
            Assert.True(FileSafety.IsPathRelative(path));
        }

    }

    public class IsPathRelative_ReturnsFalse
    {
        [Theory]
        [InlineData("/home/user")]
        [InlineData("//server/share")]
        [InlineData("\\Music")]
        [InlineData("\\\\server\\share")]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("C:\\")]
        [InlineData("C:")]
        [InlineData("C:Music")]
        [InlineData("D:path")]
        public void Absolute_Paths(string path)
        {
            Assert.False(FileSafety.IsPathRelative(path));
        }
    }

    public class CombineSafely_NullAndEdgeCases
    {
        [Fact]
        public void NullSegmentsArray_Throws()
        {
            // Passing null without an explicit cast is interpreted as a null params array.
            // foreach over a null array throws NullReferenceException.
            Assert.ThrowsAny<Exception>(() => FileSafety.CombineSafely(Base, null));
        }

        [Fact]
        public void NullSegment_Throws()
        {
            // A null element inside the params array reaches Path.IsPathRooted(null),
            // which throws ArgumentNullException.
            Assert.Throws<ArgumentNullException>(() => FileSafety.CombineSafely(Base, null, "subdir"));
        }

        [Fact]
        public void NullSegmentAlone_Throws()
        {
            // Explicit cast to string forces a single-element array containing null.
            // Path.IsPathRooted(null) throws ArgumentNullException.
            Assert.Throws<ArgumentNullException>(() => FileSafety.CombineSafely(Base, (string)null));
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
}