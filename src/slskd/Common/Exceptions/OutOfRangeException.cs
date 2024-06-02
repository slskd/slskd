// <copyright file="OutOfRangeException.cs" company="slskd Team">
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
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    ///     Represents errors that originate when a value falls out of an expected range.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Serializable]
    public class OutOfRangeException : SlskdException
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="OutOfRangeException"/> class.
        /// </summary>
        public OutOfRangeException()
            : base()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="OutOfRangeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public OutOfRangeException(string message)
            : base(message)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="OutOfRangeException"/> class with a specified error message and a
        ///     reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">
        ///     The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no
        ///     inner exception is specified.
        /// </param>
        public OutOfRangeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}