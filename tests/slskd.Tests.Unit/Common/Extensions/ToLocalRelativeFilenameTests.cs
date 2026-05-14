namespace slskd.Tests.Unit.Common.Extensions
{
    using System;
    using System.IO;
    using System.Linq;
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
        public void Returns_Last_Directory_And_Filename_From_Deep_Path()
        {
            var expected = "path" + Path.DirectorySeparatorChar + "file.ext";
            Assert.Equal(expected, @"deeply\nested\path\file.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Returns_Just_Filename_If_No_Directory_Given()
        {
            Assert.Equal("file.ext", "file.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Replaces_Invalid_Characters_In_Filename()
        {
            // Exclude backslash and forward slash — they act as segment separators in the input
            // and would cause the dirty string to be split rather than treated as a filename.
            var invalidChars = Path.GetInvalidFileNameChars()
                .Where(c => c != '\\' && c != '/')
                .ToArray();

            var dirtyName = new string(invalidChars);
            var result = $@"a\b\{dirtyName}".ToLocalRelativeFilename();
            var filename = Path.GetFileName(result);

            Assert.All(invalidChars, c =>
                Assert.False(filename.Contains(c), $"Invalid character U+{(int)c:X4} was not replaced in filename"));
        }

        [Fact]
        public void Replaces_Invalid_Characters_In_Directory()
        {
            // Directory segments are sanitized with ReplaceInvalidFileNameCharacters, so the full
            // GetInvalidFileNameChars() set applies (excluding path separators, which split the input).
            var invalidChars = Path.GetInvalidFileNameChars()
                .Where(c => c != '\\' && c != '/')
                .ToArray();

            var dirtyName = new string(invalidChars);
            var result = $@"a\{dirtyName}\file.ext".ToLocalRelativeFilename();
            var directory = Path.GetDirectoryName(result);

            Assert.All(invalidChars, c =>
                Assert.False(directory.Contains(c), $"Invalid character U+{(int)c:X4} was not replaced in directory"));
        }

        [Fact]
        public void Replaces_Each_Invalid_Filename_Character_Individually()
        {
            // Tests each invalid filename char in isolation so that failures identify the exact
            // offending character rather than only reporting that something in a concatenated
            // string was not replaced.
            var invalidChars = Path.GetInvalidFileNameChars()
                .Where(c => c != '\\' && c != '/')
                .ToArray();

            Assert.All(invalidChars, c =>
            {
                var result = $@"a\b\prefix{c}suffix.ext".ToLocalRelativeFilename();
                var filename = Path.GetFileName(result);
                Assert.False(filename.Contains(c), $"Invalid character U+{(int)c:X4} was not replaced in filename");
            });
        }

        [Fact]
        public void Replaces_Each_Invalid_Filename_Character_In_Directory_Individually()
        {
            // Tests each invalid filename char in the directory segment in isolation so that
            // failures identify the exact offending character.
            var invalidChars = Path.GetInvalidFileNameChars()
                .Where(c => c != '\\' && c != '/')
                .ToArray();

            Assert.All(invalidChars, c =>
            {
                var result = $@"a\prefix{c}suffix\file.ext".ToLocalRelativeFilename();
                var directory = Path.GetDirectoryName(result);
                Assert.False(directory.Contains(c), $"Invalid character U+{(int)c:X4} was not replaced in directory");
            });
        }

        [Fact]
        public void Returns_Last_Directory_And_Filename_From_Two_Segment_Path()
        {
            var expected = "dir" + Path.DirectorySeparatorChar + "file.ext";
            Assert.Equal(expected, @"dir\file.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Handles_Forward_Slash_As_Segment_Separator()
        {
            // LocalizePath normalizes '/' to Path.DirectorySeparatorChar before splitting,
            // so forward-slash paths must produce the same result as backslash paths.
            var expected = "b" + Path.DirectorySeparatorChar + "c.ext";
            Assert.Equal(expected, "a/b/c.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Handles_Mixed_Slash_Separators()
        {
            var expected = "b" + Path.DirectorySeparatorChar + "c.ext";
            Assert.Equal(expected, @"a/b\c.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Preserves_Spaces_In_Filename()
        {
            // Spaces are valid in file names and must survive sanitization.
            var expected = "dir" + Path.DirectorySeparatorChar + "file name.ext";
            Assert.Equal(expected, @"root\dir\file name.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Preserves_Spaces_In_Directory()
        {
            var expected = "my dir" + Path.DirectorySeparatorChar + "file.ext";
            Assert.Equal(expected, @"root\my dir\file.ext".ToLocalRelativeFilename());
        }

        [Fact]
        public void Directory_Segment_Contains_No_Characters_Invalid_For_Directory_Names()
        {
            // All GetInvalidFileNameChars() (minus separators) must be absent from the directory
            // segment. Directory segments are sanitized with ReplaceInvalidFileNameCharacters, so
            // the full filename-invalid set applies.
            var invalidChars = Path.GetInvalidFileNameChars()
                .Where(c => c != '\\' && c != '/')
                .ToArray();

            Assert.All(invalidChars, c =>
            {
                var result = $@"a\prefix{c}suffix\file.ext".ToLocalRelativeFilename();
                var directory = Path.GetDirectoryName(result);
                Assert.False(directory.Contains(c), $"Character U+{(int)c:X4} is invalid for directory names but was not replaced");
            });
        }
    }
}