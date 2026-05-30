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
using System.Text.RegularExpressions;

/// <summary>
///     Utility functions to help safely work with paths from untrusted sources.
/// </summary>
public static class FileSafety
{
    /// <summary>
    ///     Invalid filename characters on Unix-like operating systems (Linux, OSX, FreeBSD, etc).
    /// </summary>
    public static readonly char[] InvalidFileNameCharactersOnUnix = ['\0', '/'];

    /// <summary>
    ///     Invalid filename characters on Windows.
    /// </summary>
    public static readonly char[] InvalidFileNameCharactersOnWindows =
    [
        '\"', '<', '>', '|', '\0',
        (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
        (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
        (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
        (char)31, ':', '*', '?', '\\', '/',
    ];

    private static readonly Regex DriveRootRegex = new(@"^[a-zA-Z]:[/\\]?", RegexOptions.Compiled); // C: or C:\
    private static readonly Regex UncRootRegex = new(@"^[/\\]{2}[^/\\]+[/\\]?", RegexOptions.Compiled); // \\server[\] or //server[/], any slash variants
    private static readonly Regex SoulseekQtRootRegex = new(@"^@@[a-zA-Z0-9]{5,}[\/\\]", RegexOptions.Compiled); // @@abcde[\|/], a Soulseek Qt share prefix

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

    /// <summary>
    ///     Returns the filename from the specified <paramref name="path"/>, properly
    ///     handling both forward and backslashes and removing invalid characters.
    /// </summary>
    /// <param name="path">The path from which to extract the filename.</param>
    /// <param name="sanitize">An optional value indicating that invalid characters should not be replaced.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The extracted filename.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the specified path is null.</exception>
    public static string GetFileNameSafely(string path, bool sanitize = true, OSPlatform? os = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.EndsWith('/') || path.EndsWith('\\'))
        {
            return null;
        }

        return LocalizePath(path, os)
            .Split(Path.DirectorySeparatorChar)
            .TakeLast(1)
            .Select(s => sanitize == true ? SanitizePathSegment(s, os: os) : s)
            .Single();
    }

    /// <summary>
    ///     Returns the full directory name from the specified <paramref name="path"/>, properly handling both
    ///     forward and backslashes, removing Windows drive, UNC and Soulseek QT root segments and sanitizing
    ///     each of the remaining segments.
    /// </summary>
    /// <param name="path">The path from which to extract the directory name.</param>
    /// <param name="sanitize">An optional value indicating that path segments should not be sanitized.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The directory name.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the specified path is null.</exception>
    public static string GetDirectoryNameSafely(string path, bool sanitize = true, OSPlatform? os = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        var local = LocalizePath(path, os: os);

        return string.Join(Path.DirectorySeparatorChar, local
            .Split(Path.DirectorySeparatorChar)
            .SkipLast(1)
            .Select(s => sanitize == true ? SanitizePathSegment(s, os: os) : s));
    }

    /// <summary>
    ///     Converts the given path to the local format (normalizes path separators to Path.DirectorySeparatorChar).
    /// </summary>
    /// <param name="path">The path to convert.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The converted path.</returns>
    public static string LocalizePath(string path, OSPlatform? os = null)
    {
        os ??= Compute.OSPlatform();

        var sep = os.Value == OSPlatform.Windows ? '\\' : '/';

        return path.Replace('\\', sep).Replace('/', sep);
    }

    /// <summary>
    ///     Sanitizes the specified <paramref name="filename"/> (or string intended to be used as or as part of a filename)
    ///     to make it suitable and safe on the local operating system by stripping all slashes (forward or back) and
    ///     replacing any invalid characters with the specified <paramref name="replacement"/>.
    /// </summary>
    /// <param name="filename">The filename (or string intended to be used as/part of one).</param>
    /// <param name="replacement">The character to substitute for invalid characters.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The sanitized filename.</returns>
    public static string SanitizeFilename(string filename, char replacement = '_', OSPlatform? os = null)
    {
        if (replacement is '/' or '\\')
        {
            throw new ArgumentException($"The provided replacement character '{replacement}' is invalid in filenames");
        }

        os ??= Compute.OSPlatform();

        var characters = os.Value == OSPlatform.Windows ? InvalidFileNameCharactersOnWindows : InvalidFileNameCharactersOnUnix;

        if (characters.Contains(replacement))
        {
            throw new ArgumentException($"The provided replacement character '{replacement}' is invalid on {os}");
        }

        var sanitized = filename;

        foreach (var c in characters)
        {
            sanitized = sanitized.Replace(c, replacement);
        }

        // regardless of which OS and what characters are disallowed on it, we don't allow slashes in filenames
        // (or strings that will be used as filenames), otherwise we should/would have used SanitizePath()
        return sanitized
            .Replace('/', replacement)
            .Replace('\\', replacement);
    }

    /// <summary>
    ///     Sanitizes the specified <paramref name="segment"/> to make it suitable and safe on the local operating
    ///     system by stripping all slashes (forward or back) and replacing any invalid characters with the specified
    ///     <paramref name="replacement"/>.
    /// </summary>
    /// <param name="segment">The path segment.</param>
    /// <param name="replacement">The character to substitute for invalid characters.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The sanitized path segment.</returns>
    public static string SanitizePathSegment(string segment, char replacement = '_', OSPlatform? os = null)
    {
        var sanitized = SanitizeFilename(segment, replacement, os);

        // is is possible for the result of sanitization to produce path traversal strings,
        // which would result in traversal when combined (or throw an error because of CombineSafely)
        // this can happen either because the segment is already periods, or because the replacement
        // character is a period and the input one or two invalid characters.
        // if this happens we follow the principle of least surprise and return an empty string
        // avoid using a period for replacement (don't let users choose) and we won't have this problem
        if (sanitized is "." or "..")
        {
            if (replacement is '.')
            {
                return string.Empty;
            }

            return replacement.ToString();
        }

        return sanitized;
    }

    /// <summary>
    ///     Sanitizes the specified <paramref name="path"/> to make it suitable and safe on the local operating system
    ///     by converting to the correct slashes, dropping a root (if present), replacing any invalid characters with
    ///     the specified <paramref name="replacement"/>, and by dropping any path traversal segments.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="replacement">The character to substitute for invalid characters.</param>
    /// <param name="retainRoot">An optional value indicating whether to keep the path's root, if present.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The sanitized path.</returns>
    public static string SanitizePath(string path, char replacement = '_', bool retainRoot = false, OSPlatform? os = null)
    {
        os ??= Compute.OSPlatform();
        var sep = os.Value == OSPlatform.Windows ? '\\' : '/';

        // flip slashes the correct way
        path = LocalizePath(path, os);

        if (!retainRoot)
        {
            // strip C:\ or //server, if present (regardless of slash variant, etc)
            path = DriveRootRegex.Replace(path, string.Empty);
            path = UncRootRegex.Replace(path, string.Empty);

            // strip @@abcde prefixes used by SoulseekQt to obscure paths
            path = SoulseekQtRootRegex.Replace(path, string.Empty);
        }

        // for each segment, drop nulls (created by double slashes), sanitize, and replace traversal strings
        var segments = path
            .Split(sep)
            .Select(s => SanitizeFilename(s, replacement, os))
            .Select(s => s is "." or ".." ? replacement.ToString() : s);

        return string.Join(sep, segments);
    }

    /// <summary>
    ///     Removes the root of the specified <paramref name="path"/>, including Window's drive prefix (e.g. C:\),
    ///     UNC paths on any OS (e.g. \\server on Windows, //server on Linux), and drops the hashed prefix added by
    ///     Soulseek Qt to hide the path on disk (e.g. @@abcde).
    /// </summary>
    /// <param name="path">The path to strip.</param>
    /// <param name="os">An optional operating system override, for testing.</param>
    /// <returns>The path with the root stripped, if one was present.</returns>
    public static string StripPathRoot(string path, OSPlatform? os = null)
    {
        // flip slashes the correct way
        path = LocalizePath(path, os);

        // strip C:\ or //server, if present (regardless of slash variant)
        path = DriveRootRegex.Replace(path, string.Empty);
        path = UncRootRegex.Replace(path, string.Empty);

        // strip @@abcde prefixes used by SoulseekQt to obscure paths
        path = SoulseekQtRootRegex.Replace(path, string.Empty);

        return path;
    }
}