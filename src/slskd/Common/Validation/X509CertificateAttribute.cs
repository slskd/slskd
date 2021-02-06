namespace slskd.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;

    public class X509CertificateAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var cert = (Configuration.Options.WebOptions.HttpsOptions.CertificateOptions)value;

            if (string.IsNullOrEmpty(cert.Pfx.ToString()))
            {
                return ValidationResult.Success;
            }

            try
            {
                var x509 = new X509Certificate2(cert.Pfx, cert.Password);
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Invalid certificate: {ex.Message}");
            }

            return ValidationResult.Success;
        }
    }
}