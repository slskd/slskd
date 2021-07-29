// <copyright file="Program.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.FileProviders.Physical;
    using Prometheus.DotNetRuntime;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using slskd.Configuration;
    using slskd.Cryptography;
    using slskd.Validation;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;
    using IOFile = System.IO.File;

    /// <summary>
    ///     Bootstraps configuration and handles primitive command-line instructions.
    /// </summary>
    public static class Program
    {
        /// <summary>
        ///     The name of the application.
        /// </summary>
        public static readonly string AppName = "slskd";

        /// <summary>
        ///     The default application data directory.
        /// </summary>
        public static readonly string DefaultAppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), AppName);

        /// <summary>
        ///     The default configuration filename.
        /// </summary>
        public static readonly string DefaultConfigurationFile = Path.Combine(AppContext.BaseDirectory, "config", $"{AppName}.yml");

        /// <summary>
        ///     The default incomplete download directory.
        /// </summary>
        public static readonly string DefaultIncompleteDirectory = Path.Combine(DefaultAppDirectory, "incomplete");

        /// <summary>
        ///     The default downloads directory.
        /// </summary>
        public static readonly string DefaultDownloadsDirectory = Path.Combine(DefaultAppDirectory, "downloads");

        /// <summary>
        ///     The default shared directory.
        /// </summary>
        public static readonly string DefaultSharedDirectory = Path.Combine(DefaultAppDirectory, "shared");

        /// <summary>
        ///     The global prefix for environment variables.
        /// </summary>
        public static readonly string EnvironmentVariablePrefix = $"{AppName.ToUpperInvariant()}_";

        /// <summary>
        ///     The default XML documentation filename.
        /// </summary>
        public static readonly string XmlDocumentationFile = Path.Combine(AppContext.BaseDirectory, "etc", $"{AppName}.xml");

        /// <summary>
        ///     Gets the assembly version of the application.
        /// </summary>
        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        public static Version AssemblyVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        ///     Gets the informational version of the application, including the git sha at the latest commit.
        /// </summary>
        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        public static string InformationalVersion { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        ///     Gets the unique Id of this application invocation.
        /// </summary>
        public static Guid InvocationId { get; } = Guid.NewGuid();

        /// <summary>
        ///     Gets the Id of the current application process.
        /// </summary>
        public static int ProcessId { get; } = Environment.ProcessId;

        /// <summary>
        ///     Gets the full application version, including both assembly and informational versions.
        /// </summary>
        public static string Version { get; } = $"{AssemblyVersion} ({InformationalVersion})";

        private static IConfigurationRoot Configuration { get; set; }
        private static OptionsAtStartup OptionsAtStartup { get; } = new OptionsAtStartup();

        [EnvironmentVariable("CONFIG")]
        [Argument('c', "config", "path to configuration file")]
        private static string ConfigurationFile { get; set; } = DefaultConfigurationFile;

        [Argument('g', "generate-cert", "generate X509 certificate and password for HTTPs")]
        private static bool GenerateCertificate { get; set; }

        [Argument('n', "no-logo", "suppress logo on startup")]
        private static bool NoLogo { get; set; }

        [Argument('e', "envars", "display environment variables")]
        private static bool ShowEnvironmentVariables { get; set; }

        [Argument('h', "help", "display command line usage")]
        private static bool ShowHelp { get; set; }

        [Argument('v', "version", "display version information")]
        private static bool ShowVersion { get; set; }

        /// <summary>
        ///     Entrypoint.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            // populate the properties above so that we can override the default config file if needed, and to
            // check if the application is being run in command mode (run task and quit).
            EnvironmentVariables.Populate(prefix: EnvironmentVariablePrefix);
            Arguments.Populate(clearExistingValues: false);

            if (ShowVersion)
            {
                Console.WriteLine(Version);
                return;
            }

            if (ShowHelp || ShowEnvironmentVariables)
            {
                if (!NoLogo)
                {
                    PrintLogo(Version);
                }

                if (ShowHelp)
                {
                    PrintCommandLineArguments(typeof(Options));
                }

                if (ShowEnvironmentVariables)
                {
                    PrintEnvironmentVariables(typeof(Options), EnvironmentVariablePrefix);
                }

                return;
            }

            if (GenerateCertificate)
            {
                GenerateX509Certificate(password: Guid.NewGuid().ToString(), filename: $"{AppName}.pfx");
                return;
            }

            // the application isn't being run in command mode. load all configuration values and proceed
            // with bootstrapping.
            try
            {
                Configuration = new ConfigurationBuilder()
                    .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile)
                    .Build();

                Configuration.GetSection(AppName)
                    .Bind(OptionsAtStartup, (o) => { o.BindNonPublicProperties = true; });

                if (OptionsAtStartup.Debug)
                {
                    Console.WriteLine($"Configuration:\n{Configuration.GetDebugView()}");
                }

                if (!OptionsAtStartup.TryValidate(out var result))
                {
                    Console.WriteLine(result.GetResultView());
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid configuration: {(!OptionsAtStartup.Debug ? ex : ex.Message)}");
                return;
            }

            Log.Logger = (OptionsAtStartup.Debug ? new LoggerConfiguration().MinimumLevel.Debug() : new LoggerConfiguration().MinimumLevel.Information())
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("slskd.API.Authentication.PassthroughAuthenticationHandler", LogEventLevel.Information)
                .Enrich.WithProperty("Version", Version)
                .Enrich.WithProperty("InstanceName", OptionsAtStartup.InstanceName)
                .Enrich.WithProperty("InvocationId", InvocationId)
                .Enrich.WithProperty("ProcessId", ProcessId)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: (OptionsAtStartup.Debug ? "[{SourceContext}] [{SoulseekContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Async(config =>
                    config.File(
                        Path.Combine(AppContext.BaseDirectory, "logs", $"{AppName}-.log"),
                        outputTemplate: (OptionsAtStartup.Debug ? "[{SourceContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day))
                .WriteTo.Conditional(
                    e => !string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki),
                    config => config.GrafanaLoki(
                        OptionsAtStartup.Logger.Loki ?? string.Empty,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .CreateLogger();

            var logger = Log.ForContext(typeof(Program));

            try
            {
                VerifyDirectory(OptionsAtStartup.Directories.App, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(OptionsAtStartup.Directories.Incomplete, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(OptionsAtStartup.Directories.Downloads, createIfMissing: true, verifyWriteable: true);

                foreach (var share in OptionsAtStartup.Directories.Shared)
                {
                    if (!VerifyDirectory(share, createIfMissing: false, verifyWriteable: false))
                    {
                        logger.Warning("Shared directory {Directory} does not exist", share);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Filesystem exception: {Message}", ex.Message);
                return;
            }

            if (!OptionsAtStartup.NoLogo)
            {
                PrintLogo(Version);
            }

            if (ConfigurationFile != DefaultConfigurationFile && !IOFile.Exists(ConfigurationFile))
            {
                logger.Warning($"Specified configuration file '{ConfigurationFile}' could not be found and was not loaded.");
            }

            logger.Information("Version: {Version}", Version);
            logger.Information("Instance Name: {InstanceName}", OptionsAtStartup.InstanceName);
            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);

            if (!string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki))
            {
                logger.Information("Forwarding logs to Grafana Loki instance at {LoggerLokiUrl}", OptionsAtStartup.Logger.Loki);
            }

            try
            {
                if (OptionsAtStartup.Feature.Prometheus)
                {
                    using var runtimeMetrics = DotNetRuntimeStatsBuilder.Default().StartCollecting();
                }

                var webHost = WebHost.CreateDefaultBuilder(args)
                    .SuppressStatusMessages(true)
                    .ConfigureAppConfiguration((hostingContext, builder) =>
                    {
                        builder.Sources.Clear();
                        builder.AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile);
                    })
                    .UseSerilog()
                    .UseUrls()
                    .UseKestrel(options =>
                    {
                        logger.Information($"Listening for HTTP requests at http://{IPAddress.Any}:{OptionsAtStartup.Web.Port}/");
                        options.Listen(IPAddress.Any, OptionsAtStartup.Web.Port);

                        logger.Information($"Listening for HTTPS requests at https://{IPAddress.Any}:{OptionsAtStartup.Web.Https.Port}/");
                        options.Listen(IPAddress.Any, OptionsAtStartup.Web.Https.Port, listenOptions =>
                        {
                            var cert = OptionsAtStartup.Web.Https.Certificate;

                            if (!string.IsNullOrEmpty(cert.Pfx))
                            {
                                logger.Information($"Using certificate from {cert.Pfx}");
                                listenOptions.UseHttps(cert.Pfx, cert.Password);
                            }
                            else
                            {
                                logger.Information($"Using randomly generated self-signed certificate");
                                listenOptions.UseHttps(X509.Generate(subject: AppName));
                            }
                        });
                    })
                    .UseStartup<Startup>()
                    .Build();

                if (OptionsAtStartup.NoStart)
                {
                    logger.Information("Qutting because 'no-start' option is enabled");
                    return;
                }

                webHost.Run();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IConfigurationBuilder AddConfigurationProviders(this IConfigurationBuilder builder, string environmentVariablePrefix, string configurationFile)
        {
            configurationFile = Path.GetFullPath(configurationFile);

            var multiValuedArguments = typeof(Options)
                .GetPropertiesRecursively()
                .Where(p => p.PropertyType.IsArray)
                .SelectMany(p =>
                    p.CustomAttributes
                        .Where(a => a.AttributeType == typeof(ArgumentAttribute))
                        .Select(a => new[] { a.ConstructorArguments[0].Value, a.ConstructorArguments[1].Value })
                        .SelectMany(v => v))
                .Select(v => v.ToString())
                .Where(v => v != "\u0000")
                .ToArray();

            return builder
                .AddDefaultValues(
                    targetType: typeof(Options))
                .AddEnvironmentVariables(
                    targetType: typeof(Options),
                    prefix: environmentVariablePrefix)
                .AddYamlFile(
                    path: Path.GetFileName(configurationFile),
                    targetType: typeof(Options),
                    optional: true,
                    reloadOnChange: true,
                    provider: new PhysicalFileProvider(Path.GetDirectoryName(configurationFile), ExclusionFilters.None)) // required for locations outside of the app directory
                .AddCommandLine(
                    targetType: typeof(Options),
                    multiValuedArguments,
                    commandLine: Environment.CommandLine);
        }

        private static void GenerateX509Certificate(string password, string filename)
        {
            Console.WriteLine("Generating X509 certificate...");
            filename = Path.Combine(AppContext.BaseDirectory, filename);

            var cert = X509.Generate(subject: AppName, password, X509KeyStorageFlags.Exportable);
            IOFile.WriteAllBytes(filename, cert.Export(X509ContentType.Pkcs12, password));

            Console.WriteLine($"Password: {password}");
            Console.WriteLine($"Certificate exported to {filename}");
        }

        private static void PrintCommandLineArguments(Type targetType)
        {
            static string GetLongName(string longName, Type type)
                => type == typeof(bool) ? longName : $"{longName} <{type.ToColloquialString().ToLowerInvariant()}>";

            var lines = new List<(string Item, string Description)>();

            void Map(Type type)
            {
                var defaults = Activator.CreateInstance(type);
                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ArgumentAttribute));
                    var descriptionAttribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DescriptionAttribute));
                    var isRequired = property.CustomAttributes.Any(a => a.AttributeType == typeof(RequiredAttribute));

                    if (attribute != default)
                    {
                        var shortName = (char)attribute.ConstructorArguments[0].Value;
                        var longName = (string)attribute.ConstructorArguments[1].Value;
                        var description = descriptionAttribute?.ConstructorArguments[0].Value;

                        var suffix = isRequired ? " (required)" : $" (default: {property.GetValue(defaults) ?? "<null>"})";
                        var item = $"{(shortName == default ? "  " : $"{shortName}|")}--{GetLongName(longName, property.PropertyType)}";
                        var desc = $"{description}{(property.PropertyType == typeof(bool) ? string.Empty : suffix)}";
                        lines.Add(new(item, desc));
                    }
                    else
                    {
                        Map(property.PropertyType);
                    }
                }
            }

            Map(targetType);

            var longestItem = lines.Max(l => l.Item.Length);

            Console.WriteLine("\nusage: slskd [arguments]\n");
            Console.WriteLine("arguments:\n");

            foreach (var line in lines)
            {
                Console.WriteLine($"  {line.Item.PadRight(longestItem)}   {line.Description}");
            }
        }

        private static void PrintEnvironmentVariables(Type targetType, string prefix)
        {
            static string GetName(string name, Type type) => $"{name} <{type.ToColloquialString().ToLowerInvariant()}>";

            var lines = new List<(string Item, string Description)>();

            void Map(Type type)
            {
                var defaults = Activator.CreateInstance(type);
                var props = type.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in props)
                {
                    var attribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(EnvironmentVariableAttribute));
                    var descriptionAttribute = property.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(DescriptionAttribute));
                    var isRequired = property.CustomAttributes.Any(a => a.AttributeType == typeof(RequiredAttribute));

                    if (attribute != default)
                    {
                        var name = (string)attribute.ConstructorArguments[0].Value;
                        var description = descriptionAttribute?.ConstructorArguments[0].Value;

                        var suffix = isRequired ? " (required)" : $" (default: {property.GetValue(defaults) ?? "<null>"})";
                        var item = $"{prefix}{GetName(name, property.PropertyType)}";
                        var desc = $"{description}{(type == typeof(bool) ? string.Empty : suffix)}";
                        lines.Add(new(item, desc));
                    }
                    else
                    {
                        Map(property.PropertyType);
                    }
                }
            }

            Map(targetType);

            var longestItem = lines.Max(l => l.Item.Length);

            Console.WriteLine("\nenvironment variables (arguments and config file have precedence):\n");

            foreach (var line in lines)
            {
                Console.WriteLine($"  {line.Item.PadRight(longestItem)}   {line.Description}");
            }
        }

        private static void PrintLogo(string version)
        {
            var padding = 56 - version.Length;
            var paddingLeft = padding / 2;
            var paddingRight = paddingLeft + (padding % 2);

            var centeredVersion = new string(' ', paddingLeft) + version + new string(' ', paddingRight);

            var logos = new[]
            {
                $@"
                ▄▄▄▄               ▄▄▄▄          ▄▄▄▄
     ▄▄▄▄▄▄▄    █  █    ▄▄▄▄▄▄▄    █  █▄▄▄    ▄▄▄█  █
     █__ --█    █  █    █__ --█    █    ◄█    █  -  █
     █▄▄▄▄▄█    █▄▄█    █▄▄▄▄▄█    █▄▄█▄▄█    █▄▄▄▄▄█",
                @$"
                     ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
               ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
               █__ --█  █__ --█    ◄█  -  █
               █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█",
            };

            var logo = logos[new Random().Next(0, logos.Length)];

            var banner = @$"
{logo}
╒════════════════════════════════════════════════════════╕
│           GNU AFFERO GENERAL PUBLIC LICENSE            │
│                   https://slskd.org                    │
│                                                        │
│{centeredVersion}│
└────────────────────────────────────────────────────────┘";

            Console.WriteLine(banner);
        }

        private static bool VerifyDirectory(string directory, bool createIfMissing = true, bool verifyWriteable = true)
        {
            if (!Directory.Exists(directory))
            {
                if (createIfMissing)
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Directory {directory} does not exist, and could not be created: {ex.Message}", ex);
                    }
                }
                else
                {
                    return false;
                }
            }

            if (verifyWriteable)
            {
                try
                {
                    var file = Guid.NewGuid().ToString();
                    var probe = Path.Combine(directory, file);
                    IOFile.WriteAllText(probe, string.Empty);
                    IOFile.Delete(probe);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Directory {directory} is not writeable: {ex.Message}", ex);
                }
            }

            return true;
        }
    }
}