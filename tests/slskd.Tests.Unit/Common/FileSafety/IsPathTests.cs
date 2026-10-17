namespace slskd.Tests.Unit.Common;

using Xunit;

public partial class FileSafetyTests
{
    public class IsPathTests
    {
        public class Windows
        {
            public class Absolute
            {
                [Theory]
                // drive-letter paths (X:\ or X:/)
                [InlineData("C:\\")]
                [InlineData("C:/")]
                [InlineData("C:\\Music")]
                [InlineData("C:/Music")]
                [InlineData("C:\\Music\\Artist\\Album")]
                [InlineData("D:\\path")]
                [InlineData("Z:\\downloads")]
                [InlineData("C:\\Users\\Ünïcödé\\Music")]
                [InlineData("C:\\Users\\пользователь\\Музыка")]
                [InlineData("C:\\Users\\用户\\音乐")]
                // lowercase drive letters
                [InlineData("c:\\Music")]
                [InlineData("a:/path")]
                // UNC paths (\\ prefix)
                [InlineData("\\\\server\\share")]
                [InlineData("\\\\server\\share\\folder")]
                [InlineData("\\\\server\\share\\My Music")]
                [InlineData("\\\\192.168.1.1\\share")]
                [InlineData("\\\\server\\Ünïcödé")]
                [InlineData("\\\\server\\share\\пользователь")]
                [InlineData("\\\\server\\share\\用户\\音乐")]
                [InlineData("\\\\server")]
                public void IsAbsolute_NotRelative(string path)
                {
                    Assert.True(FileSafety.IsPathAbsolute(path, OperatingSystem.Windows));
                    Assert.False(FileSafety.IsPathRelative(path, OperatingSystem.Windows));
                }
            }

            public class Relative
            {
                [Theory]
                [InlineData(null)]
                [InlineData("")]
                [InlineData("subdir")]
                [InlineData("sub/dir")]
                [InlineData("sub\\dir")]
                [InlineData("Artist\\Album")]
                [InlineData("Artist/Album/Song")]
                [InlineData("music.flac")]
                [InlineData("My Music")]
                [InlineData(".hidden")]
                [InlineData("..hidden")]
                [InlineData("...dir")]
                [InlineData("../escape")]
                [InlineData("./dir")]
                [InlineData("..")]
                [InlineData(".")]
                [InlineData("Ünïcödé")]
                [InlineData("пользователь/Музыка")]
                [InlineData("/Music")]
                [InlineData("\\Music")]
                [InlineData("/home/user")]
                [InlineData("//server/share")]
                [InlineData("//server")]
                [InlineData("C:")]
                [InlineData("C:Music")]
                [InlineData("D:path")]
                public void IsRelative_NotAbsolute(string path)
                {
                    Assert.False(FileSafety.IsPathAbsolute(path, OperatingSystem.Windows));
                    Assert.True(FileSafety.IsPathRelative(path, OperatingSystem.Windows));
                }
            }
        }

        public class Linux
        {
            public class Absolute
            {
                [Theory]
                [InlineData("/")]
                [InlineData("/home")]
                [InlineData("/home/user/Music")]
                [InlineData("/home/user/My Music")]
                [InlineData("/etc/hosts")]
                [InlineData("/tmp")]
                [InlineData("/home/Ünïcödé")]
                [InlineData("/home/пользователь/Музыка")]
                [InlineData("/home/用户/音乐")]
                [InlineData("/home/ユーザー/音楽")]
                [InlineData("/home/사용자/음악")]
                [InlineData("//server/share")]
                [InlineData("//server/share/folder")]
                [InlineData("//192.168.1.1/share")]
                public void IsAbsolute_NotRelative(string path)
                {
                    Assert.True(FileSafety.IsPathAbsolute(path, OperatingSystem.Linux));
                    Assert.False(FileSafety.IsPathRelative(path, OperatingSystem.Linux));
                }
            }

            public class Relative
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
                [InlineData("./dir")]
                [InlineData("..")]
                [InlineData(".")]
                [InlineData("Ünïcödé")]
                [InlineData("пользователь/Музыка")]
                [InlineData("C:\\Music")]
                [InlineData("C:/Music")]
                [InlineData("\\\\server\\share")]
                [InlineData("\\Music")]
                public void IsRelative_NotAbsolute(string path)
                {
                    Assert.False(FileSafety.IsPathAbsolute(path, OperatingSystem.Linux));
                    Assert.True(FileSafety.IsPathRelative(path, OperatingSystem.Linux));
                }
            }
        }
    }
}