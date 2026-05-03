namespace slskd.Tests.Unit.Common.Extensions
{
    using System;
    using Xunit;

    public class ToLocalRelativeFilenameTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("\t")]
        public void Throws_ArgumentException_Given_Bad_Remote_Filename(string filename)
        {
            var ex = Record.Exception(() => filename.ToLocalRelativeFilename());

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
        }

        [Fact]
        public void Returns_Localized_Filename_And_Parent_Directory()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Assert.Equal(@"path\file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename());
                Assert.Equal(@"path\file.ext", "@username/deeply/nested/path/file.ext".ToLocalRelativeFilename());
            }
            else
            {
                Assert.Equal(@"path/file.ext", @"C:\deeply\nested\path\file.ext".ToLocalRelativeFilename());
                Assert.Equal(@"path/file.ext", @"@username\deeply\nested\path\file.ext".ToLocalRelativeFilename());
            }
        }

        [Fact]
        public void Returns_Mirror_Format()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Assert.Equal(@"deeply\nested\path\file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Mirror));
                Assert.Equal(@"deeply\nested\path\file.ext", "@username/deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Mirror));
            }
            else
            {
                Assert.Equal(@"deeply/nested/path/file.ext", @"C:\deeply\nested\path\file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Mirror));
                Assert.Equal(@"deeply/nested/path/file.ext", @"@username\deeply\nested\path\file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Mirror));
            }
        }

        [Fact]
        public void Returns_Root_Format()
        {
            Assert.Equal("file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Root));
            Assert.Equal("file.ext", "@username/deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Root));
        }

        [Fact]
        public void Includes_Username_When_Option_Set()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Assert.Equal(@"user\path\file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Subfolder, true, "user"));
            }
            else
            {
                Assert.Equal(@"user/path/file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Subfolder, true, "user"));
            }
        }

        [Fact]
        public void Returns_Just_File_If_Only_File_Given()
        {
            Assert.Equal("file.ext", "file.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Returns_Mirrored_Path_With_Strip_Count()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Assert.Equal(@"path\file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Mirror, false, null, 2));
            }
            else
            {
                Assert.Equal(@"path/file.ext", "deeply/nested/path/file.ext".ToLocalRelativeFilename(DownloadDirectoryFormat.Mirror, false, null, 2));
            }
        }

        [Fact]
        public void Removes_Invalid_Characters_From_Path_And_Filename()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Assert.Equal(@"p_a_t_h\fi_le.ext", @"p?a|t<h/fi>le.ext".ToLocalRelativeFilename());
            }
            else
            {
                Assert.Equal(@"_", $"{'\0'}".ToLocalRelativeFilename());
            }
        }
    }
}
