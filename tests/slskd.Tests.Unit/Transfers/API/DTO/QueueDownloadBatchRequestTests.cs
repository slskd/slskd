using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using slskd.Transfers.API;
using Xunit;

namespace slskd.Tests.Unit.Transfers.API.DTO;

public class QueueDownloadBatchRequestTests
{
    private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(obj, new ValidationContext(obj), results, validateAllProperties: true);
        return (isValid, results);
    }

    private static QueueDownloadBatchRequest ValidRequest() => new()
    {
        Username = "testuser",
        Files = [new EnqueueDownloadBatchItem { Filename = "music.mp3", Size = 0 }],
    };

    public class Id_Field
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void Omitted_Passes()
        {
            var (isValid, _) = Validate(ValidRequest() with { Id = null });
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-guid")]
        [InlineData("b5bde3d1-4aba-4d64-b64a")]
        public void NonGuid_Fails(string value)
        {
            var (isValid, results) = Validate(ValidRequest() with { Id = value });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Equal("The Id field must be a valid GUID/UUID", results[0].ErrorMessage);
        }

        [Theory]
        [InlineData("b5bde3d1-4aba-4d64-b64a-7a00a3ce5e1b")]
        [InlineData("B5BDE3D14ABA4D64B64A7A00A3CE5E1B")]
        [InlineData("{b5bde3d1-4aba-4d64-b64a-7a00a3ce5e1b}")]
        public void ValidGuid_Passes(string value)
        {
            var (isValid, _) = Validate(ValidRequest() with { Id = value });
            Assert.True(isValid);
        }
    }

    public class SearchId_Field
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void Omitted_Passes()
        {
            var (isValid, _) = Validate(ValidRequest() with { SearchId = null });
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-guid")]
        [InlineData("b5bde3d1-4aba-4d64-b64a")]
        public void NonGuid_Fails(string value)
        {
            var (isValid, results) = Validate(ValidRequest() with { SearchId = value });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Equal("The SearchId field must be a valid GUID/UUID", results[0].ErrorMessage);
        }

        [Theory]
        [InlineData("b5bde3d1-4aba-4d64-b64a-7a00a3ce5e1b")]
        [InlineData("B5BDE3D14ABA4D64B64A7A00A3CE5E1B")]
        [InlineData("{b5bde3d1-4aba-4d64-b64a-7a00a3ce5e1b}")]
        public void ValidGuid_Passes(string value)
        {
            var (isValid, _) = Validate(ValidRequest() with { SearchId = value });
            Assert.True(isValid);
        }
    }

    public class Username_Field
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void Omitted_Fails()
        {
            var (isValid, results) = Validate(ValidRequest() with { Username = null });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "The Username field is required.");
        }

        [Fact]
        public void Empty_Fails()
        {
            var (isValid, results) = Validate(ValidRequest() with { Username = "" });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "The Username field is required.");
        }

        [Fact]
        public void Whitespace_Fails()
        {
            var (isValid, results) = Validate(ValidRequest() with { Username = "   " });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "The Username field is required.");
        }

        [Fact]
        public void OneChar_Passes()
        {
            var (isValid, _) = Validate(ValidRequest() with { Username = "a" });
            Assert.True(isValid);
        }

        [Fact]
        public void FiveHundredChars_Passes()
        {
            var (isValid, _) = Validate(ValidRequest() with { Username = new string('a', 500) });
            Assert.True(isValid);
        }

        [Fact]
        public void FiveHundredOneChars_Fails()
        {
            var (isValid, results) = Validate(ValidRequest() with { Username = new string('a', 501) });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Contains("between 1 and 500 characters", results[0].ErrorMessage);
        }
    }

    public class Files_Field
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void Omitted_Fails_WithRequiredMessage()
        {
            var (isValid, results) = Validate(ValidRequest() with { Files = null });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Equal("The Files field is required.", results[0].ErrorMessage);
        }

        [Fact]
        public void EmptyList_Fails_WithMinLengthMessage()
        {
            var (isValid, results) = Validate(ValidRequest() with { Files = [] });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Contains("minimum length of '1'", results[0].ErrorMessage);
        }
    }

    public class Files_Item
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void NullFilename_Fails()
        {
            var (isValid, results) = Validate(new EnqueueDownloadBatchItem { Filename = null, Size = 0 });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "The Filename field is required.");
        }

        [Fact]
        public void EmptyFilename_Fails()
        {
            var (isValid, results) = Validate(new EnqueueDownloadBatchItem { Filename = "", Size = 0 });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "The Filename field is required.");
        }

        [Fact]
        public void WhitespaceFilename_Fails()
        {
            var (isValid, results) = Validate(new EnqueueDownloadBatchItem { Filename = "   ", Size = 0 });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage == "The Filename field is required.");
        }

        [Fact]
        public void ValidFilename_Passes()
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = "music.mp3", Size = 0 });
            Assert.True(isValid);
        }

        [Fact]
        public void NegativeSize_Fails()
        {
            var (isValid, results) = Validate(new EnqueueDownloadBatchItem { Filename = "music.mp3", Size = -1 });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Equal("The field Size must be between 0 and 9.223372036854776E+18.", results[0].ErrorMessage);
        }

        [Fact]
        public void ZeroSize_Passes()
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = "music.mp3", Size = 0 });
            Assert.True(isValid);
        }

        [Fact]
        public void PositiveSize_Passes()
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = "music.mp3", Size = 1 });
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("@foo/bar/../baz/file.mp3")]
        [InlineData("@foo/bar/./baz/file.mp3")]
        [InlineData("@foo\\bar\\..\\baz\\file.mp3")]
        public void TraversalSegmentInFilename_Fails(string value)
        {
            var (isValid, results) = Validate(new EnqueueDownloadBatchItem { Filename = value, Size = 0 });
            Assert.False(isValid);
            Assert.Contains(results, r => r.ErrorMessage != null && r.ErrorMessage.Contains("traversal segments"));
        }

        [Theory]
        [InlineData("@foo/bar/baz/file.mp3")]
        [InlineData("@foo/bar/baz/file..ext")]
        [InlineData("@foo\\bar\\baz\\file.mp3")]
        public void NonTraversalFilename_Passes(string value)
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = value, Size = 0 });
            Assert.True(isValid);
        }
    }

    public class Options_Destination
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void Omitted_Passes()
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchOptions { Destination = null });
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("subdir")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("Artist\\Album")]
        public void RelativePath_Passes(string value)
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchOptions { Destination = value });
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("C:\\Music")]           // Windows absolute
        [InlineData("C:/Music")]            // Windows absolute, forward slash
        [InlineData("\\\\server\\share")]   // UNC
        public void AbsolutePath_Fails_Windows(string value)
        {
            if (System.OperatingSystem.IsWindows())
            {
                var (isValid, results) = Validate(new EnqueueDownloadBatchOptions { Destination = value });
                Assert.False(isValid);
                Assert.Single(results);
                Assert.Contains("must be a relative path", results[0].ErrorMessage);
            }
        }

        [Theory]
        [InlineData("/music")]              // Unix absolute
        [InlineData("/home/user/Music")]    // Unix absolute, deep
        [InlineData("//server/share")]     // UNC
        public void AbsolutePath_Fails_Non_Windows(string value)
        {
            if (!System.OperatingSystem.IsWindows())
            {
                var (isValid, results) = Validate(new EnqueueDownloadBatchOptions { Destination = value });
                Assert.False(isValid);
                Assert.Single(results);
                Assert.Contains("must be a relative path", results[0].ErrorMessage);
            }
        }

        [Theory]
        [InlineData("../escape")]           // traversal prefix
        [InlineData("./music")]             // current-dir prefix
        [InlineData("music/../other")]      // traversal in middle
        [InlineData("sub/../../escape")]    // double traversal
        [InlineData("music\\..")]           // traversal via backslash
        public void TraversalSegment_Fails(string value)
        {
            var (isValid, results) = Validate(new EnqueueDownloadBatchOptions { Destination = value });
            Assert.False(isValid);
            Assert.Single(results);
            Assert.Contains("traversal segments", results[0].ErrorMessage);
        }
    }
}
