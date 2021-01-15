namespace slskd
{
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Events;
    using System;
    using System.Linq;
    using System.Reflection;

    public class Program
    {
        public static void Main(string[] args)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyVersion = assembly.GetName().Version;
            var informationVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            var version = $"{assemblyVersion} ({informationVersion})";

            if (args.Any(a => a == "--version" || a == "-v"))
            {
                Console.WriteLine(version);
                return;
            }

            PrintBanner(version);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                CreateWebHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables(prefix: "SLSK_");
                    config.AddJsonFile("config.json", optional: true, reloadOnChange: false);
                })
                .ConfigureLogging((context, logging) =>
                {
                    if (!System.Diagnostics.Debugger.IsAttached)
                    {
                        logging.ClearProviders();
                    }
                })
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
    }
}
