// <copyright file="FileExtensions.cs" company="slskd Team">
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

namespace slskd.Files;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     Extension methods related to <see cref="Files"/>.
/// </summary>
public static class FileExtensions
{
    /// <summary>
    ///     Converts the specified <paramref name="permissions"/> string into an instance of <see cref="UnixFileMode"/>.
    /// </summary>
    /// <param name="permissions">A 3 or 4 character string consisting of only 0-7, matching a Unix file permission (e.g. one used with 'chmod').</param>
    /// <returns>The converted UnixFileMode.</returns>
    /// <exception cref="ArgumentException">Thrown if the specified <paramref name="permissions"/> string is null or consists of only whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified <paramref name="permissions"/> are not a 3 or 4 character string consisting of only 0-7.</exception>
    public static UnixFileMode ToUnixFileMode(this string permissions)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(permissions, nameof(permissions));

        var regEx = new Regex("^[0-7]{3,4}$", RegexOptions.Compiled);

        if (!regEx.IsMatch(permissions))
        {
            throw new ArgumentOutOfRangeException($"The provided permissions are not a valid, expected 0-7 repeated 3 or 4 times (provided: {permissions})");
        }

        var opts = permissions
            .Select(x => int.Parse(x.ToString()))
            .Reverse()
            .ToArray();

        int mode = 0;

        for (var i = 0; i < opts.Length; i++)
        {
            mode |= opts[i] << (i * 3);
        }

        return (UnixFileMode)mode;
    }

#nullable enable
    public static FileInfo? TryFollowSymlink(this FileInfo fileInfo)
    {
        try
        {
            return fileInfo.FollowSymlink();
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static FileInfo FollowSymlink(this FileInfo fileInfo)
    {
        FileSystemInfo fileSystemInfo = fileInfo;
        fileSystemInfo = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true) ?? fileSystemInfo;
        return (FileInfo)fileSystemInfo;
    }
#nullable restore
}