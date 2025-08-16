// <copyright file="ConnectionString.cs" company="slskd Team">
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

namespace slskd;

/// <summary>
///     A primitive type to differentiate connection strings from other strings.
/// </summary>
public record ConnectionString
{
    /// <summary>
    ///     Gets the connection string.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    ///     Implicit conversion from string to ConnectionString.
    /// </summary>
    /// <param name="value">The string value.</param>
    public static implicit operator ConnectionString(string value) => new() { Value = value };

    /// <summary>
    ///     Implicit conversion from ConnectionString to string.
    /// </summary>
    /// <param name="connectionString">The ConnectionString instance.</param>
    public static implicit operator string(ConnectionString connectionString) => connectionString.Value;

    /// <summary>
    ///     Returns the string representation of the connection string.
    /// </summary>
    /// <returns>The connection string value.</returns>
    public override string ToString() => Value;
}