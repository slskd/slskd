namespace slskd.Validation
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using slskd.Configuration;

    public static class ValidationExtensions
    {
        public static string GetResultView(this ValidationResult result)
        {
            return string.Join("\n", result.GetResultView(0));
        }

        public static IEnumerable<string> GetResultView(this ValidationResult result, int depth = 0)
        {
            var indent = new string(' ', depth * 2);

            if (result is CompositeValidationResult composite)
            {
                var lines = new[] { indent + result + ":" }.ToList();

                foreach (var child in composite.Results)
                {
                    lines.AddRange(child.GetResultView(depth + 1));
                }

                return lines;
            }
            else
            {
                return new[] { indent + result };
            }
        }

        public static bool TryValidate(this Options options, out CompositeValidationResult result)
        {
            result = null;
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(options, new ValidationContext(options), results, true))
            {
                result = new CompositeValidationResult("Invalid configuration", results);
                return false;
            }

            return true;
        }
    }
}
