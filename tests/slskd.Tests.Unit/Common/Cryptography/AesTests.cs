namespace slskd.Tests.Unit.Common.Cryptography
{
    using System.Text;
    using slskd.Cryptography;
    using Xunit;

    public class AesTests
    {
        [Fact]
        public void Generates_Random_Keys_Of_Expected_Length()
        {
            var key = Aes.GenerateRandomKey();
            Assert.Equal(48, key.Length);
        }

        [Fact]
        public void Encrypts_And_Decrypts()
        {
            var plainText = "hello, world!";
            var plainBytes = Encoding.UTF8.GetBytes(plainText);

            var key = Aes.GenerateRandomKey();

            var encryptedBytes = Aes.Encrypt(plainBytes, key);

            var decryptedBytes = Aes.Decrypt(encryptedBytes, key);

            var decryptedText = Encoding.UTF8.GetString(decryptedBytes);

            Assert.Equal(plainText, decryptedText);
        }
    }
}
