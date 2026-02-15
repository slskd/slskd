// <copyright file="CommonExtensions.cs" company="slskd Team">
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
    using System.Collections;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    /// <summary>
    ///     Common extensions; things not specific to Soulseek or slskd.
    /// </summary>
    public static class CommonExtensions
    {
        /// <summary>
        ///     Times this Task out after the specified TimeSpan.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="task">The task.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="TimeoutException">Thrown when the task times out.</exception>
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                return await task;
            }
            else
            {
                throw new TimeoutException("The operation has timed out.");
            }
        }

        /// <summary>
        ///     Times this Task out after the specified TimeSpan.
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="timeout">The timeout.</param>
        /// <returns>The operation context.</returns>
        /// <exception cref="TimeoutException">Thrown when the task times out.</exception>
        public static async Task TimeoutAfter(this Task task, TimeSpan timeout)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource();

            var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));

            if (completedTask == task)
            {
                timeoutCancellationTokenSource.Cancel();
                await task;
            }
            else
            {
                throw new TimeoutException("The operation has timed out.");
            }
        }

        /// <summary>
        ///     Deeply compares this object with the specified object and returns a list of properties that are different.
        /// </summary>
        /// <param name="left">The left side of the comparison.</param>
        /// <param name="right">The right side of the comparison.</param>
        /// <param name="parentFqn">The root path for recursive calls.</param>
        /// <returns>A list of differences between the two objects.</returns>
        public static IEnumerable<(PropertyInfo Property, string FQN, object Left, object Right)> DiffWith(this object left, object right, string parentFqn = null)
        {
            if (left?.GetType() != right?.GetType())
            {
                throw new InvalidCastException($"Unable to diff types {left?.GetType()} and {right?.GetType()}");
            }

            var differences = new List<(PropertyInfo Property, string FQN, object Left, object Right)>();

            foreach (var prop in left?.GetType().GetProperties())
            {
                var leftVal = prop.GetValue(left);
                var rightVal = prop.GetValue(right);
                var propType = prop.PropertyType;
                var fqn = string.IsNullOrEmpty(parentFqn) ? prop.Name : string.Join(".", parentFqn, prop.Name);

                if (propType.IsArray || (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
                {
                    if (leftVal.ToJson() != rightVal.ToJson())
                    {
                        differences.Add((prop, fqn, leftVal, rightVal));
                    }
                }
                else if (propType.IsPrimitive || propType.IsEnum || Nullable.GetUnderlyingType(propType) != null || new[] { typeof(string), typeof(decimal) }.Contains(propType))
                {
                    if (!Equals(leftVal, rightVal))
                    {
                        differences.Add((prop, fqn, leftVal, rightVal));
                    }
                }
                else
                {
                    differences.AddRange(DiffWith(leftVal, rightVal, fqn));
                }
            }

            return differences;
        }

        /// <summary>
        ///     Makes a best-guess determination of the directory separator character used by a remote system,
        ///     based on the characters present in the specified <paramref name="remoteFilename"/>.
        /// </summary>
        /// <param name="remoteFilename">The fully qualified remote filename to inspect.</param>
        /// <returns>The guessed directory separator.</returns>
        public static char GuessDirectorySeparator(this string remoteFilename)
        {
            // forward slash is a forbidden character on all operating systems; if the specified string contains it,
            // then it is coming from the OS's directory separator
            if (remoteFilename.Contains('/'))
            {
                return '/';
            }

            // if the given string doesn't contain any forward slashes, it either lacks a directory separator at all
            // (potentially impossible?) or it is using backspaces. backspaces are used by all major clients, so
            // this should be a safe assumption.
            return '\\';
        }

        /// <summary>
        ///     Returns the directory name of the given <paramref name="path"/>, using the specified <paramref name="directorySeparator"/>
        ///     to split and join directories and filename.
        /// </summary>
        /// <param name="path">The path for which to return the directory name.</param>
        /// <param name="directorySeparator">The directory separator character.</param>
        /// <returns>The specified path, less the last segment.</returns>
        public static string GetDirectoryName(this string path, char directorySeparator)
        {
            return string.Join(directorySeparator, path.Split(directorySeparator).SkipLast(1));
        }

        /// <summary>
        ///     Returns the directory from the given path, regardless of separator format.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The directory.</returns>
        public static string DirectoryName(this string path)
        {
            return path.GetDirectoryName(path.GuessDirectorySeparator());
        }

        /// <summary>
        ///     Safely disposes this object without throwing if it is already exposed.
        /// </summary>
        /// <param name="obj">The object.</param>
        public static void TryDispose(this IDisposable obj)
        {
            try
            {
                obj?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // no-op; something else beat us to it
            }
        }

        /// <summary>
        ///     Recursively retrieves all properties.
        /// </summary>
        /// <param name="type">The type from which to retrieve properties.</param>
        /// <returns>The list of properties.</returns>
        public static IEnumerable<PropertyInfo> GetPropertiesRecursively(this Type type)
        {
            var props = new List<PropertyInfo>();

            foreach (var prop in type.GetProperties())
            {
                if (prop.PropertyType.IsPrimitive || prop.PropertyType.IsArray || Nullable.GetUnderlyingType(prop.PropertyType) != null || new[] { typeof(string), typeof(decimal) }.Contains(prop.PropertyType))
                {
                    props.Add(prop);
                }
                else
                {
                    props.AddRange(prop.PropertyType.GetPropertiesRecursively());
                }
            }

            return props;
        }

        /// <summary>
        ///     Determines whether the string is a valid regular expression.
        /// </summary>
        /// <param name="pattern">The string to validate.</param>
        /// <returns>A value indicating whether the string is a valid regular expression.</returns>
        public static bool IsValidRegex(this string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            try
            {
                Regex.Match(string.Empty, pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Replaces the first occurrence of <paramref name="phrase"/> in the string with <paramref name="replacement"/>.
        /// </summary>
        /// <param name="str">The string on which to perform the replacement.</param>
        /// <param name="phrase">The phrase or substring to replace.</param>
        /// <param name="replacement">The replacement string.</param>
        /// <returns>The string, with the desired phrase replaced.</returns>
        public static string ReplaceFirst(this string str, string phrase, string replacement)
        {
            int pos = str.IndexOf(phrase);

            if (pos < 0)
            {
                return str;
            }

            return str.Substring(0, pos) + replacement + str.Substring(pos + phrase.Length);
        }

        /// <summary>
        ///     Serializes the JWT.
        /// </summary>
        /// <param name="jwt">The JWT.</param>
        /// <returns>The serialized string.</returns>
        public static string Serialize(this JwtSecurityToken jwt)
            => new JwtSecurityTokenHandler().WriteToken(jwt);

        /// <summary>
        ///     Formats byte to nearest size (KB, MB, etc.)
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <param name="decimalPlaces">The number of decimal places to include.</param>
        /// <returns>The formatted string.</returns>
        public static string SizeSuffix(this double value, int decimalPlaces = 1)
        {
            string[] sizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

            if (value < 0)
            {
                return "-" + SizeSuffix(-value);
            }

            if (value == 0)
            {
                return string.Format("{0:n" + decimalPlaces + "} bytes", 0);
            }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format(
                "{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                sizeSuffixes[mag]);
        }

        /// <summary>
        ///     Returns a "pretty" string representation of the provided Type; specifically, corrects the naming of generic Types
        ///     and appends the type parameters for the type to the name as it appears in the code editor.
        /// </summary>
        /// <param name="type">The type for which the colloquial name should be created.</param>
        /// <returns>A "pretty" string representation of the provided Type.</returns>
        public static string ToColloquialString(this Type type)
        {
            return !type.IsGenericType ? type.Name : type.Name.Split('`')[0] + "<" + string.Join(", ", type.GetGenericArguments().Select(a => a.ToColloquialString())) + ">";
        }

        /// <summary>
        ///     Serializes this object to json.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A string containing the serialized object.</returns>
        public static string ToJson(this object obj) => JsonSerializer.Serialize(obj, GetJsonSerializerOptions());

        /// <summary>
        ///     Serializes this object to yaml.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A string containing the serialized object.</returns>
        public static string ToYaml(this object obj) => new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Serialize(obj);

        /// <summary>
        ///     Deserializes this string from yaml to an object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to which to deserialize the string.</typeparam>
        /// <param name="str">The string to deserialize.</param>
        /// <returns>The new object deserialized from the string.</returns>
        public static T FromYaml<T>(this string str) => new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Deserialize<T>(str);

        /// <summary>
        ///     Converts a fully qualified remote filename to a local filename based in the provided
        ///     <paramref name="baseDirectory"/>, swapping directory characters for those specific to the local OS, removing any
        ///     characters that are invalid for the local OS, and making the path relative to the remote store (including the
        ///     filename and the parent folder).
        /// </summary>
        /// <param name="remoteFilename">The fully qualified remote filename to convert.</param>
        /// <param name="baseDirectory">The base directory for the local filename.</param>
        /// <returns>The converted filename.</returns>
        public static string ToLocalFilename(this string remoteFilename, string baseDirectory)
        {
            return Path.Combine(baseDirectory, remoteFilename.ToLocalRelativeFilename());
        }

        /// <summary>
        ///     Converts the given path to the normalized format (normalizes path separators to backslashes).
        /// </summary>
        /// <remarks>
        ///     Various Soulseek clients (including this one) assume paths to use backslashes, regardless of the host system.
        /// </remarks>
        /// <param name="path">The path to convert.</param>
        /// <returns>The converted path.</returns>
        public static string NormalizePathForSoulseek(this string path)
        {
            return path.Replace('/', '\\');
        }

        /// <summary>
        ///     Converts the given path to the local format (normalizes path separators to Path.DirectorySeparatorChar).
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>The converted path.</returns>
        public static string LocalizePath(this string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }

        /// <summary>
        ///     <see cref="Path.GetDirectoryName(string)"/>, but for paths normalized to use backslashes.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetNormalizedDirectoryName(this string path)
        {
            return string.Join('\\', path.Split('\\').SkipLast(1));
        }

        /// <summary>
        ///     <see cref="Path.GetFileName(string)"/>, but for paths normalized to use backslashes.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetNormalizedFileName(this string path)
        {
            return path.Split('\\').TakeLast(1).First();
        }

        /// <summary>
        ///     Converts a fully qualified remote filename to a local filename, swapping directory characters for those specific
        ///     to the local OS, removing any characters that are invalid for the local OS, and making the path relative to the
        ///     remote store (including the filename and the parent folder).
        /// </summary>
        /// <param name="remoteFilename">The fully qualified remote filename to convert.</param>
        /// <returns>The converted filename.</returns>
        public static string ToLocalRelativeFilename(this string remoteFilename)
        {
            if (string.IsNullOrWhiteSpace(remoteFilename))
            {
                throw new ArgumentException($"Invalid remote filename; expected a non-whitespace value, received '{remoteFilename}'", nameof(remoteFilename));
            }

            // normalize path separators
            var localizedRemoteFilename = remoteFilename.LocalizePath();

            var parts = localizedRemoteFilename.Split(Path.DirectorySeparatorChar);

            if (parts.Length == 1)
            {
                return parts.First().ReplaceInvalidFileNameCharacters();
            }

            var file = parts.Last().ReplaceInvalidFileNameCharacters();
            var directory = parts.Reverse().Skip(1).Take(1).Single().ReplaceInvalidFileNameCharacters();

            return Path.Combine(directory, file);
        }

        /// <summary>
        ///     Replaces any occurrence of an invalid filename character with the specified <see paramref="replacement"/>.
        /// </summary>
        /// <param name="path">The path to sanitize.</param>
        /// <param name="replacement">The character with which to replace invalid characters.</param>
        /// <returns>The sanitized path.</returns>
        public static string ReplaceInvalidFileNameCharacters(this string path, char replacement = '_')
        {
            var sanitized = path;

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, replacement);
            }

            return sanitized;
        }

        /// <summary>
        ///     Converts a ModelStateDictionary into a human-readable format.
        /// </summary>
        /// <param name="dictionary">The ModelStateDictionary to format.</param>
        /// <returns>The formatted error string.</returns>
        public static string GetReadableString(this ModelStateDictionary dictionary)
        {
            if (dictionary == null || dictionary.IsValid)
            {
                return string.Empty;
            }

            return dictionary.Values
                .Where(v => v.Errors.Any())
                .Select(v =>
                    v.Errors
                        .Select(e => GetErrorAndOrExceptionMessage(e))
                        .Aggregate((a, b) => string.Join(", ", new[] { a, b })))
                .Aggregate((a, b) => string.Join(" ", new[] { a, b }));
        }

        /// <summary>
        ///     Returns the <see cref="ModelError.ErrorMessage"/>, the <see cref="Exception.Message"/> for the ModelError, or both if both are present.
        /// </summary>
        /// <param name="error">The ModelError from which to retrieve the error message.</param>
        /// <returns>The retrieved error message.</returns>
        public static string GetErrorAndOrExceptionMessage(this ModelError error)
        {
            var ex = error?.Exception?.Message;
            return string.IsNullOrEmpty(error?.ErrorMessage) ? ex :
                string.IsNullOrEmpty(ex) ? error?.ErrorMessage : $"{error?.ErrorMessage} ({ex})";
        }

        /// <summary>
        ///     Determines whether the given object is a <see cref="Dictionary{TKey, TValue}"/>.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>A value indicating whether the given object is a dictionary.</returns>
        public static bool IsDictionary(this object obj)
        {
            if (obj == null)
            {
                return false;
            }

            return obj is IDictionary &&
                   obj.GetType().IsGenericType &&
                   obj.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        /// <summary>
        ///     Casts the string to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The Enum type to which to cast.</typeparam>
        /// <param name="str">The string to cast.</param>
        /// <returns>The cast enum.</returns>
        public static T ToEnum<T>(this string str)
        {
            return (T)Enum.Parse(typeof(T), str, ignoreCase: true);
        }

        /// <summary>
        ///     Deserializes this string from json to an object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to which to deserialize the string.</typeparam>
        /// <param name="str">The string to deserialize.</param>
        /// <returns>The new object deserialized from the string.</returns>
        public static T FromJson<T>(this string str) => JsonSerializer.Deserialize<T>(str, GetJsonSerializerOptions());

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new IPAddressConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new TypeConverter());
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            return options;
        }
    }
}