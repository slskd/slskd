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
    using System.Reflection;
    using System.Text.Json;

    public class Program
    {
        public static string Version { get; private set; }
        public static Guid InvocationId { get; private set; }
        public static int ProcessId { get; private set; }

        public static readonly string ConfigurationFile = "config.yml";

        private static Action<BinderOptions> BinderOptions = (o) => { o.BindNonPublicProperties = true; };
        public static RuntimeOptions.slskd Options { get; private set; } = new();


        public static void Main(string[] args)
        {
            new ConfigurationBuilder()
                .MapCommandLineArguments(RuntimeOptions.ArgumentMap, Environment.CommandLine)
                .Build()
                .GetSection("slskd")
                .Bind(Options, BinderOptions);

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Version = $"{assemblyVersion} ({informationVersion})";

            if (Options.ShowVersion)
            {
                Console.WriteLine(Version);
                return;
            }

            if (Options.ShowHelp)
            {
                if (!InstanceOptions.DisableLogo)
                {
                    PrintLogo(Version);
                }

                InstanceOptions.PrintHelp();
                return;
            }

            new ConfigurationBuilder()
                .MapEnvironmentVariables(RuntimeOptions.EnvironmentVariableMap)
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddYamlFile(ConfigurationFile, optional: true, reloadOnChange: false)
                .MapCommandLineArguments(RuntimeOptions.ArgumentMap, Environment.CommandLine)
                .Build()
                .GetSection("slskd")
                .Bind(Options, BinderOptions);

            Console.WriteLine(JsonSerializer.Serialize(Options));

            return;

            InvocationId = Guid.NewGuid();
            ProcessId = Environment.ProcessId;

            if (!InstanceOptions.DisableLogo)
            {
                PrintLogo(Version);
            }

            if (InstanceOptions.Debug || Debugger.IsAttached)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("Version", Version)
                    .Enrich.WithProperty("InstanceName", InstanceOptions.InstanceName)
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
                        e => InstanceOptions.EnableLoki,
                        config => config.GrafanaLoki(
                            InstanceOptions.LokiUrl ?? string.Empty,
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
                    .Enrich.WithProperty("InstanceName", InstanceOptions.InstanceName)
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
                        e => InstanceOptions.EnableLoki,
                        config => config.GrafanaLoki(
                            InstanceOptions.LokiUrl ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
            }

            var logger = Log.ForContext<Program>();

            logger.Information("Version: {Version}", Version);
            logger.Information("Instance Name: {InstanceName}", InstanceOptions.InstanceName);
            logger.Information("Invocation ID: {InvocationId}", InvocationId);
            logger.Information("Process ID: {ProcessId}", ProcessId);

            if (InstanceOptions.DisableAuthentication)
            {
                logger.Warning("Authentication of web requests is DISABLED");
            }

            if (InstanceOptions.EnablePrometheus)
            {
                logger.Information("Publishing Prometheus metrics to /metrics");
            }

            if (InstanceOptions.EnableSwagger)
            {
                logger.Information("Publishing Swagger documentation to /swagger");
            }

            if (InstanceOptions.EnableLoki)
            {
                logger.Information("Logging to Loki instance at {LoggerLokiUrl}", InstanceOptions.LokiUrl);
            }

            try
            {
                if (InstanceOptions.EnablePrometheus)
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
    }
}
