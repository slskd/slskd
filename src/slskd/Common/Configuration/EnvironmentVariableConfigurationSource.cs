// <copyright file="EnvironmentVariableConfigurationSource.cs" company="slskd Team">
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
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Extension methods for adding <see cref="EnvironmentVariableConfigurationProvider"/>.
    /// </summary>
    public static class EnvironmentVariableConfigurationExtensions
    {
        /// <summary>
        ///     Adds an environment variable configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="targetType">The type from which to map properties.</param>
        /// <param name="prefix">A prefix to prepend to all variable names.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddEnvironmentVariables(this IConfigurationBuilder builder, Type targetType, string prefix = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            return builder.AddEnvironmentVariables(s =>
            {
                s.TargetType = targetType;
                s.Prefix = prefix ?? string.Empty;
            });
        }

        /// <summary>
        ///     Adds an environment variable configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddEnvironmentVariables(this IConfigurationBuilder builder, Action<EnvironmentVariableConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     An environment variable <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class EnvironmentVariableConfigurationProvider : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="EnvironmentVariableConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public EnvironmentVariableConfigurationProvider(EnvironmentVariableConfigurationSource source)
        {
            TargetType = source.TargetType;
            Namespace = TargetType.Namespace.Split('.').First();
            Prefix = source.Prefix;
        }

        private Type TargetType { get; set; }
        private string Namespace { get; set; }
        private string Prefix { get; set; }

        /// <summary>
        ///     Loads environment variables and maps them to the corresponding keys.
        /// </summary>
        public override void Load()
        {
            void Map(Type type, string path)
            {
                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(EnvironmentVariableAttribute));
                    var key = ConfigurationPath.Combine(path, property.Name.ToLowerInvariant());

                    if (attribute != default)
                    {
                        // retrieve the envar name from the attribute
                        var name = (string)attribute.ConstructorArguments[0].Value;

                        if (!string.IsNullOrEmpty(name))
                        {
                            // retrieve the corresponding value from environment variables
                            var value = Environment.GetEnvironmentVariable(Prefix + name);

                            if (value != null)
                            {
                                // if the type of the backing property is an array,
                                // split the retrieved value by semicolon and add the parts to
                                // config as zero-based children of the prop
                                if (property.PropertyType.IsArray)
                                {
                                    var elements = value.Split(';');

                                    for (int i = 0; i < elements.Length; i++)
                                    {
                                        Data[ConfigurationPath.Combine(key, i.ToString())] = elements[i];
                                    }
                                }
                                else
                                {
                                    Data[key] = value.ToString();
                                }
                            }
                        }
                    }
                    else
                    {
                        Map(property.PropertyType, key);
                    }
                }
            }

            Map(TargetType, Namespace);
        }
    }

    /// <summary>
    ///     Represents environment variables as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class EnvironmentVariableConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Gets or sets the type from which to map properties.
        /// </summary>
        public Type TargetType { get; set; }

        /// <summary>
        ///     Gets or sets a prefix to prepend to all variable names.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        ///     Builds the <see cref="EnvironmentVariableConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="EnvironmentVariableConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new EnvironmentVariableConfigurationProvider(this);
        }
    }
}