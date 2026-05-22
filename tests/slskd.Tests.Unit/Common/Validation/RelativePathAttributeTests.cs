using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class RelativePathAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(string value, OSPlatform? os = null)
    {
        var attribute = new RelativePathAttribute(os);
        var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
        var result = attribute.GetValidationResult(value, context);
        return (result == null, result?.ErrorMessage);
    }

    // null and empty are explicitly allowed; presence is enforced by [Required] separately
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

    public class Passes
    {
        [Theory]
        [InlineData("Artist")]                              // simple name
        [InlineData("music.flac")]                          // file with extension
        [InlineData("My Artist")]                           // spaces
        [InlineData("Artist.Name")]                         // dots that are not traversal
        [InlineData("Artist/Album")]                        // forward slash
        [InlineData("Artist\\Album")]                       // backslash
        [InlineData("Artist/Album/Song.flac")]              // multi-level forward slash
        [InlineData("Artist\\Album\\Song.flac")]            // multi-level backslash
        [InlineData("Artist/Album\\Song.flac")]             // mixed separators
        [InlineData("...dir")]                              // three dots — not a traversal segment
        [InlineData("..hidden")]                            // starts with .. but is a name, not traversal
        [InlineData(".hidden")]                             // dotfile name, not a traversal segment
        [InlineData("dir/..dir")]                           // segment starts with .. but is not exactly ..
        [InlineData("Ünïcödé Àrtïst")]                     // latin extended
        [InlineData("Ünïcödé/Àrtïst/Àlbüm")]              // latin extended, forward slash
        [InlineData("Ünïcödé\\Àrtïst\\Àlbüm")]            // latin extended, backslash
        [InlineData("пользователь/Музыка")]                 // Cyrillic, forward slash
        [InlineData("пользователь\\Музыка\\Альбом")]        // Cyrillic, backslash
        [InlineData("用户/音乐/专辑")]                       // Chinese, forward slash
        [InlineData("用户\\音乐\\专辑")]                     // Chinese, backslash
        [InlineData("ユーザー/音楽/アルバム")]                // Japanese, forward slash
        [InlineData("ユーザー\\音楽\\アルバム")]              // Japanese, backslash
        [InlineData("사용자/음악/앨범")]                      // Korean, forward slash
        [InlineData("사용자\\음악\\앨범")]                    // Korean, backslash
        [InlineData("مستخدم/موسيقى")]                       // Arabic
        [InlineData("χρήστης/μουσική")]                     // Greek
        [InlineData("משתמש/מוזיקה")]                        // Hebrew
        public void RelativePath_Passes(string value)
        {
            var (isValid, _) = Validate(value, OSPlatform.Linux);
            Assert.True(isValid);
        }
    }

    public class Absolute_Fails
    {
        [Theory]
        // Windows drive-letter paths
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("D:\\path\\to\\file")]
        [InlineData("Z:/downloads")]
        [InlineData("C:")]
        // Windows root-relative (no drive letter)
        [InlineData("\\Music")]
        [InlineData("\\Music\\Artist\\Album")]
        // Windows UNC
        [InlineData("\\\\server\\share")]
        [InlineData("\\\\server\\share\\folder")]
        [InlineData("\\\\192.168.1.1\\share")]
        // Unix/Linux
        [InlineData("/")]
        [InlineData("/home/user")]
        [InlineData("/etc/hosts")]
        [InlineData("/usr/local/bin")]
        // macOS
        [InlineData("/Users/username")]
        [InlineData("/Volumes/External Drive")]
        // Unix-style UNC (// prefix)
        [InlineData("//server/share")]
        [InlineData("//server/share/folder")]
        // Windows drive-relative (no separator after colon; still rooted)
        [InlineData("C:Music")]
        public void AbsolutePath_Fails(string value)
        {
            var (isValid, errorMessage) = Validate(value, OSPlatform.Windows);
            Assert.False(isValid);
            Assert.Equal("The Field field must be a relative path.", errorMessage);
        }
    }
}
