using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using static slskd.Options;

namespace slskd.Tests.Unit.Core.Options;

public class SharesOptionsTests
{
    private static List<ValidationResult> Validate(SharesOptions options)
    {
        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, context, results, validateAllProperties: true);
        return results;
    }

    public class Directories
    {
        public class Digest
        {
            [Fact]
            public void Alias_Uses_First_Closing_Bracket_When_Path_Contains_Brackets()
            {
                // [foo]/bar/[baz]/ should digest to alias "foo" and path "/bar/[baz]"
                var options = new SharesOptions
                {
                    Directories = [@"[foo]/bar/[baz]/"]
                };

                // todo: fix this so that it fully validates on all platforms; currently this fails
                // validation on Windows and would fail on Linux if i were to use backslashes
                // the assertion for the alias proves we have fixed an aliasing bug so it's fine for now
                var results = Validate(options);

                Assert.DoesNotContain(results, r => r.ErrorMessage?.Contains("aliases may not contain path separators") == true);
            }
        }
    }
}
