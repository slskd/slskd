using System.ComponentModel.DataAnnotations;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class DisallowedCharactersAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed, bool ignoreCase = true)
    {
        var attribute = new DisallowedCharactersAttribute(disallowed) { IgnoreCase = ignoreCase };
        var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
        var result = attribute.GetValidationResult(value, context);
        return (result == null, result?.ErrorMessage);
    }

    // null and empty pass — [Required] handles presence; this attribute only checks content
    [Fact]
    public void Null_Passes()
    {
        var (isValid, _) = Validate(null, ['!']);
        Assert.True(isValid);
    }

    [Fact]
    public void EmptyString_Passes()
    {
        var (isValid, _) = Validate(string.Empty, ['!']);
        Assert.True(isValid);
    }

    public class Passes
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed, bool ignoreCase = true)
            => DisallowedCharactersAttributeTests.Validate(value, disallowed, ignoreCase);

        // Punctuation has no case variants — IgnoreCase must not affect the result either way
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NoDisallowedCharsPresent_Passes_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("hello", ['!', '@'], ignoreCase);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EmptyDisallowedSet_Passes_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("hello!@#", [], ignoreCase);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Unicode_AllowedChars_Pass_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("пользователь/音楽", ['!', '@'], ignoreCase);
            Assert.True(isValid);
        }

        [Fact]
        public void NonString_Passes()
        {
            var attribute = new DisallowedCharactersAttribute('!');
            var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
            var result = attribute.GetValidationResult(42, context);
            Assert.Equal(ValidationResult.Success, result);
        }

        // IgnoreCase = false only: a disallowed letter in one case does not block the other case
        [Fact]
        public void CaseSensitive_LowerCaseDisallowed_DoesNotBlock_UpperCase()
        {
            var (isValid, _) = Validate("HELLO", ['h', 'e', 'l', 'o'], ignoreCase: false);
            Assert.True(isValid);
        }

        [Fact]
        public void CaseSensitive_UpperCaseDisallowed_DoesNotBlock_LowerCase()
        {
            var (isValid, _) = Validate("hello", ['H', 'E', 'L', 'O'], ignoreCase: false);
            Assert.True(isValid);
        }
    }

    public class Fails
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed, bool ignoreCase = true)
            => DisallowedCharactersAttributeTests.Validate(value, disallowed, ignoreCase);

        // Punctuation has no case variants — IgnoreCase must not affect the result either way
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SingleDisallowedChar_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("hello!", ['!'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DisallowedCharAtStart_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("!hello", ['!'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DisallowedCharAtEnd_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("hello!", ['!'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DisallowedCharInMiddle_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("hel!lo", ['!'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OneOfMultipleDisallowedCharsPresent_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("hello!", ['!', '@', '#'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MultipleDisallowedCharsAllPresent_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("h!e@l#lo", ['!', '@', '#'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DisallowedCharRepeated_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("he!lo!", ['!'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AllCharsDisallowed_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("!@#", ['!', '@', '#'], ignoreCase);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Unicode_DisallowedChar_Fails_Regardless_Of_IgnoreCase(bool ignoreCase)
        {
            var (isValid, _) = Validate("пользователь", ['е'], ignoreCase);
            Assert.False(isValid);
        }

        // IgnoreCase = true only: a disallowed letter blocks its other-case equivalent
        [Fact]
        public void IgnoreCase_LowerCaseDisallowed_Blocks_UpperCase()
        {
            var (isValid, _) = Validate("HELLO", ['h', 'e', 'l', 'o'], ignoreCase: true);
            Assert.False(isValid);
        }

        [Fact]
        public void IgnoreCase_UpperCaseDisallowed_Blocks_LowerCase()
        {
            var (isValid, _) = Validate("hello", ['H', 'E', 'L', 'O'], ignoreCase: true);
            Assert.False(isValid);
        }

        [Fact]
        public void CaseSensitive_ExactMatch_Fails()
        {
            var (isValid, _) = Validate("hello", ['h'], ignoreCase: false);
            Assert.False(isValid);
        }
    }

    public class ErrorMessage_Contains
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed, bool ignoreCase = true)
            => DisallowedCharactersAttributeTests.Validate(value, disallowed, ignoreCase);

        [Fact]
        public void DisplayName()
        {
            var (_, errorMessage) = Validate("hello!", ['!']);
            Assert.Contains("Field", errorMessage);
        }

        [Fact]
        public void TheDisallowedChar()
        {
            var (_, errorMessage) = Validate("hello!", ['!']);
            Assert.Contains("'!'", errorMessage);
        }

        [Fact]
        public void AllPresentDisallowedChars()
        {
            var (_, errorMessage) = Validate("h!@l", ['!', '@']);
            Assert.Contains("'!'", errorMessage);
            Assert.Contains("'@'", errorMessage);
        }

        [Fact]
        public void EachOccurrenceOfRepeatedChar()
        {
            // "he!lo!" has two '!' — both occurrences are listed
            var (_, errorMessage) = Validate("he!lo!", ['!']);
            Assert.Equal(2, errorMessage.Split("'!'").Length - 1);
        }

        [Fact]
        public void OnlyCharsActuallyPresent_NotAllDisallowedChars()
        {
            // '@' is disallowed but not in the value — must not appear in the message
            var (_, errorMessage) = Validate("hello!", ['!', '@']);
            Assert.Contains("'!'", errorMessage);
            Assert.DoesNotContain("'@'", errorMessage);
        }

        [Fact]
        public void IgnoreCase_ShowsActualChar_NotDisallowedChar()
        {
            // 'a' is disallowed (lowercase); value contains 'A' (uppercase).
            // The message reports what the user supplied ('A'), not the disallowed definition ('a').
            var (_, errorMessage) = Validate("A", ['a'], ignoreCase: true);
            Assert.Contains("'A'", errorMessage);
            Assert.DoesNotContain("'a'", errorMessage);
        }
    }
}