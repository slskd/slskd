using Microsoft.Extensions.Options;

namespace slskd.Tests.Unit.Files
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Files;
    using Xunit;

    public class FileServiceTests : IDisposable
    {
        public FileServiceTests()
        {
            OptionsMonitorMock = new Mock<IOptionsMonitor<Options>>();

            Temp = Path.Combine(Path.GetTempPath(), $"slskd.test.{Guid.NewGuid()}");
            Directory.CreateDirectory(Temp);

            FileService = new FileService(
                optionsMonitor: OptionsMonitorMock.Object);
        }

        public void Dispose()
        {
            Directory.Delete(Temp, recursive: true);
        }

        private Mock<IOptionsMonitor<Options>> OptionsMonitorMock { get; init; }
        private string Temp { get; init; }
        private FileService FileService { get; init; }

        [Fact]
        public async Task ListContentsAsync_Throws_ArgumentException_Given_Relative_Path()
        {
            var ex = await Record.ExceptionAsync(() => FileService.ListContentsAsync(directory: "../"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("directory", ((ArgumentException)ex).ParamName);
        }

        [Fact]
        public async Task ListContentsAsync_Throws_UnauthorizedException_Given_Disallowed_Directory()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.ListContentsAsync(directory: Path.Combine(Temp, "foo")));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
        }

        [Fact]
        public async Task ListContentsAsync_Throws_NotFoundException_Given_NonExistent_Directory()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.ListContentsAsync(directory: Path.Combine(Temp, "downloads", "foo")));

            Assert.NotNull(ex);
            Assert.IsType<NotFoundException>(ex);
        }

        [Fact]
        public async Task DeleteDirectoriesAsync_Throws_ArgumentException_Given_Relative_Path()
        {
            var ex = await Record.ExceptionAsync(() => FileService.DeleteDirectoriesAsync("../foo"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("directories", ((ArgumentException)ex).ParamName);
        }

        [Fact]
        public async Task DeleteDirectoriesAsync_Throws_ArgumentException_Given_Disallowed_Path()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.DeleteDirectoriesAsync(Path.Combine(Temp, "foo")));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
        }

        [Fact]
        public async Task DeleteFilesAsync_Throws_ArgumentException_Given_Relative_Path()
        {
            var ex = await Record.ExceptionAsync(() => FileService.DeleteFilesAsync("../foo.bar"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("files", ((ArgumentException)ex).ParamName);
        }

        [Fact]
        public async Task DeleteFilesAsync_Throws_ArgumentException_Given_Disallowed_Path()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.DeleteFilesAsync(Path.Combine(Temp, "foo")));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
        }

        [Fact]
        public void CreateFile_Creates_Directory_When_It_Does_Not_Exist()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options());

            var dir = Path.Combine(Temp, "newdir");
            var filename = Path.Combine(dir, "file.txt");

            using var stream = FileService.CreateFile(filename);

            Assert.True(Directory.Exists(dir));
        }

        [Fact]
        public void CreateFile_Creates_Directory_With_Unix_File_Mode_From_Options()
        {
            if (OperatingSystem.IsWindows()) return;

            var mode = "0755";

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = mode,
                    }
                }
            });

            var dir = Path.Combine(Temp, "newdir");
            var filename = Path.Combine(dir, "file.txt");

            using var stream = FileService.CreateFile(filename);

            var dirInfo = new DirectoryInfo(dir);
            Assert.Equal(mode.ToUnixFileMode(), dirInfo.UnixFileMode);
        }

        [Fact]
        public void CreateFile_Creates_Directory_With_Unix_File_Mode_From_CreateFileOptions()
        {
            if (OperatingSystem.IsWindows()) return;

            var unixMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options());

            var dir = Path.Combine(Temp, "newdir");
            var filename = Path.Combine(dir, "file.txt");

            using var stream = FileService.CreateFile(filename, new CreateFileOptions
            {
                UnixCreateMode = unixMode,
            });

            var dirInfo = new DirectoryInfo(dir);
            Assert.Equal(unixMode, dirInfo.UnixFileMode);
        }

        [Fact]
        public void CreateFile_Creates_File_With_Unix_File_Mode()
        {
            if (OperatingSystem.IsWindows()) return;

            var mode = "0644";

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = mode,
                    }
                }
            });

            var filename = Path.Combine(Temp, "file.txt");

            using (var stream = FileService.CreateFile(filename))
            {
            }

            Assert.Equal(mode.ToUnixFileMode(), File.GetUnixFileMode(filename));
        }

        [Fact]
        public void CreateFile_Creates_Directory_Without_Unix_File_Mode_When_Not_Configured()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options());

            var dir = Path.Combine(Temp, "newdir");
            var filename = Path.Combine(dir, "file.txt");

            using var stream = FileService.CreateFile(filename);

            Assert.True(Directory.Exists(dir));
        }

        [Fact]
        public void MoveFile_Creates_Destination_Directory_When_It_Does_Not_Exist()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options());

            var sourceDir = Path.Combine(Temp, "source");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(sourceFile, "test");

            var destDir = Path.Combine(Temp, "dest");

            FileService.MoveFile(sourceFile, destDir);

            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public void MoveFile_Creates_Destination_Directory_With_Unix_File_Mode_From_Options()
        {
            if (OperatingSystem.IsWindows()) return;

            var mode = "0755";

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = mode,
                    }
                }
            });

            var sourceDir = Path.Combine(Temp, "source");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(sourceFile, "test");

            var destDir = Path.Combine(Temp, "dest");

            FileService.MoveFile(sourceFile, destDir);

            var dirInfo = new DirectoryInfo(destDir);
            Assert.Equal(mode.ToUnixFileMode(), dirInfo.UnixFileMode);
        }

        [Fact]
        public void MoveFile_Creates_Destination_Directory_With_Unix_File_Mode_From_Parameter()
        {
            if (OperatingSystem.IsWindows()) return;

            var unixMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                         | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options());

            var sourceDir = Path.Combine(Temp, "source");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(sourceFile, "test");

            var destDir = Path.Combine(Temp, "dest");

            FileService.MoveFile(sourceFile, destDir, unixFileMode: unixMode);

            var dirInfo = new DirectoryInfo(destDir);
            Assert.Equal(unixMode, dirInfo.UnixFileMode);
        }

        [Fact]
        public void MoveFile_Sets_Unix_File_Mode_On_Moved_File()
        {
            if (OperatingSystem.IsWindows()) return;

            var mode = "0644";

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = mode,
                    }
                }
            });

            var sourceDir = Path.Combine(Temp, "source");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(sourceFile, "test");

            var destDir = Path.Combine(Temp, "dest");
            Directory.CreateDirectory(destDir);

            var result = FileService.MoveFile(sourceFile, destDir);

            Assert.Equal(mode.ToUnixFileMode(), File.GetUnixFileMode(result));
        }

        [Fact]
        public void MoveFile_Creates_Destination_Directory_Without_Unix_File_Mode_When_Not_Configured()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options());

            var sourceDir = Path.Combine(Temp, "source");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(sourceFile, "test");

            var destDir = Path.Combine(Temp, "dest");

            FileService.MoveFile(sourceFile, destDir);

            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public void MoveFile_Prefers_Parameter_Unix_File_Mode_Over_Options()
        {
            if (OperatingSystem.IsWindows()) return;

            var optionsMode = "0644";
            var paramMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                          | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = optionsMode,
                    }
                }
            });

            var sourceDir = Path.Combine(Temp, "source");
            Directory.CreateDirectory(sourceDir);
            var sourceFile = Path.Combine(sourceDir, "file.txt");
            File.WriteAllText(sourceFile, "test");

            var destDir = Path.Combine(Temp, "dest");

            FileService.MoveFile(sourceFile, destDir, unixFileMode: paramMode);

            var dirInfo = new DirectoryInfo(destDir);
            Assert.Equal(paramMode, dirInfo.UnixFileMode);
            Assert.Equal(paramMode, File.GetUnixFileMode(Path.Combine(destDir, "file.txt")));
        }

        [Fact]
        public void CreateFile_Prefers_CreateFileOptions_Unix_Mode_Over_Options()
        {
            if (OperatingSystem.IsWindows()) return;

            var optionsMode = "0644";
            var paramMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                          | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = optionsMode,
                    }
                }
            });

            var dir = Path.Combine(Temp, "newdir");
            var filename = Path.Combine(dir, "file.txt");

            using (var stream = FileService.CreateFile(filename, new CreateFileOptions
            {
                UnixCreateMode = paramMode,
            }))
            {
            }

            var dirInfo = new DirectoryInfo(dir);
            Assert.Equal(paramMode, dirInfo.UnixFileMode);
            Assert.Equal(paramMode, File.GetUnixFileMode(filename));
        }
    }
}

