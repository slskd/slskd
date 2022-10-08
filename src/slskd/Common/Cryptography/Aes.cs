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

        public static byte[] Decrypt(byte[] encryptedBytes, byte[] key)
        {
            using var aes = System.Security.Cryptography.Aes.Create();

            aes.KeySize = KeySizeInBits;
            aes.BlockSize = BlockSizeInBits;

            var (k, iv) = DecodeKey(key);

            using var decryptor = aes.CreateDecryptor(k, iv);
            using var inputStream = new MemoryStream(encryptedBytes);
            using var cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
            using var outputStream = new MemoryStream();

            cryptoStream.CopyTo(outputStream);

            return outputStream.ToArray();
        }

        public static byte[] Encrypt(byte[] plainBytes, byte[] key)
        {
            using var aes = System.Security.Cryptography.Aes.Create();

            aes.KeySize = KeySizeInBits;
            aes.BlockSize = BlockSizeInBits;

            var (k, iv) = DecodeKey(key);

            using var encryptor = aes.CreateEncryptor(k, iv);
            using var outputStream = new MemoryStream();
            using var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);

            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
            cryptoStream.Flush();
            cryptoStream.Close();

            return outputStream.ToArray();
        }

        public static byte[] GenerateRandomKey()
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.KeySize = KeySizeInBits;
            aes.BlockSize = BlockSizeInBits;

            aes.GenerateIV();
            aes.GenerateKey();

            var bytes = aes.Key.Concat(aes.IV).ToArray();
            return bytes;
        }

        private static (byte[] Key, byte[] IV) DecodeKey(byte[] bytes)
        {
            var mem = new Memory<byte>(bytes);
            var key = mem.Slice(0, KeySizeInBytes);
            var iv = mem.Slice(KeySizeInBytes, BlockSizeInBytes);

            return (key.ToArray(), iv.ToArray());
        }
    }
}