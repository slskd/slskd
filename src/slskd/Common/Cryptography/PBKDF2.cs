namespace slskd.Security
{
    using Microsoft.AspNetCore.Cryptography.KeyDerivation;
    using System.Security.Cryptography;

    /// <summary>
    ///     PBKDF2/RFC 2898 implementation 
    /// </summary>
    public static class PBKDF2
    {
        /// <summary>
        ///     Gets a 256 bit (32 byte) key derived from the specified <paramref name="password"/> using PBKDF2/RFC 2898
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static byte[] GetKey(string password)
        {
            byte[] salt = new byte[16];
            int iterations = 1000;

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, 32);
        }
    }
}
