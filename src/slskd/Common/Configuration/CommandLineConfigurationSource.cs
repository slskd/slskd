namespace slskd
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Defines a command line argument mapping.
    /// </summary>
    public record CommandLineArgument(char ShortName, string LongName, Type Type, string Key, string Description = null);

    /// <summary>
    ///     Extension methods for adding <see cref="CommandLineConfigurationProvider"/>.
    /// </summary>
    public static class CommandLineExtensions
    {
        /// <summary>
        ///     Adds a command line argument configuration soruce to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <param name="map">A list of command line argument mappings.</param>
        /// <param name="commandLine">The command line string from which to parse arguments.</param>
        /// <param name="normalizeKey">A value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).</param>
        /// <returns></returns>
        public static IConfigurationBuilder AddCommandLine(this IConfigurationBuilder builder, IEnumerable<CommandLineArgument> map, string commandLine = null, bool normalizeKey = true)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.AddCommandLine(s =>
            {
                s.Map = map;
                s.CommandLine = commandLine ?? Environment.CommandLine;
                s.NormalizeKey = normalizeKey;
            });
        }

        /// <summary>
        ///     Adds a command line argument configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddCommandLine(this IConfigurationBuilder builder, Action<CommandLineConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     Represents command line arguments as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class CommandLineConfigurationSource : IConfigurationSource
    {
        /// <summary>
        ///     Builds the <see cref="CommandLineConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="CommandLineConfigurationProvider"/>.</returns>
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new CommandLineConfigurationProvider(this);

        /// <summary>
        ///     Gets or sets a list of command line argument mappings.
        /// </summary>
        public IEnumerable<CommandLineArgument> Map { get; set; }

        /// <summary>
        ///     Gets or sets the command line string from which to parse arguments.
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether configuration keys should be normalized (_, -  removed, changed to lowercase).
        /// </summary>
        public bool NormalizeKey { get; set; }
    }

    /// <summary>
    ///     A command line argument <see cref="ConfigurationProvider"/>.
    /// </summary>
    public class CommandLineConfigurationProvider : ConfigurationProvider
    {
        private IEnumerable<CommandLineArgument> Map { get; set; }
        private string CommandLine { get; set; }
        private bool NormalizeKey { get; set; }

        /// <summary>
        ///     Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public CommandLineConfigurationProvider(CommandLineConfigurationSource source) 
        {
            Map = source.Map;
            CommandLine = source.CommandLine;
            NormalizeKey = source.NormalizeKey;
        }

        /// <summary>
        ///     Parses command line arguments from the specified string and maps them to the specified keys.
        /// </summary>
        public override void Load()
        {
            var dictionary = CommandLineArguments.Parse(CommandLine).ArgumentDictionary;

            foreach (CommandLineArgument item in Map)
            {
                var (shortName, longName, type, key, _) = item;

                if (NormalizeKey)
                {
                    key = key?.Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
                }

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var arguments = new[] { shortName.ToString(), longName }.Where(i => !string.IsNullOrEmpty(i));

                foreach (var argument in arguments)
                {
                    if (dictionary.ContainsKey(argument))
                    {
                        var value = dictionary[argument].ToString();

                        if (type == typeof(bool) && string.IsNullOrEmpty(value))
                        {
                            value = "true";
                        }

                        Data[item.Key] = value;
                    }
                }
            }
        }
    }
}
