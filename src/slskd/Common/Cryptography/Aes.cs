// <copyright file="Aes.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Cryptography
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    /// <summary>
    ///     AES utility methods.
    /// </summary>
    public static class Aes
    {
        private static readonly int BlockSizeInBits = 128;
        private static readonly int BlockSizeInBytes = BlockSizeInBits / 8;
        private static readonly int KeySizeInBits = 256;
        private static readonly int KeySizeInBytes = KeySizeInBits / 8;

        public static string Decrypt(string cipherText, string keybase62)
        {
            using var aes = System.Security.Cryptography.Aes.Create();

            aes.KeySize = KeySizeInBits;
            aes.BlockSize = BlockSizeInBits;

            var (key, iv) = DecodeKey(keybase62);

            using var decryptor = aes.CreateDecryptor(key, iv);
            using var memoryStream = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);

            return streamReader.ReadToEnd();
        }

        public static string Encrypt(string plainText, string keybase62)
        {
            using var aes = System.Security.Cryptography.Aes.Create();

            aes.KeySize = KeySizeInBits;
            aes.BlockSize = BlockSizeInBits;

            var (key, iv) = DecodeKey(keybase62);

            using var encryptor = aes.CreateEncryptor(key, iv);
            using var memoryStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write);
            using var streamWriter = new StreamWriter(cryptoStream);

            streamWriter.Write(plainText);
            streamWriter.Close();

            return Convert.ToBase64String(memoryStream.ToArray());
        }

        public static string GenerateRandomBase62Key()
        {
            var (key, iv) = GenerateRandomKey();
            return EncodeKey(key, iv);
        }

        private static (byte[] Key, byte[] IV) DecodeKey(string keybase62)
        {
            var bytes = keybase62.FromBase62();
            var mem = new Memory<byte>(bytes);
            var key = mem.Slice(0, KeySizeInBytes);
            var iv = mem.Slice(KeySizeInBytes, BlockSizeInBytes);

            return (key.ToArray(), iv.ToArray());
        }

        private static string EncodeKey(byte[] key, byte[] iv)
        {
            var bytes = key.Concat(iv).ToArray();
            return bytes.ToBase62();
        }

        private static (byte[] Key, byte[] IV) GenerateRandomKey()
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.KeySize = KeySizeInBits;
            aes.BlockSize = BlockSizeInBits;

            aes.GenerateIV();
            aes.GenerateKey();

            return (aes.Key, aes.IV);
        }
    }
}