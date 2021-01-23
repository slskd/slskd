namespace slskd
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;

    public class EnvironmentVariable
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
    }

    public static class EnvironmentVariableConfigurationExtensions
    {
        public static IConfigurationBuilder MapEnvironmentVariables(this IConfigurationBuilder builder, IEnumerable<EnvironmentVariable> map)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.MapEnvironmentVariables(s =>
            {
                s.Map = map;
            });
        }

        public static IConfigurationBuilder MapEnvironmentVariables(this IConfigurationBuilder builder, Action<EnvironmentVariableConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    public class EnvironmentVariableConfigurationSource : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new EnvironmentVariableConfigurationProvider(this);
        }

        public IEnumerable<EnvironmentVariable> Map { get; set; }
    }

    public class EnvironmentVariableConfigurationProvider : ConfigurationProvider
    {
        public EnvironmentVariableConfigurationSource Source { get; set; }
        /// <summary>
        ///     Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public EnvironmentVariableConfigurationProvider(EnvironmentVariableConfigurationSource source) 
        {
            Source = source;
        }

        public override void Load()
        {
            foreach (var item in Source.Map)
            {
                if (string.IsNullOrEmpty(item.Key))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(item.Name))
                {
                    var value = Environment.GetEnvironmentVariable(item.Name);

                    if (!string.IsNullOrEmpty(value))
                    {
                        Data[item.Key] = value;
                    }
                }
            }
        }
    }
}
