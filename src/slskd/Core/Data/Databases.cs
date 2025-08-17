// <copyright file="Databases.cs" company="slskd Team">
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

/// <summary>
///     A primitive type for Database names and a static value for each of them. And a list.
/// </summary>
public class Database
{
    public static Database Search { get; } = new Database { Name = nameof(Search).ToLower() };
    public static Database Transfers { get; } = new Database { Name = nameof(Transfers).ToLower() };
    public static Database Messaging { get; } = new Database { Name = nameof(Messaging).ToLower() };
    public static Database Events { get; } = new Database { Name = nameof(Events).ToLower() };
    public static Database[] List { get; } = [Search, Transfers, Messaging, Events];

    public required string Name { get; init; }

    public override string ToString() => Name;
}