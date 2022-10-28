// <copyright file="Pbkdf2.cs" company="slskd Team">
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
    using System.Security.Cryptography;
    using Microsoft.AspNetCore.Cryptography.KeyDerivation;

    /// <summary>
    ///     PBKDF2/RFC 2898 utility methods.
    /// </summary>
    public static class Pbkdf2
    {
        /// <summary>
        ///     Gets a 256 bit (32 byte) key derived from the specified <paramref name="password"/> using PBKDF2/RFC 2898.
        /// </summary>
        /// <param name="password">The password from which to derive the key.</param>
        /// <param name="length">The desired length of the key, in bytes.</param>
        /// <returns>The derived key.</returns>
        public static byte[] GetKey(string password, int length = 32)
        {
            byte[] salt = new byte[16];
            int iterations = 1000;

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, length);
        }
    }
}
