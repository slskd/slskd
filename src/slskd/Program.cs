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
    using System.Linq;
    using System.Reflection;
    using Utility.CommandLine;

    public class Program
    {
        [Argument('h', "help", "print usage")]
        public static bool ShowHelp { get; private set; }
        
        [Argument('v', "version", "display version information")]
        public static bool ShowVersion { get; private set; }

        [Argument('d', "debug", "run in debug mode")]
        public static bool Debug { get; private set; }

        [Argument('n', "no-logo", "suppress logo on startup")]
        public static bool NoLogo { get; private set; }
        
        [Argument('i', "instance-name", "optional; a unique name for this instance")]
        public static string InstanceName { get; private set; }
        
        [Argument(default, "loki", "the url to a Grafana Loki instance to log to")]
        public static string LoggerLokiUrl { get; private set; }

        public static bool LoggerLokiEnabled { get; private set; }
        public static string Version { get; private set; }
        public static Guid InvocationId { get; private set; }
        public static int ProcessId { get; private set; }

        public static void Main(string[] args)
        {
            try
            {
                Arguments.Populate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing input: {ex.Message}");
            }

            InvocationId = Guid.NewGuid();
            ProcessId = Environment.ProcessId;

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Version = $"{assemblyVersion} ({informationVersion})";

            if (ShowVersion)
            {
                Console.WriteLine(Version);
                return;
            }

            if (ShowHelp)
            {
                if (!NoLogo) PrintBanner(Version);
                PrintHelp();
                return;
            }

            Debug = Debugger.IsAttached 
                || Debug 
                || Environment.GetEnvironmentVariable("SLSKD_DEBUG") != null;

            InstanceName ??= Environment.GetEnvironmentVariable("SLSKD_INSTANCE_NAME") ?? "default";

            LoggerLokiUrl ??= Environment.GetEnvironmentVariable("SLSKD_LOGGER_LOKI_URL");
            LoggerLokiEnabled = !string.IsNullOrEmpty(LoggerLokiUrl);
            
            if (Debug)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("Version", Version)
                    .Enrich.WithProperty("InstanceName", InstanceName)
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
                        e => LoggerLokiEnabled,
                        config => config.GrafanaLoki(
                            LoggerLokiUrl ?? string.Empty, 
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
                    .Enrich.WithProperty("InstanceName", InstanceName)
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
                        e => LoggerLokiEnabled, 
                        config => config.GrafanaLoki(
                            LoggerLokiUrl ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
            }

            if (!NoLogo)
            {
                PrintBanner(Version);
            }

            var logger = Log.ForContext<Program>();

            logger.Information("Version: {Version}", Version);
            logger.Information("Instance Name: {InstanceName}", InstanceName);
            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);

            if (LoggerLokiEnabled)
            {
                logger.Information("Logging to Loki instance at {LoggerLokiUrl}", LoggerLokiUrl);
            }

            try
            {
                using var runtimeMetrics = DotNetRuntimeStatsBuilder.Default().StartCollecting();
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
                    config.AddJsonFile("config.json", optional: true, reloadOnChange: false);
                })
                .UseSerilog()
                .UseStartup<Startup>();

        public static void PrintBanner(string version)
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

        public static void PrintHelp()
        {
            static string GetLongName(ArgumentInfo info)
                => info.Property.PropertyType == typeof(bool) ? info.LongName : $"{info.LongName} <{info.Property.PropertyType.Name.ToLowerInvariant()}>";

            static string GetShortName(ArgumentInfo info)
                => info.ShortName > 0 ? $"-{info.ShortName}|" : "   ";

            var arguments = Arguments.GetArgumentInfo(typeof(Program));
            var longestName = arguments.Select(a => GetLongName(a)).Max(n => n.Length);

            Console.WriteLine("\nusage: slskd [options]\n");
            Console.WriteLine("options:");
            foreach (var info in arguments)
            {
                var result = $" {GetShortName(info)}--{GetLongName(info).PadRight(longestName + 3)}{info.HelpText}";
                Console.WriteLine(result);
            }
        }
    }
}
