// <copyright file="CommandLineConfigurationSource.cs" company="slskd Team">
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
    using Utility.CommandLine;

    /// <summary>
    ///     Extension methods for adding <see cref="CommandLineConfigurationProvider"/>.
    /// </summary>
    public static class CommandLineConfigurationExtensions
    {
        /// <summary>
        ///     Adds a command line argument configuration soruce to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="targetType">The type from which to map properties.</param>
        /// <param name="commandLine">The command line string from which to parse arguments.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddCommandLine(this IConfigurationBuilder builder, Type targetType, string commandLine = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            return builder.AddCommandLine(s =>
            {
                s.TargetType = targetType;
                s.CommandLine = commandLine ?? Environment.CommandLine;
            });
        }

        /// <summary>
        ///     Adds a command line argument configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddCommandLine(this IConfigurationBuilder builder, Action<CommandLineConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     A command line argument <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class CommandLineConfigurationProvider : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CommandLineConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public CommandLineConfigurationProvider(CommandLineConfigurationSource source)
        {
            TargetType = source.TargetType;
            Namespace = TargetType.Namespace.Split('.').First();
            CommandLine = source.CommandLine;
        }

        private string CommandLine { get; set; }
        private string Namespace { get; set; }
        private Type TargetType { get; set; }

        /// <summary>
        ///     Parses command line arguments from the specified string and maps them to the corresponding keys.
        /// </summary>
        public override void Load()
        {
            var dictionary = Arguments.Parse(CommandLine).ArgumentDictionary;

            void Map(Type type, string path)
            {
                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ArgumentAttribute));
                    var key = ConfigurationPath.Combine(path, property.Name.ToLowerInvariant());

                    if (attribute != default)
                    {
                        var shortName = ((char)attribute.ConstructorArguments[0].Value).ToString();
                        var longName = (string)attribute.ConstructorArguments[1].Value;
                        var arguments = new[] { shortName, longName }.Where(i => !string.IsNullOrEmpty(i));

                        foreach (var argument in arguments)
                        {
                            if (dictionary.ContainsKey(argument))
                            {
                                var value = dictionary[argument].ToString();

                                if (property.PropertyType == typeof(bool) && string.IsNullOrEmpty(value))
                                {
                                    value = "true";
                                }

                                Data[key] = value;
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
    ///     Represents command line arguments as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class CommandLineConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Gets or sets the command line string from which to parse arguments.
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        ///     Gets or sets the type from which to map properties.
        /// </summary>
        public Type TargetType { get; set; }

        /// <summary>
        ///     Builds the <see cref="CommandLineConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="CommandLineConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new CommandLineConfigurationProvider(this);
    }
}