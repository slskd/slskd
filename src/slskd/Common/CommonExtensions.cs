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
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    /// <summary>
    ///     Common extensions; things not specific to Soulseek or slskd.
    /// </summary>
    public static class CommonExtensions
    {
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
                else if (propType.IsPrimitive || Nullable.GetUnderlyingType(propType) != null || new[] { typeof(string), typeof(decimal) }.Contains(propType))
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
        ///     Returns the directory from the given path, regardless of separator format.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The directory.</returns>
        public static string DirectoryName(this string path)
        {
            var separator = path.Contains('\\') ? '\\' : '/';
            var parts = path.Split(separator);
            return string.Join(separator, parts.Take(parts.Length - 1));
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
        ///     Replaces the first occurance of <paramref name="phrase"/> in the string with <paramref name="replacement"/>.
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
        /// <returns>The new object deserialzied from the string.</returns>
        public static T FromYaml<T>(this string str) => new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build()
            .Deserialize<T>(str);

        public static string NormalizePath(this string filename)
        {
            if (Path.DirectorySeparatorChar == '\\')
            {
                return filename;
            }

            return filename.Replace(Path.DirectorySeparatorChar, '\\');
        }

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
        ///     Converts the given path to the local format (normalizes path separators).
        /// </summary>
        /// <param name="path">The path to convert.</param>
        /// <returns>The converted path.</returns>
        public static string ToLocalOSPath(this string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
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
            var localizedRemoteFilename = remoteFilename.ToLocalOSPath();

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
        ///     Replaces any occurance of an invalid filename character with the specified <see paramref="replacement"/>.
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
        ///     Converts the byte array into a base 62 encoded string.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <returns>The converted bytes as a base 62 string.</returns>
        public static string ToBase62String(this byte[] bytes)
        {
            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

            BigInteger dividend = new BigInteger(bytes);
            var builder = new StringBuilder();

            while (dividend != 0)
            {
                dividend = BigInteger.DivRem(dividend, alphabet.Length, out BigInteger remainder);
                builder.Insert(0, alphabet[Math.Abs((int)remainder)]);
            }

            return builder.ToString();
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
        ///     Deserializes this string from json to an object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to which to deserialize the string.</typeparam>
        /// <param name="str">The string to deserialize.</param>
        /// <returns>The new object deserialzied from the string.</returns>
        public static T FromJson<T>(this string str) => JsonSerializer.Deserialize<T>(str, GetJsonSerializerOptions());

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new IPAddressConverter());
            options.Converters.Add(new JsonStringEnumConverter());
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            return options;
        }
    }
}