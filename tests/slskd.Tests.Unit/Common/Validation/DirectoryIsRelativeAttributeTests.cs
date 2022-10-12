namespace slskd.Tests.Unit.Common
{
    using slskd.Validation;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Xunit;

    public class DirectoryIsRelativeAttributeTests : DirectoryIsRelativeAttribute
    {
        [Theory]
        [InlineData(@"\home\abc\")]
        [InlineData(@"\\home\abc\")]
        [InlineData(@"C:\home\abc\")]
        public void IsValidReturnsErrorResultWhenPathIsNonRelative(string path)
        {
            //arrange           
            var target = new ValidationTarget { ContentPath = path };
            var validationContext = new ValidationContext(target);
            var validationResults = new List<ValidationResult>();
            var errorMessage = $"The {nameof(target.ContentPath)} field specifies a non-realtive directory path: '{target.ContentPath}'.";

            //act
            var actual = Validator.TryValidateObject(target, validationContext, validationResults, true);

            //asert
            Assert.False(actual);
            Assert.Equal(errorMessage, validationResults[0].ErrorMessage);
        }

        [Theory]
        [InlineData(@"home\abc\")]
        [InlineData(@"..\home\abc\")]
        public void IsValidReturnsSuccessResultWhenPathIsRelative(string path)
        {
            //arrange           
            var target = new ValidationTarget { ContentPath = path };
            var validationContext = new ValidationContext(target);
            var validationResults = new List<ValidationResult>();

            //act
            var actual = Validator.TryValidateObject(target, validationContext, validationResults, true);

            //asert
            Assert.True(actual);
        }

        public class ValidationTarget
        {
            [DirectoryIsRelative]
            public string ContentPath { get; set; }
        }
    }
}
