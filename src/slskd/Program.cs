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
        ///     The url to the issues/support site.
        /// </summary>
        public static readonly string IssuesUrl = "https://github.com/slskd/slskd/issues";

        /// <summary>
        ///     The global prefix for environment variables.
        /// </summary>
        public static readonly string EnvironmentVariablePrefix = $"{AppName.ToUpperInvariant()}_";

        /// <summary>
        ///     The default XML documentation filename.
        /// </summary>
        public static readonly string XmlDocumentationFile = Path.Combine(AppContext.BaseDirectory, "etc", $"{AppName}.xml");

        /// <summary>
        ///     The default application data directory.
        /// </summary>
        public static readonly string DefaultAppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), AppName);

        /// <summary>
        ///     Gets the assembly version of the application.
        /// </summary>
        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        public static readonly Version AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.Equals(new Version(1, 0, 0, 0)) ? new Version(0, 0, 0, 0) : Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        ///     Gets the informational version of the application, including the git sha at the latest commit.
        /// </summary>
        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        public static readonly string InformationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion == "1.0.0" ? "0.0.0" : Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        ///     Gets the full application version, including both assembly and informational versions.
        /// </summary>
        public static readonly string Version = $"{AssemblyVersion} ({InformationalVersion})";

        /// <summary>
        ///     Gets a value indicating whether the current version is a Canary build.
        /// </summary>
        public static readonly bool IsCanary = AssemblyVersion.Revision == 65534;

        /// <summary>
        ///     Gets the unique Id of this application invocation.
        /// </summary>
        public static readonly Guid InvocationId = Guid.NewGuid();

        /// <summary>
        ///     Gets the Id of the current application process.
        /// </summary>
        public static readonly int ProcessId = Environment.ProcessId;

        /// <summary>
        ///     Occurs when a new log event is emitted.
        /// </summary>
        public static event EventHandler<LogRecord> LogEmitted;

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument(default, "app-dir", "path where application data is saved")]
        [EnvironmentVariable("APP_DIR")]
        public static string AppDirectory { get; private set; } = DefaultAppDirectory;

        /// <summary>
        ///     Gets the fully qualified path to the application configuration file.
        /// </summary>
        public static string ConfigurationFile => Path.Combine(AppDirectory, $"{AppName}.yml");

        /// <summary>
        ///     Gets the default downloads directory.
        /// </summary>
        public static string DefaultDownloadsDirectory => Path.Combine(AppDirectory, "downloads");

        /// <summary>
        ///     Gets the default incomplete download directory.
        /// </summary>
        public static string DefaultIncompleteDirectory => Path.Combine(AppDirectory, "incomplete");

        /// <summary>
        ///     Gets a buffer containing the last few log events.
        /// </summary>
        public static ConcurrentFixedSizeQueue<LogRecord> LogBuffer { get; } = new ConcurrentFixedSizeQueue<LogRecord>(size: 100);

        private static IConfigurationRoot Configuration { get; set; }
        private static OptionsAtStartup OptionsAtStartup { get; } = new OptionsAtStartup();

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
                GenerateX509Certificate(password: Cryptography.Random.GetBytes(16).ToBase62String(), filename: $"{AppName}.pfx");
                return;
            }

            // the application isn't being run in command mode. verify (create if needed) default application
            // directories. if the downloads or complete directories are overridden in config, those will be
            // validated after the config is loaded.
            try
            {
                VerifyDirectory(AppDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(Path.Combine(AppDirectory, "data"), createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DefaultDownloadsDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DefaultIncompleteDirectory, createIfMissing: true, verifyWriteable: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Filesystem exception: {ex.Message}");
                return;
            }

            // load and validate the configuration
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
                .MinimumLevel.Override("System.Net.Http.HttpClient", OptionsAtStartup.Debug ? LogEventLevel.Warning : LogEventLevel.Fatal)
                .MinimumLevel.Override("slskd.Authentication.PassthroughAuthenticationHandler", LogEventLevel.Warning)
                .Enrich.WithProperty("Version", Version)
                .Enrich.WithProperty("InstanceName", OptionsAtStartup.InstanceName)
                .Enrich.WithProperty("InvocationId", InvocationId)
                .Enrich.WithProperty("ProcessId", ProcessId)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: (OptionsAtStartup.Debug ? "[{SourceContext}] [{SoulseekContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Async(config =>
                    config.File(
                        Path.Combine(AppDirectory, "logs", $"{AppName}-.log"),
                        outputTemplate: (OptionsAtStartup.Debug ? "[{SourceContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day))
                .WriteTo.Conditional(
                    e => !string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki),
                    config => config.GrafanaLoki(
                        OptionsAtStartup.Logger.Loki ?? string.Empty,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .WriteTo.Sink(new DelegatingSink(logEvent =>
                {
                    var record = new LogRecord()
                    {
                        Timestamp = logEvent.Timestamp.LocalDateTime,
                        Context = logEvent.Properties["SourceContext"].ToString().TrimStart('"').TrimEnd('"'),
                        Level = logEvent.Level.ToString(),
                        Message = logEvent.RenderMessage().TrimStart('"').TrimEnd('"'),
                    };

                    LogBuffer.Enqueue(record);
                    LogEmitted?.Invoke(null, record);
                }))
                .CreateLogger();

            var logger = Log.ForContext(typeof(Program));

            if (!OptionsAtStartup.NoLogo)
            {
                PrintLogo(Version);
            }

            logger.Information("Version: {Version}", Version);

            if (IsCanary)
            {
                logger.Warning("This is a canary build");
                logger.Warning("Canary builds are considered UNSTABLE and may be completely BROKEN");
                logger.Warning($"Please report any issues here: {IssuesUrl}");
            }

            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);
            logger.Information("Instance Name: {InstanceName}", OptionsAtStartup.InstanceName);

            logger.Information("Using application directory {AppDirectory}", AppDirectory);
            logger.Information("Using configuration file {ConfigurationFile}", ConfigurationFile);

            // for convenience, try to copy the example configuration file to the application directory if no configuration
            // file exists. if any part fails, just move on silently.
            if (!IOFile.Exists(ConfigurationFile))
            {
                try
                {
                    logger.Warning("Configuration file {ConfigurationFile} does not exist; creating from example", ConfigurationFile);
                    var source = Path.Combine(AppContext.BaseDirectory, "config", $"{AppName}.example.yml");
                    var destination = ConfigurationFile;
                    IOFile.Copy(source, destination);
                }
                catch (Exception ex)
                {
                    logger.Error("Failed to create configuration file {ConfigurationFile}: {Message}", ConfigurationFile, ex.Message);
                }
            }

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

            var logo = logos[new System.Random().Next(0, logos.Length)];

            var banner = @$"
{logo}
╒════════════════════════════════════════════════════════╕
│           GNU AFFERO GENERAL PUBLIC LICENSE            │
│                   https://slskd.org                    │
│                                                        │
│{centeredVersion}│";

            if (IsCanary)
            {
                banner += "\n│■■■■■■■■■■■■■■■■■■■■■■■► CANARY ◄■■■■■■■■■■■■■■■■■■■■■■■│";
            }

            banner += "\n└────────────────────────────────────────────────────────┘";

            Console.WriteLine(banner);
        }

        private static void VerifyDirectory(string directory, bool createIfMissing = true, bool verifyWriteable = true)
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
                    throw new IOException($"Directory {directory} does not exist");
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
        }
    }
}