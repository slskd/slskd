namespace slskd.Tests.Unit.Common.Extensions
{
    using System;
    using System.IO;
    using System.Linq;
    using Xunit;

    public class ReplaceInvalidFileNameCharactersTests
    {
        [Fact]
        public void Throws_NullReferenceException_Given_Null()
        {
            string path = null;
            Assert.Throws<NullReferenceException>(() => path.ReplaceInvalidFileNameCharacters());
        }

        [Fact]
        public void Returns_Empty_String_Given_Empty_Input()
        {
            Assert.Equal(string.Empty, string.Empty.ReplaceInvalidFileNameCharacters());
        }

        [Fact]
        public void Returns_Input_Unchanged_When_No_Invalid_Characters_Present()
        {
            var valid = "valid_filename.ext";
            Assert.Equal(valid, valid.ReplaceInvalidFileNameCharacters());
        }

        [Fact]
        public void Preserves_Valid_Characters()
        {
            // Common valid filename chars that must not be touched
            var valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._- ";
            Assert.Equal(valid, valid.ReplaceInvalidFileNameCharacters());
        }

        [Fact]
        public void Default_Replacement_Character_Is_Underscore()
        {
            var c = Path.GetInvalidFileNameChars().First();
            var result = $"a{c}b".ReplaceInvalidFileNameCharacters();
            Assert.Equal("a_b", result);
        }

        [Fact]
        public void Replaces_Each_Invalid_Filename_Character_With_Default_Replacement()
        {
            // Each invalid char is tested individually so failures identify the exact offending char
            Assert.All(Path.GetInvalidFileNameChars(), c =>
            {
                var result = $"prefix{c}suffix".ReplaceInvalidFileNameCharacters();
                Assert.False(result.Contains(c), $"Invalid character U+{(int)c:X4} was not replaced");
                Assert.Equal("prefix_suffix", result);
            });
        }

        [Fact]
        public void Replaces_All_Invalid_Filename_Characters_Simultaneously()
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var allInvalid = new string(invalidChars);
            var result = allInvalid.ReplaceInvalidFileNameCharacters();

            Assert.All(invalidChars, c =>
                Assert.False(result.Contains(c), $"Invalid character U+{(int)c:X4} remained after sanitization"));
        }

        [Fact]
        public void Result_Contains_Only_Replacement_Character_When_All_Input_Characters_Are_Invalid()
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var allInvalid = new string(invalidChars);
            var result = allInvalid.ReplaceInvalidFileNameCharacters();

            Assert.Equal(new string('_', invalidChars.Length), result);
        }

        [Fact]
        public void Uses_Custom_Replacement_Character()
        {
            char replacement = '-';

            Assert.All(Path.GetInvalidFileNameChars(), c =>
            {
                var result = $"a{c}b".ReplaceInvalidFileNameCharacters(replacement);
                Assert.False(result.Contains(c), $"Invalid character U+{(int)c:X4} was not replaced");
                Assert.Equal("a-b", result);
            });
        }

        [Fact]
        public void Replaces_Consecutive_Invalid_Characters()
        {
            var chars = Path.GetInvalidFileNameChars();
            if (chars.Length < 2)
            {
                return;
            }

            var input = $"a{chars[0]}{chars[1]}b";
            var result = input.ReplaceInvalidFileNameCharacters();

            Assert.Equal("a__b", result);
        }

        [Fact]
        public void Replaces_Invalid_Character_At_Start_Of_String()
        {
            var c = Path.GetInvalidFileNameChars().First();
            var result = $"{c}filename.ext".ReplaceInvalidFileNameCharacters();
            Assert.Equal("_filename.ext", result);
        }

        [Fact]
        public void Replaces_Invalid_Character_At_End_Of_String()
        {
            var c = Path.GetInvalidFileNameChars().First();
            var result = $"filename.ext{c}".ReplaceInvalidFileNameCharacters();
            Assert.Equal("filename.ext_", result);
        }

        [Fact]
        public void Result_Contains_No_Invalid_Filename_Characters()
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var input = "valid" + new string(invalidChars) + "also_valid";
            var result = input.ReplaceInvalidFileNameCharacters();

            Assert.All(invalidChars, c =>
                Assert.False(result.Contains(c), $"Character U+{(int)c:X4} remained after sanitization"));
        }

        [Fact]
        public void Valid_Characters_Surrounding_Invalid_Characters_Are_Preserved()
        {
            var c = Path.GetInvalidFileNameChars().First();
            var result = $"before{c}after".ReplaceInvalidFileNameCharacters();

            Assert.StartsWith("before", result);
            Assert.EndsWith("after", result);
        }
    }
}
