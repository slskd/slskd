using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using static slskd.Options;

namespace slskd.Tests.Unit.Core.Options;

using BlacklistedOptions = TransfersOptions.GroupsOptions.BlacklistedOptions;

public class BlacklistedOptionsTests
{
    private static List<ValidationResult> Validate(BlacklistedOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }

    public class Patterns
    {
        [Fact]
        public void Valid_regex_produces_no_errors()
        {
            var options = new BlacklistedOptions { Patterns = [".*", "^foo$", @"\d+"] };
            Assert.Empty(Validate(options));
        }

        [Theory]
        [InlineData("[invalid")]
        [InlineData("(unclosed")]
        [InlineData("*noanchor")]
        public void Invalid_regex_produces_error(string pattern)
        {
            var options = new BlacklistedOptions { Patterns = [pattern] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains(pattern, results[0].ErrorMessage);
        }

        [Fact]
        public void Multiple_invalid_regexes_each_produce_an_error()
        {
            var options = new BlacklistedOptions { Patterns = ["[bad", "(also bad"] };
            Assert.Equal(2, Validate(options).Count);
        }

        [Fact]
        public void Mixed_valid_and_invalid_regexes_only_errors_on_invalid()
        {
            var options = new BlacklistedOptions { Patterns = ["valid.*", "[invalid"] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("[invalid", results[0].ErrorMessage);
        }

        [Fact]
        public void Empty_patterns_produces_no_errors()
        {
            var options = new BlacklistedOptions { Patterns = [] };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Null_patterns_produces_no_errors()
        {
            var options = new BlacklistedOptions { Patterns = null };
            Assert.Empty(Validate(options));
        }
    }

    public class Cidrs
    {
        [Theory]
        [InlineData("192.168.1.0/24")]
        [InlineData("10.0.0.0/8")]
        [InlineData("1.2.3.4/32")]
        [InlineData("0.0.0.0/0")]
        public void Valid_cidr_produces_no_errors(string cidr)
        {
            var options = new BlacklistedOptions { Cidrs = [cidr] };
            Assert.Empty(Validate(options));
        }

        [Theory]
        [InlineData("not-a-cidr")]
        [InlineData("999.999.999.999/24")]
        [InlineData("192.168.1.0/33")]
        public void Invalid_cidr_produces_error(string cidr)
        {
            var options = new BlacklistedOptions { Cidrs = [cidr] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains(cidr, results[0].ErrorMessage);
        }

        [Theory]
        [InlineData("::ffff:192.168.1.1")]
        [InlineData("::FFFF:10.0.0.1")]
        [InlineData("::ffff:0:0/96")]
        public void IPv4_mapped_IPv6_address_produces_error(string cidr)
        {
            var options = new BlacklistedOptions { Cidrs = [cidr] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains(cidr, results[0].ErrorMessage);
        }

        [Fact]
        public void Multiple_invalid_cidrs_each_produce_an_error()
        {
            var options = new BlacklistedOptions { Cidrs = ["bad1", "bad2"] };
            Assert.Equal(2, Validate(options).Count);
        }

        [Fact]
        public void Empty_cidrs_produces_no_errors()
        {
            var options = new BlacklistedOptions { Cidrs = [] };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Null_cidrs_produces_no_errors()
        {
            var options = new BlacklistedOptions { Cidrs = null };
            Assert.Empty(Validate(options));
        }
    }
}
