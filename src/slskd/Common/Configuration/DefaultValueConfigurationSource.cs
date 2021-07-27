// <copyright file="DefaultValueConfigurationSource.cs" company="slskd Team">
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

namespace slskd.Configuration
{
    using System;
    using System.Linq;
    using System.Reflection;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    ///     Extension methods for adding <see cref="DefaultValueConfigurationProvider"/>.
    /// </summary>
    public static class DefaultValueConfigurationExtensions
    {
        /// <summary>
        ///     Adds a default value configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="targetType">The type from which to load default values.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddDefaultValues(this IConfigurationBuilder builder, Type targetType)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            return builder.AddDefaultValues(s =>
            {
                s.TargetType = targetType;
            });
        }

        /// <summary>
        ///     Adds a default value configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddDefaultValues(this IConfigurationBuilder builder, Action<DefaultValueConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     A default value <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class DefaultValueConfigurationProvider : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="DefaultValueConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public DefaultValueConfigurationProvider(DefaultValueConfigurationSource source)
        {
            TargetType = source.TargetType;
            Namespace = TargetType.Namespace.Split('.').First();
        }

        private string Namespace { get; set; }
        private Type TargetType { get; set; }

        /// <summary>
        ///     Loads default values from the specified <see cref="TargetType"/> and maps them to the corresponding keys.
        /// </summary>
        public override void Load()
        {
            void Map(Type type, string path)
            {
                var defaults = Activator.CreateInstance(type);
                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var key = ConfigurationPath.Combine(path, property.Name.ToLowerInvariant());

                    if (property.PropertyType.Namespace.StartsWith(Namespace))
                    {
                        Map(property.PropertyType, key);
                    }
                    else
                    {
                        // don't add array values to the configuration; these are additive across providers
                        // and the default value from the class is "stuck", so adding them again results in duplicates.
                        if (!property.PropertyType.IsArray)
                        {
                            var value = property.GetValue(defaults);

                            if (value != null)
                            {
                                Data[key] = value.ToString();
                            }
                        }
                    }
                }
            }

            Map(TargetType, Namespace);
        }
    }

    /// <summary>
    ///     Represents default values as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class DefaultValueConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Gets or sets the type from which to map properties.
        /// </summary>
        public Type TargetType { get; set; }

        /// <summary>
        ///     Builds the <see cref="DefaultValueConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="DefaultValueConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new DefaultValueConfigurationProvider(this);
        }
    }
}