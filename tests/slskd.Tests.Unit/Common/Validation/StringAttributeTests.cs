using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class StringAttributeTests
{
    private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
    {
        var attr = new StringAttribute();
        configure?.Invoke(attr);
        var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
        var result = attr.GetValidationResult(value, context);
        return (result == null, result?.ErrorMessage);
    }

    // empty string passes all default checks — [Required] handles presence
    [Fact]
    public void EmptyString_Passes()
    {
        var (isValid, _) = Validate(string.Empty);
        Assert.True(isValid);
    }

    public class DisallowedCharacters
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed)
            => StringAttributeTests.Validate(value, a => a.DisallowedCharacters = disallowed);

        public class Passes
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed)
                => DisallowedCharacters.Validate(value, disallowed);

            [Fact]
            public void EmptyString_Passes()
            {
                var (isValid, _) = Validate(string.Empty, ['!']);
                Assert.True(isValid);
            }

            [Fact]
            public void NoDisallowedCharsPresent_Passes()
            {
                var (isValid, _) = Validate("hello", ['!', '@']);
                Assert.True(isValid);
            }

            [Fact]
            public void EmptyDisallowedSet_Passes()
            {
                var (isValid, _) = Validate("hello!@#", []);
                Assert.True(isValid);
            }

            [Fact]
            public void Unicode_AllowedChars_Pass()
            {
                var (isValid, _) = Validate("пользователь/音楽", ['!', '@']);
                Assert.True(isValid);
            }

            // Case-insensitive matching only folds the declared disallowed chars, not all letters;
            // a disallowed lower-case letter does not block an entirely different upper-case letter.
            [Fact]
            public void LowerCaseDisallowed_DoesNotBlock_Different_UpperCaseLetter()
            {
                var (isValid, _) = Validate("B", ['a']);
                Assert.True(isValid);
            }
        }

        public class Fails
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed)
                => DisallowedCharacters.Validate(value, disallowed);

            [Fact]
            public void SingleDisallowedChar_Fails()
            {
                var (isValid, _) = Validate("hello!", ['!']);
                Assert.False(isValid);
            }

            [Fact]
            public void DisallowedCharAtStart_Fails()
            {
                var (isValid, _) = Validate("!hello", ['!']);
                Assert.False(isValid);
            }

            [Fact]
            public void DisallowedCharAtEnd_Fails()
            {
                var (isValid, _) = Validate("hello!", ['!']);
                Assert.False(isValid);
            }

            [Fact]
            public void DisallowedCharInMiddle_Fails()
            {
                var (isValid, _) = Validate("hel!lo", ['!']);
                Assert.False(isValid);
            }

            [Fact]
            public void OneOfMultipleDisallowedCharsPresent_Fails()
            {
                var (isValid, _) = Validate("hello!", ['!', '@', '#']);
                Assert.False(isValid);
            }

            [Fact]
            public void MultipleDisallowedCharsAllPresent_Fails()
            {
                var (isValid, _) = Validate("h!e@l#lo", ['!', '@', '#']);
                Assert.False(isValid);
            }

            [Fact]
            public void DisallowedCharRepeated_Fails()
            {
                var (isValid, _) = Validate("he!lo!", ['!']);
                Assert.False(isValid);
            }

            [Fact]
            public void AllCharsDisallowed_Fails()
            {
                var (isValid, _) = Validate("!@#", ['!', '@', '#']);
                Assert.False(isValid);
            }

            [Fact]
            public void Unicode_DisallowedChar_Fails()
            {
                var (isValid, _) = Validate("пользователь", ['е']);
                Assert.False(isValid);
            }

            // character matching is always case-insensitive
            [Fact]
            public void LowerCaseDisallowed_Blocks_UpperCase()
            {
                var (isValid, _) = Validate("HELLO", ['h', 'e', 'l', 'o']);
                Assert.False(isValid);
            }

            [Fact]
            public void UpperCaseDisallowed_Blocks_LowerCase()
            {
                var (isValid, _) = Validate("hello", ['H', 'E', 'L', 'O']);
                Assert.False(isValid);
            }

            [Fact]
            public void ExactMatch_Fails()
            {
                var (isValid, _) = Validate("hello", ['h']);
                Assert.False(isValid);
            }
        }

        public class ErrorMessage_Contains
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, char[] disallowed)
                => DisallowedCharacters.Validate(value, disallowed);

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
            public void ShowsActualChar_NotDisallowedChar()
            {
                // 'a' is disallowed (lowercase); value contains 'A' (uppercase).
                // The message reports what the user supplied ('A'), not the disallowed definition ('a').
                var (_, errorMessage) = Validate("A", ['a']);
                Assert.Contains("'A'", errorMessage);
                Assert.DoesNotContain("'a'", errorMessage);
            }
        }
    }

    // -----------------------------------------------------------------------
    // AllowNull
    // -----------------------------------------------------------------------
    public class AllowNull
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
            => StringAttributeTests.Validate(value, configure);

        [Fact]
        public void Null_Passes_When_AllowNull_Is_True()
        {
            var (isValid, _) = Validate(null);
            Assert.True(isValid);
        }

        [Fact]
        public void Null_Fails_When_AllowNull_Is_False()
        {
            var (isValid, _) = Validate(null, a => a.AllowNull = false);
            Assert.False(isValid);
        }

        [Fact]
        public void NonNull_Passes_When_AllowNull_Is_False()
        {
            var (isValid, _) = Validate("hello", a => a.AllowNull = false);
            Assert.True(isValid);
        }

        public class ErrorMessage_Contains
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
                => AllowNull.Validate(value, configure);

            [Fact]
            public void Null_Keyword()
            {
                var (_, message) = Validate(null, a => a.AllowNull = false);
                Assert.Contains("null", message);
            }
        }
    }

    // -----------------------------------------------------------------------
    // AllowEmpty
    // -----------------------------------------------------------------------
    public class AllowEmpty
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
            => StringAttributeTests.Validate(value, configure);

        [Fact]
        public void Empty_Passes_When_AllowEmpty_Is_True()
        {
            var (isValid, _) = Validate(string.Empty);
            Assert.True(isValid);
        }

        [Fact]
        public void Empty_Fails_When_AllowEmpty_Is_False()
        {
            var (isValid, _) = Validate(string.Empty, a => a.AllowEmpty = false);
            Assert.False(isValid);
        }

        [Fact]
        public void NonEmpty_Passes_When_AllowEmpty_Is_False()
        {
            var (isValid, _) = Validate("hello", a => a.AllowEmpty = false);
            Assert.True(isValid);
        }

        public class ErrorMessage_Contains
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
                => AllowEmpty.Validate(value, configure);

            [Fact]
            public void Character_Keyword()
            {
                var (_, message) = Validate(string.Empty, a => a.AllowEmpty = false);
                Assert.Contains("character", message);
            }
        }
    }

    // -----------------------------------------------------------------------
    // AllowWhiteSpace
    // -----------------------------------------------------------------------
    public class AllowWhiteSpace
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
            => StringAttributeTests.Validate(value, configure);

        [Fact]
        public void WhiteSpace_Passes_When_AllowWhiteSpace_Is_True()
        {
            var (isValid, _) = Validate("   ");
            Assert.True(isValid);
        }

        [Fact]
        public void WhiteSpace_Fails_When_AllowWhiteSpace_Is_False()
        {
            var (isValid, _) = Validate("   ", a => a.AllowWhiteSpace = false);
            Assert.False(isValid);
        }

        [Fact]
        public void NonWhiteSpace_Passes_When_AllowWhiteSpace_Is_False()
        {
            var (isValid, _) = Validate("hello", a => a.AllowWhiteSpace = false);
            Assert.True(isValid);
        }

        // IsNullOrWhiteSpace("") is true, so empty string also fails the whitespace check
        // when AllowEmpty=true (default) and AllowWhiteSpace=false
        [Fact]
        public void EmptyString_Fails_When_AllowWhiteSpace_Is_False_And_AllowEmpty_Is_True()
        {
            var (isValid, _) = Validate(string.Empty, a => a.AllowWhiteSpace = false);
            Assert.False(isValid);
        }

        public class ErrorMessage_Contains
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
                => AllowWhiteSpace.Validate(value, configure);

            [Fact]
            public void Whitespace_Keyword()
            {
                var (_, message) = Validate("   ", a => a.AllowWhiteSpace = false);
                Assert.Contains("whitespace", message);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Length (MinimumLength / MaximumLength)
    // -----------------------------------------------------------------------
    public class Length
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
            => StringAttributeTests.Validate(value, configure);

        [Fact]
        public void Within_Range_Passes()
        {
            var (isValid, _) = Validate("hello", a => { a.MinimumLength = 3; a.MaximumLength = 10; });
            Assert.True(isValid);
        }

        [Fact]
        public void ExactlyAtMinimumLength_Passes()
        {
            var (isValid, _) = Validate("hi", a => a.MinimumLength = 2);
            Assert.True(isValid);
        }

        [Fact]
        public void ExactlyAtMaximumLength_Passes()
        {
            var (isValid, _) = Validate("hello", a => a.MaximumLength = 5);
            Assert.True(isValid);
        }

        [Fact]
        public void BelowMinimumLength_Fails()
        {
            var (isValid, _) = Validate("hi", a => a.MinimumLength = 5);
            Assert.False(isValid);
        }

        [Fact]
        public void ExceedsMaximumLength_Fails()
        {
            var (isValid, _) = Validate("hello world", a => a.MaximumLength = 5);
            Assert.False(isValid);
        }

        public class ErrorMessage_Contains
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
                => Length.Validate(value, configure);

            [Fact]
            public void MinimumLength_Value()
            {
                var (_, message) = Validate("hi", a => { a.MinimumLength = 5; a.MaximumLength = 20; });
                Assert.Contains("5", message);
            }

            [Fact]
            public void MaximumLength_Value()
            {
                var (_, message) = Validate("hi", a => { a.MinimumLength = 5; a.MaximumLength = 20; });
                Assert.Contains("20", message);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Pattern
    // -----------------------------------------------------------------------
    // The Pattern check fails validation when the value *matches* the regex —
    // Pattern acts as a "disallowed pattern" rather than a "required pattern".
    public class Pattern
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
            => StringAttributeTests.Validate(value, configure);

        [Fact]
        public void NullPattern_Passes()
        {
            // Pattern defaults to null; any value passes the pattern check
            var (isValid, _) = Validate("hello");
            Assert.True(isValid);
        }

        [Fact]
        public void ValueMatchingPattern_Fails()
        {
            var (isValid, _) = Validate("hello1", a => a.Pattern = new Regex("[0-9]"));
            Assert.False(isValid);
        }

        [Fact]
        public void ValueNotMatchingPattern_Passes()
        {
            var (isValid, _) = Validate("hello", a => a.Pattern = new Regex("[0-9]"));
            Assert.True(isValid);
        }

        public class ErrorMessage_Contains
        {
            private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
                => Pattern.Validate(value, configure);

            [Fact]
            public void RegularExpression_Keyword()
            {
                var (_, message) = Validate("hello1", a => a.Pattern = new Regex("[0-9]"));
                Assert.Contains("regular expression", message);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Check ordering
    // -----------------------------------------------------------------------
    // Validation checks run in declaration order; when a value violates multiple
    // conditions the error from the first check in code is the one returned.
    public class CheckOrdering
    {
        private static (bool IsValid, string ErrorMessage) Validate(string value, Action<StringAttribute> configure = null)
            => StringAttributeTests.Validate(value, configure);

        // AllowNull is checked before AllowEmpty:
        // null with both disabled returns the null error, not the empty error.
        [Fact]
        public void Null_ReturnsNullError_Not_EmptyError()
        {
            var (_, message) = Validate(null, a => { a.AllowNull = false; a.AllowEmpty = false; });
            Assert.Contains("null", message);
            Assert.DoesNotContain("character", message);
        }

        // AllowEmpty is checked before AllowWhiteSpace:
        // "" with both disabled returns the empty error, not the whitespace error.
        [Fact]
        public void Empty_ReturnsEmptyError_Not_WhitespaceError()
        {
            var (_, message) = Validate(string.Empty, a => { a.AllowEmpty = false; a.AllowWhiteSpace = false; });
            Assert.Contains("character", message);
            Assert.DoesNotContain("whitespace", message);
        }

        // AllowWhiteSpace is checked before the length check:
        // a whitespace string that is also below MinimumLength returns the whitespace error.
        [Fact]
        public void Whitespace_ReturnsWhitespaceError_Not_LengthError()
        {
            var (_, message) = Validate(" ", a => { a.AllowWhiteSpace = false; a.MinimumLength = 10; });
            Assert.Contains("whitespace", message);
            Assert.DoesNotContain("between", message);
        }

        // Length is checked before DisallowedCharacters:
        // a too-short string that also contains a disallowed character returns the length error.
        [Fact]
        public void TooShort_ReturnsLengthError_Not_DisallowedCharsError()
        {
            var (_, message) = Validate("hi!", a => { a.MinimumLength = 10; a.DisallowedCharacters = ['!']; });
            Assert.Contains("between", message);
            Assert.DoesNotContain("disallowed", message);
        }

        // DisallowedCharacters is checked before Pattern:
        // a string with a disallowed character that also matches the pattern returns the disallowed-chars error.
        [Fact]
        public void DisallowedChar_ReturnsDisallowedError_Not_PatternError()
        {
            var (_, message) = Validate("hello!1", a => { a.DisallowedCharacters = ['!']; a.Pattern = new Regex("[0-9]"); });
            Assert.Contains("disallowed", message);
            Assert.DoesNotContain("expression", message);
        }
    }
}
