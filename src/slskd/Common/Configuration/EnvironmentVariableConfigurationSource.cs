namespace slskd.Configuration
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Defines an environment variable mapping.
    /// </summary>
    public record EnvironmentVariable(string Name, Type Type, string Key, string Description = null);

    /// <summary>
    ///     Extension methods for adding <see cref="EnvironmentVariableConfigurationProvider"/>.
    /// </summary>
    public static class EnvironmentVariableConfigurationExtensions
    {
        /// <summary>
        ///     Adds an environment variable configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="map">A list of environment variable mappings.</param>
        /// <param name="prefix">A prefix to prepend to all variable names.</param>
        /// <param name="normalizeKey">A value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddEnvironmentVariables(this IConfigurationBuilder builder, IEnumerable<EnvironmentVariable> map, string prefix = null, bool normalizeKey = true)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddEnvironmentVariables(s =>
            {
                s.Map = map ?? Enumerable.Empty<EnvironmentVariable>();
                s.Prefix = prefix ?? string.Empty;
                s.NormalizeKey = normalizeKey;
            });
        }

        /// <summary>
        ///     Adds an enviroment variable configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddEnvironmentVariables(this IConfigurationBuilder builder, Action<EnvironmentVariableConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     Represents environment variables as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class EnvironmentVariableConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Builds the <see cref="EnvironmentVariableConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="EnvironmentVariableConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new EnvironmentVariableConfigurationProvider(this);
        }

        /// <summary>
        ///     Gets or sets a list of enviroment variable mappings.
        /// </summary>
        public IEnumerable<EnvironmentVariable> Map { get; set; }

        /// <summary>
        ///     Gets or sets a prefix to prepend to all variable names.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).
        /// </summary>
        public bool NormalizeKey { get; set; }
    }

    /// <summary>
    ///     An enviroment variable <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class EnvironmentVariableConfigurationProvider : ConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public EnvironmentVariableConfigurationProvider(EnvironmentVariableConfigurationSource source) 
        {
            Map = source.Map;
            Prefix = source.Prefix;
            NormalizeKey = source.NormalizeKey;
        }

        private IEnumerable<EnvironmentVariable> Map { get; set; }
        private bool NormalizeKey { get; set; }
        private string Prefix { get; set; }

        /// <summary>
        ///     Loads environment variables and maps them to the specified keys.
        /// </summary>
        public override void Load()
        {
            foreach (var item in Map)
            {
                var (name, type, key, _) = item;

                if (NormalizeKey)
                {
                    key = key?.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
                }

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var value = Environment.GetEnvironmentVariable(Prefix + name);

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (type == typeof(bool))
                {                    
                    value = value.Equals("true", StringComparison.InvariantCultureIgnoreCase) ? value : "false";
                }

                Data[item.Key] = value;
            }
        }
    }
}
