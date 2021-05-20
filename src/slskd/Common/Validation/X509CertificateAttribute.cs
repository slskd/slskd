namespace slskd.Validation
{
    using System.ComponentModel.DataAnnotations;
    using slskd.Common.Cryptography;
    using static slskd.Options.WebOptions.HttpsOptions;

    public class X509CertificateAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null)
            {
                var cert = (CertificateOptions)value;

                System.Console.WriteLine($"pass: {cert.Password} {string.IsNullOrEmpty(cert.Password)}");

                if (!string.IsNullOrEmpty(cert.Pfx) && !X509.TryValidate(cert.Pfx, cert.Password, out var certResult))
                {
                    return new ValidationResult($"Invalid HTTPs certificate: {certResult}");
                }
            }

            return ValidationResult.Success;
        }
    }
}