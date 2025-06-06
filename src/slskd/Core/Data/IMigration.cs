// <copyright file="IMigration.cs" company="slskd Team">
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
///     Applies database migrations.
/// </summary>
public interface IMigration
{
    /// <summary>
    ///     Determines whether the migration needs to be applied.
    /// </summary>
    /// <remarks>
    ///     This method MUST be read-only and should contain no side effects.
    /// </remarks>
    /// <returns>A value indicating whether the migration needs to be applied.</returns>
    bool NeedsToBeApplied();

    /// <summary>
    ///    Applies the migration.
    /// </summary>
    void Apply();
}