// <copyright file="RuntimePatchConfigurationSource.cs" company="slskd Team">
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
    using System.Reflection;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    ///     Extension methods for adding <see cref="RuntimePatchConfigurationProvider"/>.
    /// </summary>
    public static class RuntimePatchConfigurationExtensions
    {
        /// <summary>
        ///     Adds a runtime patch configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to which to add.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The updated <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddRuntimePatch(
            this IConfigurationBuilder builder,
            Action<RuntimePatchConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     A runtime patch <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class RuntimePatchConfigurationProvider : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RuntimePatchConfigurationProvider"/> class.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public RuntimePatchConfigurationProvider(RuntimePatchConfigurationSource source)
        {
            Source = source;
            source.Provider = this;
        }

        private RuntimePatchConfigurationSource Source { get; }

        /// <summary>
        ///     Loads patch values from the current <see cref="OptionsPatch"/> and maps them to the corresponding keys.
        /// </summary>
        public override void Load()
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (Source.Patch != null)
            {
                Flatten(Source.Patch, "slskd", data);
            }

            Data = data;
        }

        /// <summary>
        ///     Applies the specified <paramref name="patch"/> and triggers a configuration reload.
        /// </summary>
        /// <param name="patch">The patch to apply.</param>
        public void ApplyPatch(OptionsPatch patch)
        {
            Source.Patch = patch;
            Load();
            OnReload();
        }

        private void Flatten(object obj, string path, Dictionary<string, string> data)
        {
            if (obj == null)
            {
                return;
            }

            var type = obj.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in props)
            {
                var value = property.GetValue(obj);

                if (value == null)
                {
                    continue;
                }

                var key = ConfigurationPath.Combine(path, property.Name.ToLowerInvariant());

                if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    Flatten(value, key, data);
                }
                else
                {
                    data[key] = value.ToString();
                }
            }
        }
    }

    /// <summary>
    ///     Represents a runtime patch as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class RuntimePatchConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Gets or sets the provider instance.
        /// </summary>
        internal RuntimePatchConfigurationProvider Provider { get; set; }

        /// <summary>
        ///     Gets or sets the current patch.
        /// </summary>
        internal OptionsPatch Patch { get; set; }

        /// <summary>
        ///     Builds the <see cref="RuntimePatchConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="RuntimePatchConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new RuntimePatchConfigurationProvider(this);
        }

        /// <summary>
        ///     Applies the specified <paramref name="patch"/> and triggers a configuration reload.
        /// </summary>
        /// <param name="patch">The patch to apply.</param>
        public void ApplyPatch(OptionsPatch patch)
        {
            Provider?.ApplyPatch(patch);
        }
    }
}
