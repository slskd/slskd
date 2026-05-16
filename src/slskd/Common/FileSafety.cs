// <copyright file="FileSafety.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd;

using System;
using System.IO;
using System.Linq;

/// <summary>
///     Utility functions to help safely work with paths from untrusted sources.
/// </summary>
public static class FileSafety
{
    /// <summary>
    ///     An alternative to <see cref="Path.Combine(string, string)"/> that disallows rooted segments in the second or
    ///     subsequent position, and disallows segments containing path traversal characters "." and "..".
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="segments">The segments to append.</param>
    /// <returns>The combined path.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the root, list of segments, or one or more segments is null.</exception>
    /// <exception cref="ArgumentException">Thrown if any of the specified segments is rooted, or if any contain path traversal characters.</exception>
    public static string CombineSafely(string root, params string[] segments)
    {
        // ensure no inputs are null. matches the behavior of Path.Combine()
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentNullException(nameof(root), $"Specified root directory is null or consists only of whitespace");
        }

        if (segments is null || segments.Any(s => s is null))
        {
            throw new ArgumentNullException(nameof(segments), $"One or more segments is null");
        }

        foreach (var segment in segments)
        {
            // if any segment is rooted (leading slash), Path.Combine drops everyting up to that segment
            // and it becomes the new root. example: Path.Combine("/home/users/foo", "/etc") results in "/etc"
            // not what we want!
            if (IsPathAbsolute(segment))
            {
                throw new ArgumentException($"Rooted paths are not permitted in segments: '{segment}'");
            }

            var parts = segment.Split(Path.DirectorySeparatorChar, '\\', '/');

            // ensure the segments we're combining don't contain traversal segments "." or ".."
            // untrusted input could use this to "break out" of the root directory and access sensitive areas
            if (parts.Any(s => s is "." or ".."))
            {
                throw new ArgumentException($"Path traversal is not permitted: '{segment}'");
            }
        }

        var combined = Path.Combine(root, Path.Combine(segments));

        // not sure how we could possibly get here, but if we do, throw.
        if (!Path.GetFullPath(combined).StartsWith(Path.GetFullPath(root)))
        {
            throw new ArgumentException($"Path traversal detected in path {combined}, which is not allowed");
        }

        return combined;
    }

    /// <summary>
    ///     Returns a value indicating whether the specified <paramref name="path"/> contains path traversal segments.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if one or more segments is present, false otherwise.</returns>
    public static bool ContainsTraversalSegments(string path) => path?.Split('\\', '/')?.Any(s => s is "." or "..") ?? false;

    /// <summary>
    ///     Returns a value indicating whether the specified <paramref name="path"/> is absolute (rooted), using
    ///     platform-independent rules that recognize both Unix and Windows absolute path formats.
    /// </summary>
    /// <remarks>
    ///     This is necessary because the base library is opaque and untestable in a cross-platform way, so we are
    ///     implementing the nuclear option and merging the logic for Windows and non-Windows.
    /// </remarks>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is absolute, false otherwise.</returns>
    public static bool IsPathAbsolute(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // Unix/Linux/macOS absolute paths and UNC paths with forward slashes start with /
        // Windows root-relative paths and UNC paths start with \
        if (path[0] is '/' or '\\')
        {
            return true;
        }

        // Windows drive-letter paths: X:\ or X:/
        // X:foo is a "drive relative" path so it doesn't _technically_ count, but
        // it's questionable and the user probably intended to root it anyway
        // this matches the built in IsPathRooted but not IsPathFullyQualified
        if (path.Length >= 2
            && char.IsAsciiLetter(path[0])
            && path[1] == ':')
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Returns a value indicating whether the specified <paramref name="path"/>, using
    ///     platform-independent rules that recognize both Unix and Windows absolute path formats.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is relative, false otherwise.</returns>
    public static bool IsPathRelative(string path) => !IsPathAbsolute(path);
}