namespace slskd
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    public class Program
    {
        public static bool Debug { get; private set; }
        public static string InstanceName { get; private set; }
        public static bool LoggerLokiEnable { get; private set; }
        public static string LoggerLokiUrl { get; private set; }

        public static void Main(string[] args)
        {
            var runId = Guid.NewGuid();
            var processId = Environment.ProcessId;

            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var version = $"{assemblyVersion} ({informationVersion})";

            if (args.Any(a => a == "--version" || a == "-v"))
            {
                Console.WriteLine(version);
                return;
            }

            if (args.Any(a => a == "--help" || a == "-h"))
            {
                PrintHelp();
                return;
            }

            Debug = Debugger.IsAttached 
                || args.Any(a => a == "--debug" || a == "-d") 
                || Environment.GetEnvironmentVariable("SLSKD_DEBUG") != null;

            InstanceName = Environment.GetEnvironmentVariable("SLSKD_INSTANCE_NAME") ?? "default";

            LoggerLokiUrl = Environment.GetEnvironmentVariable("SLSKD_LOGGER_LOKI_URL");
            LoggerLokiEnable = !string.IsNullOrWhiteSpace(LoggerLokiUrl) && Environment.GetEnvironmentVariable("SLSKD_LOGGER_LOKI_ENABLE") != null;

            if (Debug)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .Enrich.WithProperty("InstanceName", InstanceName)
                    .Enrich.WithProperty("ProcessId", processId)
                    .Enrich.WithProperty("RunId", runId)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "[{SourceContext}] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Async(config => 
                        config.File(
                            "logs/slskd-.log", 
                            outputTemplate: "[{SourceContext}] [{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                            rollingInterval: RollingInterval.Day))
                    .WriteTo.Conditional(
                        e => LoggerLokiEnable,
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
                    .Enrich.WithProperty("InstanceName", InstanceName)
                    .Enrich.WithProperty("ProcessId", processId)
                    .Enrich.WithProperty("RunId", runId)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.Async(config => 
                        config.File(
                            "logs/slskd-.log", 
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                            rollingInterval: RollingInterval.Day))
                    .WriteTo.Conditional(
                        e => LoggerLokiEnable, 
                        config => config.GrafanaLoki(
                            LoggerLokiUrl ?? string.Empty,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                    .CreateLogger();
            }

            PrintBanner(version);

            var logger = Log.ForContext<Program>();

            logger.Information("Instance Name: {InstanceName}", InstanceName);
            logger.Information("Process ID: {ProcessId}", processId);
            logger.Information("Run ID: {RunId}", runId);

            if (LoggerLokiEnable)
            {
                logger.Information("Logging to Loki instance at {LoggerLokiUrl}", LoggerLokiUrl);
            }

            try
            {
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
            var banner = @$"
           oooo           oooo              .o8  
           `888           `888             '888  
   .oooo.o  888   .oooo.o  888  oooo   .oooo888  
  d88(  '8  888  d88(  '8  888 .8P'   d88' `888  
  `'Y88b.   888  `'Y88b.   888888.    888   888  
  o.  )88b  888  o.  )88b  888 `88b.  888   888  
  8''888P' o888o 8''888P' o888o o888o `Y8bod88P  
╒═══════════════════════════════════════════════╕
│       GNU AFFERO GENERAL PUBLIC LICENSE       │
│                   slskd.org                   │
│                                               │
│            {version}            │
└───────────────────────────────────────────────┘
            ";

            Console.WriteLine(banner);
        }

        public static void PrintHelp()
        {
            Console.WriteLine(@"
usage: slskd [options]
  --version|-v      displays the current version
  --debug|-d        runs the daemon in debug mode
  --help|-h         prints this help");
        }
    }
}
