namespace slskd
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.FileProviders.Physical;
    using Prometheus.DotNetRuntime;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using slskd.Common.Cryptography;
    using slskd.Configuration;
    using slskd.Validation;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;

    public static class ProgramExtensions
    {
        public static IConfigurationBuilder AddConfigurationProviders(this IConfigurationBuilder builder, string environmentVariablePrefix, string configurationFile)
        {
            configurationFile = Path.GetFullPath(configurationFile);

            return builder
                .AddDefaultValues(
                    map: Options.Map.Select(o => o.ToDefaultValue()))
                .AddEnvironmentVariables(
                    prefix: environmentVariablePrefix,
                    map: Options.Map.Select(o => o.ToEnvironmentVariable()))
                .AddYamlFile(
                    path: Path.GetFileName(configurationFile), 
                    optional: true, 
                    reloadOnChange: false,
                    provider: new PhysicalFileProvider(Path.GetDirectoryName(configurationFile), ExclusionFilters.None))
                .AddCommandLine(
                    commandLine: Environment.CommandLine,
                    map: Options.Map.Select(o => o.ToCommandLineArgument()));
        }
    }

    public class Program
    {
        public static readonly string AppName = "slskd";
        public static readonly string DefaultConfigurationFile = $"{AppName}.yml";
        public static readonly string EnvironmentVariablePrefix = $"{AppName.ToUpperInvariant()}_";
        
        public static Guid InvocationId { get; } = Guid.NewGuid();
        public static int ProcessId { get; } = Environment.ProcessId;
        public static Version AssemblyVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version;
        public static string InformationalVersion { get; } = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        public static string Version { get; } = $"{AssemblyVersion} ({InformationalVersion})";

        [CommandLineArgument('n', "no-logo", "suppress logo on startup")]
        private static bool NoLogo { get; set; }

        [CommandLineArgument('e', "envars", "display environment variables")]
        private static bool ShowEnvironmentVariables { get; set; }

        [CommandLineArgument('h', "help", "display command line usage")]
        private static bool ShowHelp { get; set; }

        [CommandLineArgument('v', "version", "display version information")]
        private static bool ShowVersion { get; set; }

        [CommandLineArgument('g', "generate-cert", "generate X509 certificate and password for HTTPs")]
        private static bool GenerateCertificate { get; set; }

        [EnvironmentVariable("CONFIG")]
        [CommandLineArgument('c', "config", "path to configuration file")]
        private static string ConfigurationFile { get; set; } = DefaultConfigurationFile;

        private static Options Options { get; } = new Options();
        private static IConfigurationRoot Configuration { get; set; }

        public static void Main(string[] args)
        {
            EnvironmentVariables.Populate(prefix: EnvironmentVariablePrefix);
            CommandLineArguments.Populate(clearExistingValues: false);

            if (ShowVersion)
            {
                Console.WriteLine(Version);
                return;
            }

            if (ShowHelp || ShowEnvironmentVariables)
            {
                if (!NoLogo) PrintLogo(Version);
                if (ShowHelp) PrintCommandLineArguments(Options.Map);
                if (ShowEnvironmentVariables) PrintEnvironmentVariables(Options.Map, EnvironmentVariablePrefix);
                return;
            }

            if (GenerateCertificate)
            {
                GenerateX509Certificate(password: Guid.NewGuid().ToString(), filename: $"{AppName}.pfx");
                return;
            }

            try
            {
                Configuration = new ConfigurationBuilder()
                    .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile)
                    .Build();

                Configuration.GetSection("slskd")
                    .Bind(Options, (o) => { o.BindNonPublicProperties = true; });

                if (!Options.TryValidate(out var result))
                {
                    Console.WriteLine(result.GetResultView("Invalid configuration:"));
                    return;
                }

                var cert = Options.Web.Https.Certificate;

                if (!string.IsNullOrEmpty(cert.Pfx) && !X509.TryValidate(cert.Pfx, cert.Password, out var certResult))
                {
                    Console.WriteLine($"Invalid HTTPs certificate: {certResult}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid configuration: {(Options.Debug ? ex : ex.Message)}");
                return;
            }

            if (!Options.NoLogo)
            {
                PrintLogo(Version);
            }

            if (Options.Debug)
            {
                Console.WriteLine("Configuration:");
                Console.WriteLine(Configuration.GetDebugView());

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("Version", Version)
                    .Enrich.WithProperty("InstanceName", Options.InstanceName)
                    .Enrich.WithProperty("InvocationId", InvocationId)
                    .Enrich.WithProperty("ProcessId", ProcessId)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "[{SourceContext}] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Async(config =>
                        config.File(
                            "logs/slskd-.log",
                            outputTemplate: "[{SourceContext}] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                            rollingInterval: RollingInterval.Day))
                    .WriteTo.Conditional(
                        e => !string.IsNullOrEmpty(Options.Logger.Loki),
                        config => config.GrafanaLoki(
                            Options.Logger.Loki ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
            }
            else
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("slskd.Security.PassthroughAuthenticationHandler", LogEventLevel.Information)
                    .Enrich.WithProperty("Version", Version)
                    .Enrich.WithProperty("InstanceName", Options.InstanceName)
                    .Enrich.WithProperty("InvocationId", InvocationId)
                    .Enrich.WithProperty("ProcessId", ProcessId)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Async(config =>
                        config.File(
                            "logs/slskd-.log",
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                            rollingInterval: RollingInterval.Day))
                    .WriteTo.Conditional(
                        e => !string.IsNullOrEmpty(Options.Logger.Loki),
                        config => config.GrafanaLoki(
                            Options.Logger.Loki ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
            }

            var logger = Log.ForContext<Program>();

            if (ConfigurationFile != DefaultConfigurationFile && !File.Exists(ConfigurationFile))
            {
                logger.Warning($"Specified configuration file '{ConfigurationFile}' could not be found and was not loaded.");
            }

            logger.Information("Version: {Version}", Version);
            logger.Information("Instance Name: {InstanceName}", Options.InstanceName);
            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);

            if (!string.IsNullOrEmpty(Options.Logger.Loki))
            {
                logger.Information("Forwarding logs to Grafana Loki instance at {LoggerLokiUrl}", Options.Logger.Loki);
            }

            try
            {
                if (Options.Feature.Prometheus)
                {
                    using var runtimeMetrics = DotNetRuntimeStatsBuilder.Default().StartCollecting();
                }

                WebHost.CreateDefaultBuilder(args)
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
                        logger.Information($"Listening for HTTP requests at http://{IPAddress.Any}:{Options.Web.Port}/");
                        options.Listen(IPAddress.Any, Options.Web.Port);

                        logger.Information($"Listening for HTTPS requests at https://{IPAddress.Any}:{Options.Web.Https.Port}/");
                        options.Listen(IPAddress.Any, Options.Web.Https.Port, listenOptions =>
                        {
                            var cert = Options.Web.Https.Certificate;

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
                    .Build()
                    .Run();
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

        public static void GenerateX509Certificate(string password, string filename)
        {
            Console.WriteLine("Generating X509 certificate...");
            filename = Path.Combine(AppContext.BaseDirectory, filename);

            var cert = X509.Generate(subject: AppName, password, X509KeyStorageFlags.Exportable);
            File.WriteAllBytes(filename, cert.Export(X509ContentType.Pkcs12, password));

            Console.WriteLine($"Password: {password}");
            Console.WriteLine($"Certificate exported to {filename}");
        }

        public static void PrintCommandLineArguments(IEnumerable<Option> map)
        {
            static string GetLongName(string longName, Type type)
                => type == typeof(bool) ? longName : $"{longName} <{type.Name.ToLowerInvariant()}>";

            var longestName = map.Where(a => !string.IsNullOrEmpty(a.LongName)).Select(a => GetLongName(a.LongName, a.Type)).Max(n => n.Length);

            Console.WriteLine("\nusage: slskd [arguments]\n");
            Console.WriteLine("arguments:\n");

            foreach (Option item in map)
            {
                var (shortName, longName, _, key, type, defaultValue, description) = item;

                if (shortName == default && string.IsNullOrEmpty(longName))
                {
                    continue;
                }

                var suffix = type == typeof(bool) ? string.Empty : $" (default: {defaultValue ?? "<null>"})";
                var result = $"  {shortName}{(shortName == default ? " " : "|")}--{GetLongName(longName, type).PadRight(longestName + 3)}{description}{suffix}";
                Console.WriteLine(result);
            }
        }

        public static void PrintEnvironmentVariables(IEnumerable<Option> map, string prefix)
        {
            static string GetName(string name, Type type) => $"{name} <{type.Name.ToLowerInvariant()}>";

            var longestName = map.Select(a => GetName(a.EnvironmentVariable, a.Type)).Max(n => n.Length);

            Console.WriteLine("\nenvironment variables (arguments and config file have precedence):\n");

            foreach (Option item in map)
            {
                var (_, _, environmentVariable, key, type, defaultValue, description) = item;

                if (string.IsNullOrEmpty(environmentVariable))
                {
                    continue;
                }

                var suffix = type == typeof(bool) ? string.Empty : $" (default: {defaultValue ?? "<null>"})";
                var result = $"  {prefix}{GetName(environmentVariable, type).PadRight(longestName + 3)}{description}{suffix}";
                Console.WriteLine(result);
            }
        }

        private static void PrintLogo(string version)
        {
            var padding = 56 - version.Length;
            var paddingLeft = padding / 2;
            var paddingRight = paddingLeft + (padding % 2);

            var centeredVersion = new string(' ', paddingLeft) + version + new string(' ', paddingRight);

            var banner = @$"
                ▄▄▄▄               ▄▄▄▄          ▄▄▄▄
     ▄▄▄▄▄▄▄    █  █    ▄▄▄▄▄▄▄    █  █▄▄▄    ▄▄▄█  █
     █__ --█    █  █    █__ --█    █    ◄█    █  -  █
     █▄▄▄▄▄█    █▄▄█    █▄▄▄▄▄█    █▄▄█▄▄█    █▄▄▄▄▄█
╒════════════════════════════════════════════════════════╕
│           GNU AFFERO GENERAL PUBLIC LICENSE            │
│                   https://slskd.org                    │
│                                                        │
│{centeredVersion}│
└────────────────────────────────────────────────────────┘";

            Console.WriteLine(banner);
        }
    }
}