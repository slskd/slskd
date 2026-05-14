using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class AbsolutePathAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(string value)
    {
        var attribute = new AbsolutePathAttribute();
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

    public class Passes
    {
        // Standard Windows drive-letter absolute paths
        [Theory]
        [InlineData("C:\\Music")]                           // basic
        [InlineData("C:/Music")]                            // forward slash separator
        [InlineData("C:\\Music\\Artist\\Album")]            // multi-level backslash
        [InlineData("C:\\Music\\Artist/Album")]             // mixed separators
        [InlineData("C:\\Music\\My Artist")]                // spaces in component
        [InlineData("C:\\Program Files\\App")]              // spaces, typical Windows path
        [InlineData("D:\\path")]                            // different drive letter
        [InlineData("Z:\\downloads")]                       // drive letter Z
        [InlineData("C:")]                                  // bare drive
        [InlineData("C:\\Users\\Ünïcödé\\Music")]          // latin extended
        [InlineData("C:\\Users\\пользователь\\Музыка")]    // Cyrillic
        [InlineData("C:\\Users\\用户\\音乐")]               // Chinese
        [InlineData("C:\\Users\\ユーザー\\音楽")]           // Japanese
        [InlineData("C:\\Users\\사용자\\음악")]             // Korean
        [InlineData("C:\\Users\\مستخدم\\موسيقى")]          // Arabic
        [InlineData("C:\\Users\\χρήστης\\μουσική")]        // Greek
        [InlineData("C:Music")]                             // drive-relative (no separator)
        public void Windows_DriveLetterPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }

        // Windows root paths (drive root with trailing separator)
        [Theory]
        [InlineData("C:\\")]    // Windows C: root
        [InlineData("D:\\")]    // Windows D: root
        [InlineData("Z:\\")]    // Windows Z: root
        [InlineData("C:/")]     // root with forward slash
        public void Windows_RootPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }

        // Windows root-relative paths (rooted to the current drive, no drive letter)
        [Theory]
        [InlineData("\\Music")]                     // root-relative, backslash
        [InlineData("\\Music\\Artist\\Album")]      // deep, backslash
        [InlineData("/Music")]                      // root-relative, forward slash
        [InlineData("/Music/Artist/Album")]         // deep, forward slash
        [InlineData("\\Ünïcödé")]                   // latin extended
        [InlineData("\\пользователь\\Музыка")]      // Cyrillic
        [InlineData("\\用户\\音乐")]                 // Chinese
        [InlineData("\\ユーザー\\音楽")]             // Japanese
        [InlineData("\\사용자\\음악")]               // Korean
        public void Windows_RootRelativePaths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }

        // Windows UNC (Universal Naming Convention) paths.
        // Server name is DNS-constrained (ASCII), but share names and path components support Unicode.
        [Theory]
        [InlineData("\\\\server\\share")]                       // basic UNC
        [InlineData("\\\\server\\share\\folder")]               // UNC with subfolder
        [InlineData("\\\\server\\share\\My Music")]             // UNC with spaces
        [InlineData("\\\\192.168.1.1\\share")]                  // UNC with IP address
        [InlineData("\\\\DESKTOP-ABC\\SharedFolder")]           // UNC with machine name
        [InlineData("\\\\192.168.1.1\\Ünïcödé")]               // latin extended share name
        [InlineData("\\\\server\\Müsïk")]                       // latin extended share name
        [InlineData("\\\\server\\share\\Ünïcödé Artist")]       // latin extended path component
        [InlineData("\\\\server\\音楽")]                        // Japanese share name
        [InlineData("\\\\server\\share\\пользователь")]         // Cyrillic path component
        [InlineData("\\\\server\\share\\用户\\音乐")]           // Chinese path components
        [InlineData("\\\\server\\share\\사용자\\음악")]         // Korean path components
        public void Windows_UNCPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }

        // Unix/Linux absolute paths — always start with a leading /
        [Theory]
        [InlineData("/")]                               // filesystem root
        [InlineData("/home")]                           // single component
        [InlineData("/home/user")]                      // typical home directory
        [InlineData("/home/user/Music")]                // deep path
        [InlineData("/home/user/My Music")]             // spaces in component
        [InlineData("/etc/hosts")]                      // config file path
        [InlineData("/usr/local/bin")]                  // system path
        [InlineData("/var/log/app.log")]                // log file
        [InlineData("/tmp")]                            // temp directory
        [InlineData("/media/usb0")]                     // mounted device
        [InlineData("/mnt/storage/Music")]              // mounted storage
        [InlineData("/home/user/Ünïcödé")]              // latin extended
        [InlineData("/home/пользователь/Музыка")]       // Cyrillic
        [InlineData("/home/用户/音乐")]                  // Chinese
        [InlineData("/home/ユーザー/音楽")]              // Japanese
        [InlineData("/home/사용자/음악")]                // Korean
        [InlineData("/home/مستخدم/موسيقى")]             // Arabic
        [InlineData("/home/χρήστης/μουσική")]           // Greek
        [InlineData("/home/משתמש/מוזיקה")]              // Hebrew
        public void Unix_Paths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }

        // macOS absolute paths — same format as Unix, with macOS-specific conventions
        [Theory]
        [InlineData("/Users/username")]                             // home directory
        [InlineData("/Users/username/Music")]                       // music library
        [InlineData("/Users/username/Music/Artist/Album")]          // deep path
        [InlineData("/Volumes/External Drive")]                     // mounted external drive
        [InlineData("/Volumes/External Drive/Music")]               // file on external drive
        [InlineData("/Applications/App.app")]                       // application bundle
        [InlineData("/Library/Application Support/App")]            // system library
        [InlineData("/Users/Ünïcödé")]                              // latin extended username
        [InlineData("/Users/пользователь/Музыка")]                  // Cyrillic
        [InlineData("/Users/用户/音乐")]                             // Chinese
        [InlineData("/Users/ユーザー/音楽")]                         // Japanese
        [InlineData("/Users/사용자/음악")]                           // Korean
        [InlineData("/Volumes/Ünïcödé Drive/Music")]                // latin extended volume name
        [InlineData("/Volumes/音楽ドライブ")]                        // Japanese volume name
        [InlineData("/Volumes/외장 드라이브/음악")]                  // Korean volume name
        public void MacOS_Paths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }

        // Unix-style UNC equivalents (// prefix, used on some systems)
        [Theory]
        [InlineData("//server/share")]                  // basic
        [InlineData("//server/share/folder")]           // with subfolder
        [InlineData("//server/Ünïcödé")]                // latin extended share name
        [InlineData("//server/share/Ünïcödé Artist")]  // latin extended path component
        [InlineData("//server/音楽")]                   // Japanese share name
        [InlineData("//server/share/пользователь")]    // Cyrillic path component
        public void Unix_UNCPaths_Pass(string value)
        {
            var (isValid, _) = Validate(value);
            Assert.True(isValid);
        }
    }

    // Paths containing . or .. segments, which are valid directory names in isolation
    // but indicate traversal or ambiguity and must be rejected regardless of whether
    // the path is otherwise absolute or relative.
    public class Traversal_Fails
    {
        [Theory]
        // Windows-style traversal
        [InlineData("C:\\Music\\..\\Windows")]      // traversal to sibling via backslash
        [InlineData("C:\\Music\\.\\Artist")]        // current-dir segment via backslash
        [InlineData("C:/Music/../Windows")]         // traversal via forward slash
        [InlineData("C:/Music/./Artist")]           // current-dir via forward slash
        [InlineData("C:\\..")]                      // traversal at drive root
        [InlineData("D:\\a\\b\\..\\c")]            // traversal deep in path
        // Unix-style traversal
        [InlineData("/../etc")]                             // traversal at Unix root
        [InlineData("/home/user/../etc")]                   // traversal to sibling on Unix
        [InlineData("/home/user/./Music")]                  // current-dir on Unix
        [InlineData("/home/../../etc")]                     // double traversal on Unix
        // UNC traversal (backslash)
        [InlineData("\\\\server\\..\\Windows")]             // traversal past UNC server
        [InlineData("\\\\server\\share\\..\\other")]        // traversal within UNC share
        [InlineData("\\\\server\\share\\.\\folder")]        // current-dir within UNC share
        // UNC traversal (forward slash)
        [InlineData("//server/../share")]                   // traversal past server, forward slash
        [InlineData("//server/share/../other")]             // traversal within share, forward slash
        public void TraversalSegment_Fails(string value)
        {
            var (isValid, errorMessage) = Validate(value);
            Assert.False(isValid);
            Assert.Contains("traversal segments", errorMessage);
        }
    }

    public class Relative_Fails
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
            var (isValid, errorMessage) = Validate(value);
            Assert.False(isValid);
            Assert.Equal("The Field field must be an absolute file path.", errorMessage);
        }
    }
}