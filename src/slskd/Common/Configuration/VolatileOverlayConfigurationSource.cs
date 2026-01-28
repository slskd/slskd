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
    ///     A runtime patch <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class VolatileOverlayConfigurationProvider<T> : ConfigurationProvider
    {
        // /// <summary>
        // ///     Initializes a new instance of the <see cref="VolatileOverlayConfigurationProvider"/> class.
        // /// </summary>
        // /// <param name="source">The source settings.</param>
        // public VolatileOverlayConfigurationProvider(VolatileOverlayConfigurationSource source)
        // {
        //     TargetType = source.TargetType;
        //     Namespace = TargetType.Namespace.Split('.').First();
        // }
        public VolatileOverlayConfigurationProvider()
        {
            TargetType = typeof(T);
            Namespace = TargetType.Namespace.Split('.').First();
        }

        private T Current { get; set; }
        private string Namespace { get; set; }
        private Type TargetType { get; set; }

        public override void Load()
        {
            if (Current is null)
            {
                return;
            }

            void Map(Type type, string path)
            {
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
                            var value = property.GetValue(Current);

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
                            Data[key] = JsonSerializer.Serialize(property.GetValue(Current));
                        }
                    }
                }
            }

            Map(TargetType, Namespace);

            Console.WriteLine("load called!");
            // Data["slskd:soulseek:listenport"] = Current.Soulseek.ListenPort.ToString();

            // todo: take the current patch value and stuff the options into Data[key]
            Console.WriteLine(Data.ToJson());
        }

        public void Apply(T overlay)
        {
            Current = overlay;
            Load();
            OnReload();
        }
    }

    public class VolatileOverlayConfigurationSource<T> : IConfigurationSource
    {
        public VolatileOverlayConfigurationSource()
        {
            Provider = new VolatileOverlayConfigurationProvider<T>();
        }

        private VolatileOverlayConfigurationProvider<T> Provider { get; set; }

        public void Apply(T overlay) => Provider.Apply(overlay);

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return Provider;
        }
    }
}
