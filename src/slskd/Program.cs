namespace slskd
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Prometheus.DotNetRuntime;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using slskd.Configuration;
    using slskd.Validation;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    
    public static class ProgramExtensions
    {
        public static IConfigurationBuilder AddConfigurationProviders(this IConfigurationBuilder builder, string environmentVariablePrefix, string configurationFile)
        {
            return builder
                .AddDefaultValues(
                    map: Options.Map.Select(o => o.ToDefaultValue()))
                .AddEnvironmentVariables(
                    prefix: environmentVariablePrefix,
                    map: Options.Map.Select(o => o.ToEnvironmentVariable()))
                .AddYamlFile(
                    path: Path.Combine(AppContext.BaseDirectory, configurationFile), 
                    optional: true, 
                    reloadOnChange: false)
                .AddCommandLine(
                    commandLine: Environment.CommandLine,
                    map: Options.Map.Select(o => o.ToCommandLineArgument()));
        }
    }

    public class Program
    {
        private static readonly string ConfigurationFile = "slskd.yml";
        private static readonly string EnvironmentVariablePrefix = "SLSKD_";
        
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

        private static Options Options { get; } = new Options();
        private static IConfigurationRoot Configuration { get; set;  }

        public static void Main(string[] args)
        {
            CommandLineArguments.Populate();

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid configuration: {ex.Message}");
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
                    .ConfigureAppConfiguration((hostingContext, builder) =>
                    {
                        builder.Sources.Clear();
                        builder.AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile);
                    })
                    .UseSerilog()
                    .UseStartup<Startup>()
                    //.UseUrls(
                    //    $"http://+:{options.Web.Port}", 
                    //    $"https://+:{options.Web.Https.Port}")
                    .UseUrls($"http://+:{Options.Web.Port}")
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

            Console.WriteLine("\nenvironment variables (arguments and config.yml have precedence):\n");

            foreach (Option item in map)
            {
                var (_, _, environmentVariable, key, type, defaultValue, description) = item;

                if (string.IsNullOrEmpty(environmentVariable) || string.IsNullOrEmpty(key))
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