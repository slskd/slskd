using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using static slskd.Options;

namespace slskd.Tests.Unit.Core.Options;

using CorsOptions = WebOptions.CorsOptions;

public class CorsOptionsTests
{
    private static List<ValidationResult> Validate(CorsOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }

    public class AllowedOrigins
    {
        [Fact]
        public void Null_origins_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = null };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Empty_origins_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = [] };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Single_wildcard_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = ["*"] };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Multiple_wildcards_produces_error()
        {
            var options = new CorsOptions { AllowedOrigins = ["*", "*"] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("only one wildcard", results[0].ErrorMessage);
        }

        [Theory]
        [InlineData("http://localhost:3000")]
        [InlineData("https://example.com")]
        [InlineData("https://sub.example.com")]
        [InlineData("https://example.com:8443")]
        [InlineData("http://192.168.1.10:5030")]
        [InlineData("HTTPS://EXAMPLE.COM")]
        public void Valid_origin_produces_no_errors(string origin)
        {
            var options = new CorsOptions { AllowedOrigins = [origin] };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Wildcard_and_valid_origin_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = ["*", "https://example.com"] };
            Assert.Empty(Validate(options));
        }

        [Theory]
        [InlineData("https://*.example.com")]
        [InlineData("https://example.*")]
        public void Origin_containing_wildcard_produces_error(string origin)
        {
            var options = new CorsOptions { AllowedOrigins = [origin] };
            var results = Validate(options);
            Assert.NotEmpty(results);
            Assert.Contains(results, r => r.ErrorMessage.Contains(origin) && r.ErrorMessage.Contains("wildcard"));
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("example.com")]
        [InlineData("null")]
        [InlineData("")]
        [InlineData(" ")]
        public void Invalid_uri_produces_error(string origin)
        {
            var options = new CorsOptions { AllowedOrigins = [origin] };
            Assert.NotEmpty(Validate(options));
        }

        [Theory]
        [InlineData("ftp://example.com")]
        [InlineData("ws://example.com")]
        [InlineData("file:///c:/foo")]
        public void Non_http_scheme_produces_error(string origin)
        {
            var options = new CorsOptions { AllowedOrigins = [origin] };
            var results = Validate(options);
            Assert.Contains(results, r => r.ErrorMessage.Contains("HTTP or HTTPS"));
        }

        [Theory]
        [InlineData("https://example.com/")]
        [InlineData("https://example.com/app")]
        [InlineData("https://example.com?foo=bar")]
        [InlineData("https://example.com#foo")]
        [InlineData("https://example.com:443")]
        [InlineData("http://example.com:80")]
        [InlineData("https://alice@example.com")]
        [InlineData("https://alice:secret@example.com")]
        [InlineData("https://example.com@evil.com")]
        public void Origin_that_never_matches_a_browser_request_produces_error(string origin)
        {
            var options = new CorsOptions { AllowedOrigins = [origin] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains(origin, results[0].ErrorMessage);
        }

        [Fact]
        public void Malformed_origin_error_suggests_browser_origin()
        {
            var options = new CorsOptions { AllowedOrigins = ["https://example.com/"] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.EndsWith("https://example.com", results[0].ErrorMessage);
        }

        [Fact]
        public void Internationalized_origin_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = ["https://münchen.de"] };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void Multiple_invalid_origins_each_produce_an_error()
        {
            var options = new CorsOptions { AllowedOrigins = ["https://example.com/", "ftp://example.com"] };
            Assert.Equal(2, Validate(options).Count);
        }

        [Fact]
        public void Mixed_valid_and_invalid_origins_only_errors_on_invalid()
        {
            var options = new CorsOptions { AllowedOrigins = ["https://example.com", "https://other.com/"] };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("https://other.com/", results[0].ErrorMessage);
        }
    }

    public class AllowCredentials
    {
        [Fact]
        public void Credentials_with_null_origins_produces_error()
        {
            var options = new CorsOptions { AllowedOrigins = null, AllowCredentials = true };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("Credentials", results[0].ErrorMessage);
        }

        [Fact]
        public void Credentials_with_empty_origins_produces_error()
        {
            var options = new CorsOptions { AllowedOrigins = [], AllowCredentials = true };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("Credentials", results[0].ErrorMessage);
        }

        [Fact]
        public void Credentials_with_wildcard_origin_produces_error()
        {
            var options = new CorsOptions { AllowedOrigins = ["*"], AllowCredentials = true };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("Credentials", results[0].ErrorMessage);
        }

        [Fact]
        public void Credentials_with_wildcard_and_specific_origin_produces_error()
        {
            var options = new CorsOptions { AllowedOrigins = ["*", "https://example.com"], AllowCredentials = true };
            var results = Validate(options);
            Assert.Single(results);
            Assert.Contains("Credentials", results[0].ErrorMessage);
        }

        [Fact]
        public void Credentials_with_specific_origins_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = ["https://example.com", "http://localhost:3000"], AllowCredentials = true };
            Assert.Empty(Validate(options));
        }

        [Fact]
        public void No_credentials_with_null_origins_produces_no_errors()
        {
            var options = new CorsOptions { AllowedOrigins = null, AllowCredentials = false };
            Assert.Empty(Validate(options));
        }
    }
}
