namespace slskd.Common.Cryptography
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public class X509
    {
        public static X509Certificate2 Generate(string subject, string password = null, X509KeyStorageFlags x509KeyStorageFlags = X509KeyStorageFlags.MachineKeySet)
        {
            password ??= Guid.NewGuid().ToString();

            using RSA rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                new X500DistinguishedName($"CN={subject}"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var certificate = request.CreateSelfSigned(
                new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                new DateTimeOffset(DateTime.UtcNow.AddDays(36500)));

            return new X509Certificate2(certificate.Export(X509ContentType.Pkcs12, password), password, x509KeyStorageFlags);
        }

        public static bool TryValidate(string fileName, string password, out string result)
        {
            result = null;

            try
            {
                _ = new X509Certificate2(fileName, password);
                return true;
            }
            catch (Exception ex)
            {
                result = ex.Message;
                return false;
            }
        }
    }
}
