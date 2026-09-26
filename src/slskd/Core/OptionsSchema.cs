// <copyright file="OptionsSchema.cs" company="JP Dillingham">
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

namespace slskd
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using slskd.Configuration;
    using slskd.Validation;
    using YamlDotNet.Serialization;

    /// <summary>
    ///     Describes an option, or a group of options, so that clients can build an editor for them.
    /// </summary>
    public record OptionsSchemaNode
    {
        /// <summary>
        ///     Gets the name of the option, as it appears in the options returned by the API.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     Gets the kind of value: object, dictionary, array, boolean, integer, number or string.
        /// </summary>
        public string Type { get; init; }

        /// <summary>
        ///     Gets a short description of the option, if one is available.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        ///     Gets the default value, if it is a simple value and isn't a secret.
        /// </summary>
        public object Default { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the value may be null.
        /// </summary>
        public bool Nullable { get; init; }

        /// <summary>
        ///     Gets the values allowed for strings (or the elements of string arrays), if restricted.
        /// </summary>
        public IEnumerable<string> Values { get; init; }

        /// <summary>
        ///     Gets the minimum allowed value for numbers, or length for strings.
        /// </summary>
        public double? Minimum { get; init; }

        /// <summary>
        ///     Gets the maximum allowed value for numbers, or length for strings.
        /// </summary>
        public double? Maximum { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the application must be restarted for changes to take effect.
        /// </summary>
        public bool RequiresRestart { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the client must reconnect to the server for changes to take effect.
        /// </summary>
        public bool RequiresReconnect { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the value is a secret, which the API redacts.
        /// </summary>
        public bool Secret { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the value is a filesystem path.
        /// </summary>
        public bool Path { get; init; }

        /// <summary>
        ///     Gets the child options of an object.
        /// </summary>
        public IEnumerable<OptionsSchemaNode> Properties { get; init; }

        /// <summary>
        ///     Gets the schema of the elements of an array, or the values of a dictionary, when they are objects.
        /// </summary>
        public OptionsSchemaNode Items { get; init; }
    }

    /// <summary>
    ///     Builds a description of <see cref="Options"/> from the attributes on its properties.
    /// </summary>
    public static class OptionsSchema
    {
        private static readonly Lazy<OptionsSchemaNode> Cached = new(() => DescribeType(typeof(Options), defaults: null, inheritRestart: false));

        /// <summary>
        ///     Gets the schema for <see cref="Options"/>.
        /// </summary>
        public static OptionsSchemaNode Root => Cached.Value;

        private static OptionsSchemaNode DescribeType(Type type, object defaults, bool inheritRestart)
        {
            if (defaults is null)
            {
                try
                {
                    defaults = Activator.CreateInstance(type);
                }
                catch
                {
                    // types without a parameterless constructor just won't have defaults
                }
            }

            return new OptionsSchemaNode
            {
                Type = "object",
                Properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(IsConfigurable)
                    .Select(property => DescribeProperty(property, defaults, inheritRestart))
                    .Where(node => node is not null)
                    .ToList(),
            };
        }

        private static OptionsSchemaNode DescribeProperty(PropertyInfo property, object defaults, bool inheritRestart)
        {
            var type = property.PropertyType;
            var underlying = System.Nullable.GetUnderlyingType(type) ?? type;
            var restart = inheritRestart || property.GetCustomAttribute<RequiresRestartAttribute>() is not null;
            var secret = property.GetCustomAttribute<SecretAttribute>() is not null;
            var range = property.GetCustomAttribute<RangeAttribute>();
            var length = property.GetCustomAttribute<StringLengthAttribute>();
            var enumeration = property.GetCustomAttribute<EnumAttribute>()?.TargetType;

            object defaultValue = null;

            try
            {
                defaultValue = defaults is null ? null : property.GetValue(defaults);
            }
            catch
            {
                // leave the default unknown
            }

            var node = new OptionsSchemaNode
            {
                Name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name),
                Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                RequiresRestart = restart,
                RequiresReconnect = property.GetCustomAttribute<RequiresReconnectAttribute>() is not null,
                Secret = secret,
                Path = property.GetCustomAttributes().Any(attribute =>
                    attribute is AbsolutePathAttribute or DirectoryExistsAttribute or FileExistsAttribute or RelativePathAttribute),
                Nullable = !type.IsValueType || underlying != type,
                Values = enumeration is null ? null : Enum.GetNames(enumeration).Select(value => value.ToLowerInvariant()).ToList(),
                Minimum = ToDouble(range?.Minimum) ?? (length?.MinimumLength > 0 ? length.MinimumLength : null),
                Maximum = ToDouble(range?.Maximum) ?? length?.MaximumLength,
            };

            if (underlying == typeof(bool))
            {
                return node with { Type = "boolean", Default = defaultValue };
            }

            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short))
            {
                return node with { Type = "integer", Default = defaultValue };
            }

            if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
            {
                return node with { Type = "number", Default = defaultValue };
            }

            if (underlying == typeof(string))
            {
                return node with { Type = "string", Default = secret ? null : defaultValue };
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var valueType = type.GetGenericArguments()[1];
                return node with { Type = "dictionary", Items = DescribeType(valueType, defaults: null, inheritRestart: restart) };
            }

            if (type.IsArray || (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)))
            {
                var elementType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];

                if (elementType == typeof(string))
                {
                    return node with { Type = "array", Default = defaultValue };
                }

                return node with { Type = "array", Items = DescribeType(elementType, defaults: null, inheritRestart: restart) };
            }

            if (type.IsClass && type != typeof(object))
            {
                var described = DescribeType(type, defaultValue, restart);
                return node with { Type = "object", Properties = described.Properties };
            }

            return null;
        }

        private static bool IsConfigurable(PropertyInfo property) =>
            property.CanRead
            && property.GetIndexParameters().Length == 0
            && property.PropertyType != typeof(object)
            && property.GetCustomAttribute<ObsoleteAttribute>() is null
            && property.GetCustomAttribute<YamlIgnoreAttribute>() is null
            && property.GetCustomAttribute<JsonIgnoreAttribute>() is null;

        private static double? ToDouble(object value)
        {
            try
            {
                return value is null ? null : Convert.ToDouble(value);
            }
            catch
            {
                return null;
            }
        }
    }
}
