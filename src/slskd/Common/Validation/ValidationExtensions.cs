namespace slskd.Validation
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    public static class ValidationExtensions
    {
        public static string GetResultView(this ValidationResult result, string message)
        {
            var view = result.GetResultView(0).ToList();

            if (view.Count > 0 && !string.IsNullOrEmpty(message))
            {
                view[0] = message;
            }

            return string.Join("\n", view);
        }

        private static IEnumerable<string> GetResultView(this ValidationResult result, int depth = 0)
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
    }
}
