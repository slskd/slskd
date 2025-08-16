// <copyright file="ConnectionStringDictionary.cs" company="slskd Team">
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

using System.Collections.Generic;

/// <summary>
///     Provides a lightweight read-only construct to store connection strings keyed by database name.
/// </summary>
public class ConnectionStringDictionary
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConnectionStringDictionary"/> class.
    /// </summary>
    /// <param name="dict">The Dictionary over which to create this read-only copy.</param>
    public ConnectionStringDictionary(Dictionary<string, ConnectionString> dict)
    {
        Strings = dict;
    }

    private Dictionary<string, ConnectionString> Strings { get; } = [];

    public ConnectionString this[string name] => Strings[name];
}