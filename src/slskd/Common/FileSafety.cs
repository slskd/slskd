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
using System.Runtime.InteropServices;

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
    public static string CombineSafely(string root, params string[] segments) => CombineSafely(root, os: null, segments: segments);

    /// <summary>
    ///     An alternative to <see cref="Path.Combine(string, string)"/> that disallows rooted segments in the second or
    ///     subsequent position, and disallows segments containing path traversal characters "." and "..".
    /// </summary>
    /// <param name="root">The root directory.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <param name="segments">The segments to append.</param>
    /// <returns>The combined path.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the root, list of segments, or one or more segments is null.</exception>
    /// <exception cref="ArgumentException">Thrown if any of the specified segments is rooted, or if any contain path traversal characters.</exception>
    public static string CombineSafely(string root, OSPlatform? os, params string[] segments)
    {
        // ensure no inputs are null. matches the behavior of Path.Combine()
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentNullException(nameof(root), $"Specified root directory is null or consists only of whitespace");
        }

        if (ContainsTraversalSegments(root))
        {
            throw new ArgumentException("Specified root directory contains path traversal segments", nameof(root));
        }

        if (segments is null || segments.Any(s => s is null))
        {
            throw new ArgumentNullException(nameof(segments), $"One or more segments is null");
        }

        var isWindows = os.HasValue ? os.Value == OSPlatform.Windows : OperatingSystem.IsWindows();

        foreach (var segment in segments)
        {
            // if any segment is rooted (leading slash), Path.Combine drops everyting up to that segment
            // and it becomes the new root. example: Path.Combine("/home/users/foo", "/etc") results in "/etc"
            // not what we want!
            if (IsPathAbsolute(segment, os))
            {
                throw new ArgumentException($"Absolute paths are not permitted in segments: '{segment}'");
            }

            if (isWindows)
            {
                if (segment.Contains(':'))
                {
                    throw new ArgumentException($"Colons are not permitted in segments: '{segment}'");
                }

                // on Windows, a segment beginning with a slash is considered 'drive rooted' and something like
                // "\foo" translates to "C:\foo" when passed to Path.Combine()
                if (segment.StartsWith('\\') || segment.StartsWith('/'))
                {
                    throw new ArgumentException($"Drive-relative segments or those containing leading slashes are not permitted in segments: '{segment}'");
                }
            }

            var parts = segment.Split(Path.DirectorySeparatorChar, '\\', '/');

            // ensure the segments we're combining don't contain traversal segments "." or ".."
            // untrusted input could use this to "break out" of the root directory and access sensitive areas
            if (parts.Any(s => s is "." or ".."))
            {
                throw new ArgumentException($"Path traversal is not permitted in segments: '{segment}'");
            }
        }

        var combined = Path.Combine(root, Path.Combine(segments));

        // this is a backstop to catch any condition we haven't thought of; it makes sure that the resulting
        // path is actually rooted in the root we provided; if not the input was successful in traversal
        if (!Path.GetFullPath(combined).StartsWith(Path.GetFullPath(root)))
        {
            throw new ArgumentException($"Path traversal detected in combined path '{combined}', which is not allowed");
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
    ///     Returns a value indicating whether the specified <paramref name="path"/> is absolute (rooted) on the current
    ///     operating system.
    /// </summary>
    /// <remarks>
    ///     This is necessary because the base library is opaque and untestable in a cross-platform way, so we are
    ///     implementing the nuclear option and doing it ourselves.
    /// </remarks>
    /// <param name="path">The path to check.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>True if the path is absolute, false otherwise.</returns>
    public static bool IsPathAbsolute(string path, OSPlatform? os = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (os.HasValue ? os.Value == OSPlatform.Windows : OperatingSystem.IsWindows())
        {
            // Windows drive-letter path: X:\ or X:/
            // X:foo is relative on windows
            if (path.Length >= 3
                && char.IsAsciiLetter(path[0])
                && path[1] == ':'
                && (path[2] == '\\' || path[2] == '/'))
            {
                return true;
            }

            // UNC path \\server\share
            if (path.StartsWith("\\\\"))
            {
                return true;
            }

            return false;
        }

        // Unix/Linux/macOS absolute paths and UNC paths with forward slashes start with /
        if (path.StartsWith('/'))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Returns a value indicating whether the specified <paramref name="path"/> is relative on the current
    ///     operating system.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>True if the path is relative, false otherwise.</returns>
    public static bool IsPathRelative(string path, OSPlatform? os = null) => !IsPathAbsolute(path, os);
}