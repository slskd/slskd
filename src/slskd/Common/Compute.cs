// <copyright file="Compute.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Computational functions.
    /// </summary>
    public static class Compute
    {
        public static (int Delay, int Jitter) ExponentialBackoffDelay(int iteration, int maxDelayInMilliseconds = int.MaxValue)
        {
            iteration = Math.Min(100, iteration);

            var computedDelay = Math.Floor((Math.Pow(2, iteration) - 1) / 2) * 1000;
            var clampedDelay = (int)Math.Min(computedDelay, maxDelayInMilliseconds);

            var jitter = new Random().Next(1000);

            return (clampedDelay, jitter);
        }

        public static string Sha1Hash(string str)
        {
            using var sha1 = new SHA1Managed();
            return BitConverter.ToString(sha1.ComputeHash(Encoding.UTF8.GetBytes(str))).Replace("-", string.Empty);
        }

        public static string MaskHash(string str)
        {
            var hash = Sha1Hash(str);
            hash = Convert.ToBase64String(Encoding.UTF8.GetBytes(hash));
            hash = Regex.Replace(hash, @"[\d=]", string.Empty).ToLowerInvariant();

            // in some very unlucky circumstances, the sha1 might end up being all or mostly numbers.
            // if this is the case the resulting hash could be fewer than the 5 characters we need,
            // so copy it until we get to 5
            while (hash.Length < 5)
            {
                hash += hash;
            }

            return $"@@{hash.Substring(0, 5)}";
        }
    }
}