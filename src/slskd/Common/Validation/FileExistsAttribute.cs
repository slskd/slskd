namespace slskd.Validation
{
    using System.ComponentModel.DataAnnotations;
    using System.IO;

    public class FileExistsAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null)
            {
                var file = Path.GetFullPath(value?.ToString());

                if (!string.IsNullOrEmpty(file) && !File.Exists(file))
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field specifies a non-existent file '{file}'.");
                }
            }

            return ValidationResult.Success;
        }
    }
}