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
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;
    using Utility.Extensions.Configuration.Yaml;

    public class Program
    {
        [Argument('h', "help", "print usage")]
        public static bool ShowHelp { get; private set; }
        
        [Argument('v', "version", "display version information")]
        public static bool ShowVersion { get; private set; }

        [EnvironmentVariable("SLSKD_DEBUG")]
        [Configuration("slskd:debug")]
        [Argument('d', "debug", "run in debug mode")]
        public static bool Debug { get; private set; }

        [EnvironmentVariable("SLSKD_NO_LOGO")]
        [Configuration("slskd:noLogo")]
        [Argument('n', "no-logo", "suppress logo on startup")]
        public static bool DisableLogo { get; private set; }
        
        [EnvironmentVariable("SLSKD_NO_AUTH")]
        [Configuration("slskd:noAuth")]
        [Argument('x', "no-auth", "disable authentication for web requests")]
        public static bool DisableAuthentication { get; private set; }

        [EnvironmentVariable("SLSKD_INSTANCE_NAME")]
        [Configuration("slskd:instanceName")]
        [Argument('i', "instance-name", "optional; a unique name for this instance")]
        public static string InstanceName { get; private set; } = "default";

        [EnvironmentVariable("SLSKD_PROMETHEUS")]
        [Configuration("slskd:prometheus")]
        [Argument('p', "prometheus", "enable collection and publish of prometheus metrics")]
        public static bool EnablePrometheus { get; private set; }

        [EnvironmentVariable("SLSKD_SWAGGER")]
        [Configuration("slskd:swagger")]
        [Argument('s', "swagger", "enable swagger documentation")]
        public static bool EnableSwagger { get; private set; }

        [EnvironmentVariable("SLSKD_LOKI")]
        [Configuration("slskd:loki")]
        [Argument(default, "loki", "the url to a Grafana Loki instance to which to log")]
        public static string LokiUrl { get; private set; }

        public static bool EnableLoki { get; private set; }
        public static string Version { get; private set; }
        public static Guid InvocationId { get; private set; }
        public static int ProcessId { get; private set; }

        public static readonly string ConfigurationFile = "config.yml";

        public static void Main(string[] args)
        {
            try
            {
                EnvironmentVariables.Populate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing environment variables: {ex.Message}");
                return;
            }

            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddYamlFile(ConfigurationFile, optional: true, reloadOnChange: false)
                    .Build();

                config.Populate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing configuration file '{ConfigurationFile}': {ex.Message}");
                return;
            }

            try
            {
                Arguments.Populate(clearExistingValues: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing command line input: {ex.Message}");
                return;
            }

            Console.WriteLine(LokiUrl);

            Version = ComputeVersion();

            if (ShowVersion)
            {
                Console.WriteLine(Version);
                return;
            }

            if (ShowHelp)
            {
                if (!DisableLogo)
                {
                    PrintLogo(Version);
                }

                PrintHelp();
                return;
            }

            InvocationId = Guid.NewGuid();
            ProcessId = Environment.ProcessId;
            Debug = Debugger.IsAttached || Debug;
            EnableLoki = !string.IsNullOrEmpty(LokiUrl);

            ConfigureLogger();

            if (!DisableLogo)
            {
                PrintLogo(Version);
            }

            var logger = Log.ForContext<Program>();

            logger.Information("Version: {Version}", Version);
            logger.Information("Instance Name: {InstanceName}", InstanceName);
            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);

            if (DisableAuthentication)
            {
                logger.Warning("Authentication of web requests is DISABLED");
            }

            if (EnablePrometheus)
            {
                logger.Information("Publishing Prometheus metrics to /metrics");
            }

            if (EnableSwagger)
            {
                logger.Information("Publishing Swagger documentation to /swagger");
            }

            if (EnableLoki)
            {
                logger.Information("Logging to Loki instance at {LoggerLokiUrl}", LokiUrl);
            }

            try
            {
                if (EnablePrometheus)
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

        private static string ComputeVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            return $"{assemblyVersion} ({informationVersion})";
        }

        private static void ConfigureLogger()
        {
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
                        e => EnableLoki,
                        config => config.GrafanaLoki(
                            LokiUrl ?? string.Empty,
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
                        e => EnableLoki,
                        config => config.GrafanaLoki(
                            LokiUrl ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
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

        private static void PrintHelp()
        {
            PrintArguments();
            PrintEnvironmentVariables();
        }

        private static void PrintArguments()
        {
            static string GetLongName(ArgumentInfo info)
                => info.Property.PropertyType == typeof(bool) ? info.LongName : $"{info.LongName} <{info.Property.PropertyType.Name.ToLowerInvariant()}>";

            static string GetShortName(ArgumentInfo info)
                => info.ShortName > 0 ? $"-{info.ShortName}|" : "   ";

            var arguments = Arguments.GetArgumentInfo(typeof(Program));
            var longestName = arguments.Select(a => GetLongName(a)).Max(n => n.Length);

            Console.WriteLine("\nusage: slskd [arguments]\n");
            Console.WriteLine("arguments:\n");
            foreach (var info in arguments)
            {
                var envar = info.Property.CustomAttributes
                    ?.Where(a => a.AttributeType == typeof(EnvironmentVariableAttribute)).FirstOrDefault()
                    ?.ConstructorArguments.FirstOrDefault().Value;

                var result = $"  {GetShortName(info)}--{GetLongName(info).PadRight(longestName + 3)}{info.HelpText}";
                Console.WriteLine(result);
            }
        }

        private static void PrintEnvironmentVariables()
        {
            static string GetEnvironmentVariableName(ArgumentInfo info, bool includeType = false)
                => info.Property.CustomAttributes
                    ?.Where(a => a.AttributeType == typeof(EnvironmentVariableAttribute)).FirstOrDefault()
                    ?.ConstructorArguments.FirstOrDefault().Value?.ToString() + (includeType ? $" <{info.Property.PropertyType.Name.ToLowerInvariant()}>" : "");

            var arguments = Arguments.GetArgumentInfo(typeof(Program));
            var longestName = arguments.Select(a => GetEnvironmentVariableName(a, includeType: true)).Where(a => a !=null).Max(n => n.Length);

            Console.WriteLine("\nenvironment variables (arguments have precedence):\n");
            foreach (var info in arguments)
            {
                var envar = GetEnvironmentVariableName(info, includeType: false);

                if (string.IsNullOrEmpty(envar)) continue;

                var result = $"  {GetEnvironmentVariableName(info, includeType: true).PadRight(longestName + 3)}{info.HelpText}";
                Console.WriteLine(result);
            }
        }
    }
}
