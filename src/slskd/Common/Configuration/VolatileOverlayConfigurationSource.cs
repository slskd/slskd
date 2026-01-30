// <copyright file="VolatileOverlayConfigurationSource.cs" company="slskd Team">
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
    using System.Text.Json;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    ///     A <see cref="ConfigurationProvider"/> that provides a volatile, run-time overlay that supersedes other configuration
    ///     sources.
    /// </summary>
    /// <remarks>
    ///     To use this provider as intended, the application must instantiate and store an instance of either <see cref="VolatileOverlayConfigurationProvider{T}"/>
    ///     or <see cref="VolatileOverlayConfigurationSource{T}"/>, and then invoke <see cref="Apply"/> with the desired overlay object.
    /// </remarks>
    /// <typeparam name="T">The Type of the overlay.</typeparam>
    public class VolatileOverlayConfigurationProvider<T> : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="VolatileOverlayConfigurationProvider{T}"/> class.
        /// </summary>
        public VolatileOverlayConfigurationProvider()
        {
            TargetType = typeof(T);
            Namespace = TargetType.Namespace.Split('.').First();
        }

        /// <summary>
        ///     Gets the current overlay value.
        /// </summary>
        public T CurrentValue { get; private set; }

        private string Namespace { get; set; }
        private Type TargetType { get; set; }

        /// <summary>
        ///     Loads values from the <see cref="CurrentValue"/> overlay.
        /// </summary>
        public override void Load()
        {
            if (CurrentValue is null)
            {
                return;
            }

            // note: pretty much the same as DefaultValueConfigurationProvider; the two should be updated in lockstep
            void Map(Type type, string path, object instance)
            {
                if (instance is null)
                {
                    return;
                }

                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var key = ConfigurationPath.Combine(path, property.Name.ToLowerInvariant());

                    if (property.PropertyType.Namespace.StartsWith(Namespace))
                    {
                        Map(property.PropertyType, key, property.GetValue(instance));
                    }
                    else
                    {
                        // don't add array values to the configuration; these are additive across providers
                        // and the default value from the class is "stuck", so adding them again results in duplicates.
                        if (!property.PropertyType.IsArray)
                        {
                            var value = property.GetValue(instance);

                            if (value != null)
                            {
                                Data[key] = value.ToString();
                            }
                        }
                        else
                        {
                            // serialize array defaults and stick them on the parent key
                            // (not indexed by array position).  this value is "stuck", and
                            // we want to show that in the config debug view.  this isn't really
                            // functional, just illustrative.
                            Data[key] = JsonSerializer.Serialize(property.GetValue(instance));
                        }
                    }
                }
            }

            Map(TargetType, Namespace, CurrentValue);
        }

        /// <summary>
        ///     Applies the given <paramref name="overlay"/>.
        /// </summary>
        /// <param name="overlay">An object containing the values to overlay.</param>
        public void Apply(T overlay)
        {
            CurrentValue = overlay;
            Load();
            OnReload();
        }
    }

    /// <summary>
    ///     Represents values that are provided at run-time as an <see cref="IConfigurationSource"/>.
    /// </summary>
    /// <remarks>
    ///     To use this provider as intended, the application must instantiate and store an instance of either <see cref="VolatileOverlayConfigurationProvider{T}"/>
    ///     or <see cref="VolatileOverlayConfigurationSource{T}"/>, and then invoke <see cref="Apply"/> with the desired overlay object.
    /// </remarks>
    /// <typeparam name="T">The Type of the overlay.</typeparam>
    public class VolatileOverlayConfigurationSource<T> : IConfigurationSource
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="VolatileOverlayConfigurationSource{T}"/> class.
        /// </summary>
        public VolatileOverlayConfigurationSource()
        {
            Provider = new VolatileOverlayConfigurationProvider<T>();
        }

        /// <summary>
        ///     Gets the current overlay value.
        /// </summary>
        public T CurrentValue => Provider.CurrentValue;

        private VolatileOverlayConfigurationProvider<T> Provider { get; set; }

        /// <summary>
        ///     Applies the given <paramref name="overlay"/> to the underlying <see cref="VolatileOverlayConfigurationProvider{T}"/>.
        /// </summary>
        /// <param name="overlay">An object containing the values to overlay.</param>
        public void Apply(T overlay) => Provider.Apply(overlay);

        /// <summary>
        ///     Builds the <see cref="VolatileOverlayConfigurationSource{T}"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="VolatileOverlayConfigurationProvider{T}"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return Provider;
        }
    }
}
