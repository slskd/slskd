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
            var (isValid, _) = Validate(ValidRequest() with { Id = value });
            Assert.False(isValid);
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
            var (isValid, _) = Validate(ValidRequest() with { SearchId = value });
            Assert.False(isValid);
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
            var (isValid, _) = Validate(ValidRequest() with { Username = null });
            Assert.False(isValid);
        }

        [Fact]
        public void Empty_Fails()
        {
            var (isValid, _) = Validate(ValidRequest() with { Username = "" });
            Assert.False(isValid);
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
            var (isValid, _) = Validate(ValidRequest() with { Username = new string('a', 501) });
            Assert.False(isValid);
        }
    }

    public class Files_Item
    {
        private static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
            => QueueDownloadBatchRequestTests.Validate(obj);

        [Fact]
        public void NullFilename_Fails()
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = null, Size = 0 });
            Assert.False(isValid);
        }

        [Fact]
        public void EmptyFilename_Fails()
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = "", Size = 0 });
            Assert.False(isValid);
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
            var (isValid, _) = Validate(new EnqueueDownloadBatchItem { Filename = "music.mp3", Size = -1 });
            Assert.False(isValid);
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
        [InlineData("C:\\Music")]
        [InlineData("/music")]
        [InlineData("\\\\server\\share")]
        public void AbsolutePath_Fails(string value)
        {
            var (isValid, _) = Validate(new EnqueueDownloadBatchOptions { Destination = value });
            Assert.False(isValid);
        }
    }
}
