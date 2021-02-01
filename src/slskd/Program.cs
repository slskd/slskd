namespace slskd
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Prometheus.DotNetRuntime;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

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
                if (ShowHelp) PrintCommandLineArguments();
                if (ShowEnvironmentVariables) PrintEnvironmentVariables(EnvironmentVariablePrefix);
                return;
            }

            var configuration = new ConfigurationBuilder()
                .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile)
                .Build();

            var options = new Options();

            configuration.GetSection("slskd").Bind(options, (o) => { o.BindNonPublicProperties = true; });

            if (!options.NoLogo)
            {
                PrintLogo(Version);
            }

            if (options.Debug)
            {
                Console.WriteLine(configuration.GetDebugView());
                Console.WriteLine(JsonSerializer.Serialize(options, new JsonSerializerOptions() { WriteIndented = true }));

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("Version", Version)
                    .Enrich.WithProperty("InstanceName", options.InstanceName)
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
                        e => !string.IsNullOrEmpty(options.Logger.Loki),
                        config => config.GrafanaLoki(
                            options.Logger.Loki ?? string.Empty,
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
                    .Enrich.WithProperty("InstanceName", options.InstanceName)
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
                        e => !string.IsNullOrEmpty(options.Logger.Loki),
                        config => config.GrafanaLoki(
                            options.Logger.Loki ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
            }

            var logger = Log.ForContext<Program>();

            logger.Information("Version: {Version}", Version);
            logger.Information("Instance Name: {InstanceName}", options.InstanceName);
            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);

            if (!string.IsNullOrEmpty(options.Logger.Loki))
            {
                logger.Information("Forwarding logs to Grafana Loki instance at {LoggerLokiUrl}", options.Logger.Loki);
            }

            try
            {
                if (options.Feature.Prometheus)
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
                    .UseUrls(
                        $"http://0.0.0.0:{options.Web.Port}", 
                        $"https://0.0.0.0:{options.Web.Https.Port}")
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

        public static void PrintCommandLineArguments()
        {
            static string GetLongName(string longName, Type type)
                => type == typeof(bool) ? longName : $"{longName} <{type.Name.ToLowerInvariant()}>";

            var longestName = Options.Map.Where(a => !string.IsNullOrEmpty(a.LongName)).Select(a => GetLongName(a.LongName, a.Type)).Max(n => n.Length);

            Console.WriteLine("\nusage: slskd [arguments]\n");
            Console.WriteLine("arguments:\n");

            foreach (Option item in Options.Map)
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

        public static void PrintEnvironmentVariables(string prefix)
        {
            static string GetName(string name, Type type) => $"{name} <{type.Name.ToLowerInvariant()}>";

            var longestName = Options.Map.Select(a => GetName(a.EnvironmentVariable, a.Type)).Max(n => n.Length);

            Console.WriteLine("\nenvironment variables (arguments and config.yml have precedence):\n");

            foreach (Option item in Options.Map)
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