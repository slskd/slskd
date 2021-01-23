namespace slskd
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Utility.CommandLine;

    public class Argument
    {
        public string Description { get; set; }
        public char ShortName { get; set; }
        public string LongName { get; set; }
        public string Key { get; set; }
    }

    public static class CommandLineArgumentExtensions
    {
        public static IConfigurationBuilder MapCommandLineArguments(this IConfigurationBuilder builder, IEnumerable<Argument> map, string commandLine)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            return builder.MapCommandLineArguments(s =>
            {
                s.Map = map;
                s.CommandLine = commandLine;
            });
        }

        public static IConfigurationBuilder MapCommandLineArguments(this IConfigurationBuilder builder, Action<CommandLineArgumentConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    public class CommandLineArgumentConfigurationSource : IConfigurationSource
    {
        public IEnumerable<Argument> Map { get; set; }
        public string CommandLine { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new CommandLineArgumentConfigurationProvider(this);
        }
    }

    public class CommandLineArgumentConfigurationProvider : ConfigurationProvider
    {
        public CommandLineArgumentConfigurationSource Source { get; set; }

        /// <summary>
        ///     Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public CommandLineArgumentConfigurationProvider(CommandLineArgumentConfigurationSource source) 
        {
            Source = source;   
        }

        public override void Load()
        {
            var dictionary = Arguments.Parse(Source.CommandLine).ArgumentDictionary;

            foreach (var item in Source.Map)
            {
                Console.WriteLine($"Item: {item.Key}");

                if (string.IsNullOrEmpty(item.Key))
                {
                    continue;
                }

                var arguments = new[] { item.ShortName.ToString(), item.LongName }.Where(i => !string.IsNullOrEmpty(i));

                foreach (var argument in arguments)
                {
                    if (dictionary.ContainsKey(argument))
                    {
                        var value = dictionary[argument].ToString();

                        if (string.IsNullOrEmpty(value))
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
