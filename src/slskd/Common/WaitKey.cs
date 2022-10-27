// <copyright file="WaitKey.cs" company="slskd Team">
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

    /// <summary>
    ///     Uniquely identifies a Wait.
    /// </summary>
    public sealed class WaitKey : IEquatable<WaitKey>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="WaitKey"/> class.
        /// </summary>
        /// <param name="tokenParts">The parts which make up the key.</param>
        public WaitKey(params object[] tokenParts)
        {
            TokenParts = tokenParts;
            Token = string.Join(":", TokenParts);
        }

        /// <summary>
        ///     Gets the wait token.
        /// </summary>
        public string Token { get; }

        /// <summary>
        ///     Gets the parts which make up the key.
        /// </summary>
        public object[] TokenParts { get; }

        public static bool operator !=(WaitKey lhs, WaitKey rhs)
        {
            return !lhs.Equals(rhs);
        }

        public static bool operator ==(WaitKey lhs, WaitKey rhs)
        {
            return lhs.Equals(rhs);
        }

        /// <summary>
        ///     Compares the specified <paramref name="obj"/> to this instance.
        /// </summary>
        /// <param name="obj">The object to which to compare.</param>
        /// <returns>A value indicating whether the specified object is equal to this instance.</returns>
        public override bool Equals(object obj)
        {
            try
            {
                return Equals((WaitKey)obj);
            }
            catch (InvalidCastException)
            {
                return false;
            }
        }

        /// <summary>
        ///     Compares the specified <paramref name="other"/> WaitKey to this instance.
        /// </summary>
        /// <param name="other">The WaitKey to which to compare.</param>
        /// <returns>A value indicating whether the specified WaitKey is equal to this instance.</returns>
        public bool Equals(WaitKey other)
        {
            return Token == other.Token;
        }

        /// <summary>
        ///     Returns the hash code of this instance.
        /// </summary>
        /// <returns>The hash code of this instance.</returns>
        public override int GetHashCode()
        {
#if NETSTANDARD2_0
            return string.IsNullOrEmpty(Token) ? 0 : Token.GetHashCode();
#else
            return string.IsNullOrEmpty(Token) ? 0 : Token.GetHashCode(StringComparison.InvariantCulture);
#endif
        }

        /// <summary>
        ///     Returns the string representation of the key.
        /// </summary>
        /// <returns>The string representation of the key.</returns>
        public override string ToString()
        {
            return Token;
        }
    }
}