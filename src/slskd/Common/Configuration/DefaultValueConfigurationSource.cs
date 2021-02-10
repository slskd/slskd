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
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    ///     Defines a default value mapping.
    /// </summary>
    public record DefaultValue(string Key, Type Type, object Default);

    /// <summary>
    ///     Extension methods for adding <see cref="DefaultValueConfigurationProvider"/>.
    /// </summary>
    public static class DefaultValueConfigurationExtensions
    {
        /// <summary>
        ///     Adds a default value configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="map">A list of default value mappings.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddDefaultValues(this IConfigurationBuilder builder, IEnumerable<DefaultValue> map)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddDefaultValues(s =>
            {
                s.Map = map ?? Enumerable.Empty<DefaultValue>();
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
            Map = source.Map;
        }

        private IEnumerable<DefaultValue> Map { get; set; }

        /// <summary>
        ///     Sets the keys specified in the map to the specified default values.
        /// </summary>
        public override void Load()
        {
            foreach (var item in Map)
            {
                var (key, _, value) = item;

                if (string.IsNullOrEmpty(key) || value == null)
                {
                    continue;
                }

                Data[item.Key] = value.ToString();
            }
        }
    }

    /// <summary>
    ///     Represents default values as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class DefaultValueConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Gets or sets a list of default value mappings.
        /// </summary>
        public IEnumerable<DefaultValue> Map { get; set; }

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