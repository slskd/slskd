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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    
    public class Program
    {
        [CommandLineArgument('v', "version", "display version information")]
        public static bool ShowVersion { get; private set; }

        [CommandLineArgument('h', "help", "display command line usage")]
        public static bool ShowHelp { get; private set; }

        [CommandLineArgument('e', "envars", "display environment variables")]
        public static bool ShowEnvironmentVariables { get; private set; }

        [CommandLineArgument('n', "no-logo", "suppress logo on startup")]
        public static bool NoLogo { get; private set; }

        public static string Version { get; private set; }
        public static Guid InvocationId { get; private set; }
        public static int ProcessId { get; private set; }

        public static readonly string ConfigurationFile = "config.yml";
        public static readonly string EnvironmentVariablePrefix = "SLSKD_";

        public static Configuration.Program Options { get; private set; } = new Configuration.Program();

        public static void Main(string[] args)
        {
            CommandLineArguments.Populate();

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Version = $"{assemblyVersion} ({informationVersion})";

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
                .AddEnvironmentVariables(
                    prefix: EnvironmentVariablePrefix,
                    map: Configuration.Map.Select(o => o.ToEnvironmentVariable()))
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddYamlFile(ConfigurationFile, optional: true, reloadOnChange: false)
                .AddCommandLine(
                    commandLine: Environment.CommandLine,
                    map: Configuration.Map.Select(o => o.ToCommandLineArgument()))
                .Build();

            configuration
                .GetSection("slskd")
                .Bind(Options, (o) => 
                { 
                    o.BindNonPublicProperties = true; 
                });

            if (Options.Debug)
            {
                Console.WriteLine(configuration.GetDebugView());
                Console.WriteLine(JsonSerializer.Serialize(Options));
            }

            InvocationId = Guid.NewGuid();
            ProcessId = Environment.ProcessId;

            if (!Options.NoLogo)
            {
                PrintLogo(Version);
            }

            if (Options.Debug || Debugger.IsAttached)
            {
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

            if (Options.NoAuth)
            {
                logger.Warning("Authentication of web requests is DISABLED");
            }

            if (Options.Feature.Prometheus)
            {
                logger.Information("Publishing Prometheus metrics to /metrics");
            }

            if (Options.Feature.Swagger)
            {
                logger.Information("Publishing Swagger documentation to /swagger");
            }

            if (!string.IsNullOrEmpty(Options.Logger.Loki))
            {
                logger.Information("Logging to Loki instance at {LoggerLokiUrl}", Options.Logger.Loki);
            }

            try
            {
                if (Options.Feature.Prometheus)
                {
                    using var runtimeMetrics = DotNetRuntimeStatsBuilder.Default().StartCollecting();
                }

                CreateWebHostBuilder(args).Build().Run();
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

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables(prefix: "SLSK_");
                    config.AddYamlFile(ConfigurationFile, optional: true, reloadOnChange: true);
                })
                .UseSerilog()
                .UseStartup<Startup>();

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

        public static void PrintCommandLineArguments()
        {
            static string GetLongName(string longName, Type type)
                => type == typeof(bool) ? longName : $"{longName} <{type.Name.ToLowerInvariant()}>";

            var longestName = Configuration.Map.Select(a => GetLongName(a.LongName, a.Type)).Max(n => n.Length);

            Console.WriteLine("\nusage: slskd [arguments]\n");
            Console.WriteLine("arguments:\n");

            foreach (Option option in Configuration.Map)
            {
                var (shortName, longName, _, type, key, description) = option;

                if (shortName == default && string.IsNullOrEmpty(longName))
                {
                    continue;
                }

                var result = $"  {shortName}|--{GetLongName(longName, type).PadRight(longestName + 3)}{description}";
                Console.WriteLine(result);
            }
        }

        public static void PrintEnvironmentVariables(string prefix)
        {
            static string GetName(string name, Type type) => $"{name} <{type.Name.ToLowerInvariant()}>";

            var longestName = Configuration.Map.Select(a => GetName(a.EnvironmentVariable, a.Type)).Max(n => n.Length);

            Console.WriteLine("\nenvironment variables (arguments and config.yml have precedence):\n");

            foreach (Option option in Configuration.Map)
            {
                var (_, _, environmentVariable, type, key, description) = option;

                if (string.IsNullOrEmpty(environmentVariable) || string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var result = $"  {EnvironmentVariablePrefix}{GetName(environmentVariable, type).PadRight(longestName + 3)}{description}";
                Console.WriteLine(result);
            }
        }
    }
}
