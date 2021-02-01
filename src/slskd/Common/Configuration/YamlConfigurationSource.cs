namespace slskd
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.FileProviders;
    using System;
    using System.IO;
    using System.Linq;
    using YamlDotNet.Core;
    using YamlDotNet.RepresentationModel;

    /// <summary>
    ///     Extension methods for adding <see cref="YamlConfigurationProvider"/>.
    /// </summary>
    public static class YamlConfigurationExtensions
    {
        /// <summary>
        ///     Adds a YAML configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="path">Path relative to the base path stored in
        /// <see cref="IConfigurationBuilder.Properties"/> of <paramref name="builder"/>.</param>
        /// <param name="optional">Whether the file is optional.</param>
        /// <param name="reloadOnChange">Whether the configuration should be reloaded if the file changes.</param>
        /// <param name="normalizeKeys">A value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).</param>
        /// <param name="provider">The <see cref="IFileProvider"/> to use to access the file.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, string path, bool optional = true, bool reloadOnChange = false, bool normalizeKeys = true, IFileProvider provider = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("File path must be a non-empty string.", nameof(path));
            }

            return builder.AddYamlFile(s =>
            {
                s.Path = path;
                s.Optional = optional;
                s.ReloadOnChange = reloadOnChange;
                s.NormalizeKeys = normalizeKeys;
                s.FileProvider = provider;
                s.ResolveFileProvider();
            });
        }

        /// <summary>
        ///     Adds a YAML configuration source to <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
        /// <param name="configureSource">Configures the source.</param>
        /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
        public static IConfigurationBuilder AddYamlFile(this IConfigurationBuilder builder, Action<YamlConfigurationSource> configureSource)
            => builder.Add(configureSource);
    }

    /// <summary>
    ///     Represents a YAML file as an <see cref="IConfigurationSource"/>.
    /// </summary>
    public class YamlConfigurationSource : FileConfigurationSource
    {
        /// <summary>
        /// Builds the <see cref="YamlConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="YamlConfigurationProvider"/></returns>
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new YamlConfigurationProvider(this);
        }

        /// <summary>
        ///     Gets or sets a value indicating whether configuration keys should be normalized (_, - removed, changed to lowercase).
        /// </summary>
        public bool NormalizeKeys { get; set; }
    }

    /// <summary>
    ///     A YAML file based <see cref="FileConfigurationProvider"/>.
    /// </summary>
    public class YamlConfigurationProvider : FileConfigurationProvider
    {
        /// <summary>
        ///     Initializes a new instance with the specified source.
        /// </summary>
        /// <param name="source">The source settings.</param>
        public YamlConfigurationProvider(YamlConfigurationSource source) 
            : base(source) 
        {
            NormalizeKeys = source.NormalizeKeys;
        }

        private bool NormalizeKeys { get; set; }
        private string[] NullValues { get; } = new[] { "~", "null", "" };

        /// <summary>
        ///     Loads the YAML data from a stream.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        public override void Load(Stream stream)
        {
            try
            {
                using var reader = new StreamReader(stream);

                var yaml = new YamlStream();
                yaml.Load(reader);

                if (yaml.Documents.Count > 0)
                {
                    var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
                    Traverse(rootNode);
                }
            }
            catch (YamlException e)
            {
                throw new FormatException("Could not parse the YAML file.", e);
            }
        }

        private void Traverse(YamlNode root, string path = null)
        {
            string Normalize(string str) => NormalizeKeys ? str?.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant() : str;

            if (root is YamlScalarNode scalar)
            {
                if (Data.ContainsKey(Normalize(path)))
                {
                    throw new FormatException($"A duplicate key '{Normalize(path)}' was found.");
                }

                var value = NullValues.Contains(scalar.Value.ToLower()) ? null : scalar.Value;

                if (value != null)
                {
                    Data[Normalize(path)] = NullValues.Contains(scalar.Value.ToLower()) ? null : scalar.Value;
                }
            }
            else if (root is YamlMappingNode map)
            {
                foreach (var node in map.Children)
                {
                    var key = Normalize(((YamlScalarNode)node.Key).Value);
                    Traverse(node.Value, path == null ? key : ConfigurationPath.Combine(path, key));
                }
            }
            else if (root is YamlSequenceNode sequence)
            {
                for (int i = 0; i < sequence.Children.Count; i++)
                {
                    Traverse(sequence.Children[i], ConfigurationPath.Combine(path, i.ToString()));
                }
            }
        }
    }
}
