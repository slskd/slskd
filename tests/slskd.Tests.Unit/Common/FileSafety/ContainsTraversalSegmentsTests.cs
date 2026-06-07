using Xunit;

namespace slskd.Tests.Unit.Common;

public partial class FileSafetyTests
{
    public class ContainsTraversalSegmentTests
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
        // Segments that look like traversal but have surrounding whitespace — not exact matches
        [InlineData(" . ")]
        [InlineData(" .. ")]
        public void Path_DoesNotContainTraversal(string path)
        {
            Assert.False(FileSafety.ContainsTraversalSegments(path));
        }
    }
}