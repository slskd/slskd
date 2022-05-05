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
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.FileProviders.Physical;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi.Models;
    using Prometheus;
    using Prometheus.DotNetRuntime;
    using Prometheus.SystemMetrics;
    using Serilog;
    using Serilog.Events;
    using Serilog.Sinks.Grafana.Loki;
    using slskd.Authentication;
    using slskd.Configuration;
    using slskd.Core.API;
    using slskd.Cryptography;
    using slskd.Integrations.FTP;
    using slskd.Integrations.Pushbullet;
    using slskd.Messaging;
    using slskd.Search;
    using slskd.Search.API;
    using slskd.Shares;
    using slskd.Transfers;
    using slskd.Users;
    using slskd.Validation;
    using Soulseek;
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
        ///     Gets the unique Id of this application invocation.
        /// </summary>
        public static readonly Guid InvocationId = Guid.NewGuid();

        /// <summary>
        ///     Gets the Id of the current application process.
        /// </summary>
        public static readonly int ProcessId = Environment.ProcessId;

        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        private static readonly Version AssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.Equals(new Version(1, 0, 0, 0)) ? new Version(0, 0, 0, 0) : Assembly.GetExecutingAssembly().GetName().Version;

        /// <remarks>
        ///     Inaccurate when running locally.
        /// </remarks>
        private static readonly string InformationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion == "1.0.0" ? "0.0.0" : Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

        /// <summary>
        ///     Occurs when a new log event is emitted.
        /// </summary>
        public static event EventHandler<LogRecord> LogEmitted;

        /// <summary>
        ///     Gets the semantic application version.
        /// </summary>
        public static string SemanticVersion { get; } = InformationalVersion.Split('-').First();

        /// <summary>
        ///     Gets the full application version, including both assembly and informational versions.
        /// </summary>
        public static string FullVersion { get; } = $"{SemanticVersion} ({InformationalVersion})";

        /// <summary>
        ///     Gets a value indicating whether the current version is a Canary build.
        /// </summary>
        public static bool IsCanary { get; } = AssemblyVersion.Revision == 65534;

        /// <summary>
        ///     Gets a value indicating whether the current version is a Development build.
        /// </summary>
        public static bool IsDevelopment { get; } = new Version(0, 0, 0, 0) == AssemblyVersion;

        /// <summary>
        ///     Gets the path where application data is saved.
        /// </summary>
        [Argument('a', "app-dir", "path where application data is saved")]
        [EnvironmentVariable("APP_DIR")]
        public static string AppDirectory { get; private set; } = null;

        /// <summary>
        ///     Gets the fully qualified path to the application configuration file.
        /// </summary>
        [Argument('c', "config", "path to configuration file")]
        [EnvironmentVariable("CONFIG")]
        public static string ConfigurationFile { get; private set; } = null;

        /// <summary>
        ///     Gets the default fully qualified path to the configuration file.
        /// </summary>
        public static string DefaultConfigurationFile { get; private set; }

        /// <summary>
        ///     Gets the default downloads directory.
        /// </summary>
        public static string DefaultDownloadsDirectory { get; private set; }

        /// <summary>
        ///     Gets the default incomplete download directory.
        /// </summary>
        public static string DefaultIncompleteDirectory { get; private set; }

        /// <summary>
        ///     Gets a buffer containing the last few log events.
        /// </summary>
        public static ConcurrentFixedSizeQueue<LogRecord> LogBuffer { get; } = new ConcurrentFixedSizeQueue<LogRecord>(size: 100);

        private static IConfigurationRoot Configuration { get; set; }
        private static OptionsAtStartup OptionsAtStartup { get; } = new OptionsAtStartup();
        private static ILogger Log { get; set; } = new ConsoleWriteLineLogger();
        private static MemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions());

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

            // if a user has used one of the arguments above, perform the requested task, then quit
            if (ShowVersion)
            {
                Log.Information(FullVersion);
                return;
            }

            if (ShowHelp || ShowEnvironmentVariables)
            {
                if (!NoLogo)
                {
                    PrintLogo(FullVersion);
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

            // the application isn't being run in command mode. derive the application directory value
            // and defaults that are dependent upon it
            AppDirectory ??= DefaultAppDirectory;

            DefaultConfigurationFile = Path.Combine(AppDirectory, $"{AppName}.yml");
            DefaultDownloadsDirectory = Path.Combine(AppDirectory, "downloads");
            DefaultIncompleteDirectory = Path.Combine(AppDirectory, "incomplete");

            // the location of the configuration file might have been overriden by command line or envar.
            // if not, set it to the default.
            ConfigurationFile ??= DefaultConfigurationFile;

            // verify(create if needed) default application directories. if the downloads or complete
            // directories are overridden in config, those will be validated after the config is loaded.
            try
            {
                VerifyDirectory(AppDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(Path.Combine(AppDirectory, "data"), createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DefaultDownloadsDirectory, createIfMissing: true, verifyWriteable: true);
                VerifyDirectory(DefaultIncompleteDirectory, createIfMissing: true, verifyWriteable: true);
            }
            catch (Exception ex)
            {
                Log.Information($"Filesystem exception: {ex.Message}");
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
                    Log.Information($"Configuration:\n{Configuration.GetDebugView()}");
                }

                if (!OptionsAtStartup.TryValidate(out var result))
                {
                    Log.Information(result.GetResultView());
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Invalid configuration: {(!OptionsAtStartup.Debug ? ex : ex.Message)}");
                return;
            }

            ConfigureGlobalLogger();
            Log = Serilog.Log.ForContext(typeof(Program));

            if (!OptionsAtStartup.NoLogo)
            {
                PrintLogo(FullVersion);
            }

            Log.Information("Version: {Version}", FullVersion);

            if (IsDevelopment)
            {
                Log.Warning("This is a Development build; YMMV");
            }

            if (IsCanary)
            {
                Log.Warning("This is a canary build");
                Log.Warning("Canary builds are considered UNSTABLE and may be completely BROKEN");
                Log.Warning($"Please report any issues here: {IssuesUrl}");
            }

            Log.Information("Invocation ID: {InvocationId}", InvocationId);
            Log.Information("Process ID: {ProcessId}", ProcessId);
            Log.Information("Instance Name: {InstanceName}", OptionsAtStartup.InstanceName);

            Log.Information("Using application directory {AppDirectory}", AppDirectory);
            Log.Information("Using configuration file {ConfigurationFile}", ConfigurationFile);

            RecreateConfigurationFileIfMissing(ConfigurationFile);

            if (!string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki))
            {
                Log.Information("Forwarding logs to Grafana Loki instance at {LoggerLokiUrl}", OptionsAtStartup.Logger.Loki);
            }

            // bootstrap the ASP.NET application
            try
            {
                if (OptionsAtStartup.Feature.Prometheus)
                {
                    using var runtimeMetrics = DotNetRuntimeStatsBuilder.Default().StartCollecting();
                }

                var builder = WebApplication.CreateBuilder(args);

                builder.Configuration
                    .AddConfigurationProviders(EnvironmentVariablePrefix, ConfigurationFile);

                builder.Host
                    .UseSerilog();

                builder.WebHost
                    .UseUrls()
                    .UseKestrel(options =>
                    {
                        Log.Information($"Listening for HTTP requests at http://{IPAddress.Any}:{OptionsAtStartup.Web.Port}/");
                        options.Listen(IPAddress.Any, OptionsAtStartup.Web.Port);

                        if (!OptionsAtStartup.Web.Https.Disabled)
                        {
                            Log.Information($"Listening for HTTPS requests at https://{IPAddress.Any}:{OptionsAtStartup.Web.Https.Port}/");
                            options.Listen(IPAddress.Any, OptionsAtStartup.Web.Https.Port, listenOptions =>
                            {
                                var cert = OptionsAtStartup.Web.Https.Certificate;

                                if (!string.IsNullOrEmpty(cert.Pfx))
                                {
                                    Log.Information($"Using certificate from {cert.Pfx}");
                                    listenOptions.UseHttps(cert.Pfx, cert.Password);
                                }
                                else
                                {
                                    Log.Information($"Using randomly generated self-signed certificate");
                                    listenOptions.UseHttps(X509.Generate(subject: AppName));
                                }
                            });
                        }
                    });

                builder.Services
                    .ConfigureAspDotNetServices()
                    .ConfigureDependencyInjectionContainer();

                var app = builder.Build();

                app.ConfigureAspDotNetPipeline();

                if (OptionsAtStartup.NoStart)
                {
                    Log.Information("Qutting because 'no-start' option is enabled");
                    return;
                }

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Serilog.Log.CloseAndFlush();
            }
        }

        private static IServiceCollection ConfigureDependencyInjectionContainer(this IServiceCollection services)
        {
            // add the instance of OptionsAtStartup to DI as they were at startup. use when Options might change, but
            // the values at startup are to be used (generally anything marked RequiresRestart).
            services.AddSingleton(OptionsAtStartup);

            // add IOptionsMonitor and IOptionsSnapshot to DI.
            // use when the current Options are to be used (generally anything not marked RequiresRestart)
            // the monitor should be used for services with Singleton lifetime, snapshots for everything else
            services.AddOptions<Options>()
                .Bind(Configuration.GetSection(AppName), o => { o.BindNonPublicProperties = true; })
                .Validate(options =>
                {
                    if (!options.TryValidate(out var result))
                    {
                        Log.Warning("Options (re)configuration rejected.");
                        Log.Warning(result.GetResultView());
                        return false;
                    }

                    return true;
                });

            // add IManagedState, IStateMutator, IStateMonitor, and IStateSnapshot state to DI.
            // the mutator should be used any time application state needs to be mutated (as the name implies)
            // as with options, the monitor should be used for services with Singleton lifetime, snapshots for everything else
            // IManagedState should be used where state is being mutated and accessed in the same context
            services.AddManagedState<State>();

            // add IHttpClientFactory
            // use through 'using var http = HttpClientFactory.CreateClient()' wherever HTTP calls will be made
            // this is important to prevent memory leaks
            services.AddHttpClient();

            // add a partially configured instance of SoulseekClient. the Application instance will
            // complete configuration at startup.
            services.AddSingleton<ISoulseekClient, SoulseekClient>(_ =>
                new SoulseekClient(options: new SoulseekClientOptions(
                    maximumConcurrentUploads: OptionsAtStartup.Global.Upload.Slots,
                    maximumConcurrentDownloads: OptionsAtStartup.Global.Download.Slots,
                    minimumDiagnosticLevel: OptionsAtStartup.Soulseek.DiagnosticLevel)));

            // add the core application service to DI as well as a hosted service so that other services can
            // access instance methods
            services.AddSingleton<IApplication, Application>();
            services.AddHostedService(p => p.GetRequiredService<IApplication>());

            services.AddDbContext<SearchDbContext>("search.db");

            services.AddSingleton<ITransferTracker, TransferTracker>();
            services.AddSingleton<IBrowseTracker, BrowseTracker>();
            services.AddSingleton<IConversationTracker, ConversationTracker>();
            services.AddSingleton<IRoomTracker, RoomTracker>(_ => new RoomTracker(messageLimit: 250));

            services.AddSingleton<ISharedFileCache, SharedFileCache>();

            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IRoomService, RoomService>();
            services.AddSingleton<ITransferService, TransferService>();

            services.AddSingleton<IFTPClientFactory, FTPClientFactory>();
            services.AddSingleton<IFTPService, FTPService>();

            services.AddSingleton<IPushbulletService, PushbulletService>();

            return services;
        }

        private static IServiceCollection ConfigureAspDotNetServices(this IServiceCollection services)
        {
            services.AddCors(options => options.AddPolicy("AllowAll", builder => builder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed((host) => true)
                .AllowCredentials()));

            var jwtSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OptionsAtStartup.Web.Authentication.Jwt.Key));

            services.AddSingleton(jwtSigningKey);

            if (!OptionsAtStartup.Web.Authentication.Disabled)
            {
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ClockSkew = TimeSpan.FromMinutes(5),
                            RequireSignedTokens = true,
                            RequireExpirationTime = true,
                            ValidateLifetime = true,
                            ValidIssuer = AppName,
                            ValidateIssuer = true,
                            ValidateAudience = false,
                            IssuerSigningKey = jwtSigningKey,
                            ValidateIssuerSigningKey = true,
                        };

                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // assign the request token from the access_token query parameter
                                // but only if the destination is a SignalR hub
                                // https://docs.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-5.0
                                if (context.HttpContext.Request.Path.StartsWithSegments("/hub"))
                                {
                                    context.Token = context.Request.Query["access_token"];
                                }

                                return Task.CompletedTask;
                            },
                        };
                    });
            }
            else
            {
                Log.Warning("Authentication of web requests is DISABLED");

                services.AddAuthentication(PassthroughAuthentication.AuthenticationScheme)
                    .AddScheme<PassthroughAuthenticationOptions, PassthroughAuthenticationHandler>(PassthroughAuthentication.AuthenticationScheme, options =>
                    {
                        options.Username = "Anonymous";
                        options.Role = Role.Administrator;
                    });
            }

            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new IPAddressConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

            services.AddSignalR().AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new IPAddressConverter());
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            services.AddHealthChecks();

            services.AddApiVersioning(options => options.ReportApiVersions = true);
            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            if (OptionsAtStartup.Feature.Swagger)
            {
                services.AddSwaggerGen(options =>
                {
                    options.DescribeAllParametersInCamelCase();
                    options.SwaggerDoc(
                        "v0",
                        new OpenApiInfo
                        {
                            Title = AppName,
                            Version = "v0",
                        });

                    if (System.IO.File.Exists(XmlDocumentationFile))
                    {
                        options.IncludeXmlComments(XmlDocumentationFile);
                    }
                    else
                    {
                        Log.Warning($"Unable to find XML documentation in {XmlDocumentationFile}, Swagger will not include metadata");
                    }
                });
            }

            if (OptionsAtStartup.Feature.Prometheus)
            {
                services.AddSystemMetrics();
            }

            return services;
        }

        private static WebApplication ConfigureAspDotNetPipeline(this WebApplication app)
        {
            app.UseExceptionHandler(a => a.Run(async context =>
            {
                await context.Response.WriteAsJsonAsync(context.Features.Get<IExceptionHandlerPathFeature>().Error.Message);
            }));

            app.UseCors("AllowAll");

            if (OptionsAtStartup.Web.Https.Force)
            {
                app.UseHttpsRedirection();
                app.UseHsts();

                Log.Information($"Forcing HTTP requests to HTTPS");
            }

            // allow users to specify a custom path base, for use behind a reverse proxy
            var urlBase = OptionsAtStartup.Web.UrlBase;
            urlBase = urlBase.StartsWith("/") ? urlBase : "/" + urlBase;

            // 404 if a custom urlBase is set, but the request doesn't include it.
            // this is necessary to make sure the development-time and run-time behavior
            // are consistent.
            app.EnforceUrlBase(urlBase);

            // use urlBase. this effectively just removes urlBase from the path, which is
            // why the enforcement behavior exists above (the actually requested path becomes ambiguous)
            app.UsePathBase(urlBase);
            Log.Information("Using base url {UrlBase}", urlBase);

            // serve static content from the configured path
            FileServerOptions fileServerOptions = default;
            var contentPath = Path.GetFullPath(OptionsAtStartup.Web.ContentPath);

            if (TryPatchIndexWithUrlBase(contentPath, urlBase, out var index))
            {
                Log.Information("Patched static index file {Index} with base url {UrlBase}", index, urlBase);
            }
            else
            {
                Log.Warning("Failed to patch static content with custom base url {UrlBase}; the UI may not work properly.");
            }

            fileServerOptions = new FileServerOptions
            {
                FileProvider = new PhysicalFileProvider(contentPath),
                RequestPath = string.Empty,
                EnableDirectoryBrowsing = false,
                EnableDefaultFiles = true,
            };

            app.UseFileServer(fileServerOptions);
            Log.Information("Serving static content from {ContentPath}", contentPath);

            if (OptionsAtStartup.Feature.Prometheus)
            {
                app.UseHttpMetrics();
                Log.Information("Publishing Prometheus metrics to /metrics");
            }

            if (OptionsAtStartup.Web.Logging)
            {
                app.UseSerilogRequestLogging();
            }

            app.UseAuthentication();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ApplicationHub>("/hub/application");
                endpoints.MapHub<LogsHub>("/hub/logs");
                endpoints.MapHub<SearchHub>("/hub/search");

                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");

                if (OptionsAtStartup.Feature.Prometheus)
                {
                    endpoints.MapMetrics("/metrics");
                }
            });

            // if this is an /api route and no API controller was matched, give up and return a 404.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                await next();
            });

            if (OptionsAtStartup.Feature.Swagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(options => app.Services.GetRequiredService<IApiVersionDescriptionProvider>().ApiVersionDescriptions.ToList()
                    .ForEach(description => options.SwaggerEndpoint($"{(urlBase == "/" ? string.Empty : urlBase)}/swagger/{description.GroupName}/swagger.json", description.GroupName)));

                Log.Information("Publishing Swagger documentation to /swagger");
            }

            // if we made it this far, the caller is either looking for a route that was synthesized with a SPA router, or is genuinely confused.
            // optimistically search the dictionary of static files to see if we have a file that matches, and serve it if so.
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.ToString();

                // if there is no extension, the caller is looking for a directory (an index file)
                // modify the request to get the canonical default index
                if (Path.GetExtension(path) == string.Empty)
                {
                    context.Request.Path = "/";
                }
                else
                {
                    HashSet<string> staticFiles;

                    if (!Cache.TryGetValue(CacheKey.StaticFiles, out staticFiles))
                    {
                        staticFiles = System.IO.Directory.GetFiles(contentPath, "*", SearchOption.AllDirectories)
                            .Select(file => file.Replace(contentPath, string.Empty).TrimStart('/', '\\').Replace('\\', '/'))
                            .ToHashSet();

                        Cache.Set(CacheKey.StaticFiles, staticFiles);
                    }

                    var pathParts = path.Split('/');

                    for (int i = 1; i <= pathParts.Length; i++)
                    {
                        path = string.Join('/', pathParts.Skip(i));

                        if (staticFiles.Contains(path, StringComparer.InvariantCultureIgnoreCase))
                        {
                            // the request is for a static file we have, but the route contains extra segments
                            // correct the route and continue to the next middleware, which should serve the file from
                            // disk.
                            context.Request.Path = '/' + path;
                            break;
                        }
                    }
                }

                await next();
            });

            // the request path may have been rewritten above, in which case we're likely to have better luck finding
            // a match with the file server middleware.  let it try, and if it fails let it return a 404.
            app.UseFileServer(fileServerOptions);

            return app;
        }

        private static void ConfigureGlobalLogger()
        {
            Serilog.Log.Logger = (OptionsAtStartup.Debug ? new LoggerConfiguration().MinimumLevel.Debug() : new LoggerConfiguration().MinimumLevel.Information())
                .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                .MinimumLevel.Override("System.Net.Http.HttpClient", OptionsAtStartup.Debug ? LogEventLevel.Warning : LogEventLevel.Fatal)
                .MinimumLevel.Override("slskd.Authentication.PassthroughAuthenticationHandler", LogEventLevel.Warning)
                .Enrich.WithProperty("InstanceName", OptionsAtStartup.InstanceName)
                .Enrich.WithProperty("InvocationId", InvocationId)
                .Enrich.WithProperty("ProcessId", ProcessId)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: (OptionsAtStartup.Debug ? "[{SubContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Async(config =>
                    config.File(
                        Path.Combine(AppDirectory, "logs", $"{AppName}-.log"),
                        outputTemplate: (OptionsAtStartup.Debug ? "[{SubContext}] " : string.Empty) + "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day))
                .WriteTo.Conditional(
                    e => !string.IsNullOrEmpty(OptionsAtStartup.Logger.Loki),
                    config => config.GrafanaLoki(
                        OptionsAtStartup.Logger.Loki ?? string.Empty,
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .WriteTo.Sink(new DelegatingSink(logEvent =>
                {
                    try
                    {
                        var record = new LogRecord()
                        {
                            Timestamp = logEvent.Timestamp.LocalDateTime,
                            Context = logEvent.Properties["SourceContext"].ToString().TrimStart('"').TrimEnd('"'),
                            SubContext = logEvent.Properties.ContainsKey("SubContext") ? logEvent.Properties["SubContext"].ToString().TrimStart('"').TrimEnd('"') : null,
                            Level = logEvent.Level.ToString(),
                            Message = logEvent.RenderMessage().TrimStart('"').TrimEnd('"'),
                        };

                        LogBuffer.Enqueue(record);
                        LogEmitted?.Invoke(null, record);
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"Misconfigured delegating logger: {ex.Message}");
                    }
                }))
                .CreateLogger();
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

        private static IServiceCollection AddDbContext<T>(this IServiceCollection services, string filename)
            where T : DbContext
        {
            Log.Debug($"Initializing database context {typeof(T).Name}");

            try
            {
                services.AddDbContextFactory<SearchDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={Path.Combine(AppDirectory, "data", filename)}");
                });

                using var ctx = services
                    .BuildServiceProvider()
                    .GetRequiredService<IDbContextFactory<T>>()
                    .CreateDbContext();

                return services;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to initialize database context {typeof(T).Name}: ${ex.Message}");
                throw;
            }
        }

        private static void RecreateConfigurationFileIfMissing(string configurationFile)
        {
            if (!IOFile.Exists(configurationFile))
            {
                try
                {
                    Log.Warning("Configuration file {ConfigurationFile} does not exist; creating from example", configurationFile);
                    var source = Path.Combine(AppContext.BaseDirectory, "config", $"{AppName}.example.yml");
                    var destination = configurationFile;
                    IOFile.Copy(source, destination);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to create configuration file {ConfigurationFile}: {Message}", configurationFile, ex.Message);
                }
            }
        }

        private static bool TryPatchIndexWithUrlBase(string contentPath, string urlBase, out string index)
        {
            index = null;

            bool exists(string file) => System.IO.File.Exists(Path.Combine(contentPath, file));

            if (exists("index.html"))
            {
                index = "index.html";
            }
            else if (exists("index.htm"))
            {
                index = "index.htm";
            }
            else if (exists("Default.htm"))
            {
                index = "Default.htm";
            }
            else
            {
                Log.Warning("Failed to locate an index file in content path {ContentPath}; expected one of: {Defaults}", contentPath, "index.html, index.htm, Default.htm");
                return false;
            }

            var indexFQN = Path.Combine(contentPath, index);
            var pattern = "<script id=[\"|']urlBase[\"|']>(.*?)<\\/script>";

            try
            {
                var contents = System.IO.File.ReadAllText(indexFQN)
                    .Replace("\n", string.Empty)
                    .Replace("\r", string.Empty);

                var patch = $"<script id=\"urlBase\">window.urlBase=\"{urlBase}\"</script>";

                if (!Regex.IsMatch(contents, pattern, RegexOptions.Multiline))
                {
                    Log.Debug("Index does not contain a urlBase script tag; appending {Patch} to the end of the file", patch);
                    contents += patch;
                }
                else
                {
                    var code = $"window.urlBase=\"{urlBase}\"";
                    Log.Debug("Index contains a urlBase script tag; replacing tag contents with {Patch}", patch);
                    contents = Regex.Replace(contents, pattern, patch);
                }

                System.IO.File.WriteAllText(indexFQN, contents);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to patch index file {File}: {Message}", index, ex.Message);
                return false;
            }

            return true;
        }

        private static void GenerateX509Certificate(string password, string filename)
        {
            Log.Information("Generating X509 certificate...");
            filename = Path.Combine(AppContext.BaseDirectory, filename);

            var cert = X509.Generate(subject: AppName, password, X509KeyStorageFlags.Exportable);
            IOFile.WriteAllBytes(filename, cert.Export(X509ContentType.Pkcs12, password));

            Log.Information($"Password: {password}");
            Log.Information($"Certificate exported to {filename}");
        }

        private static void PrintCommandLineArguments(Type targetType)
        {
            static string GetLongName(string longName, Type type)
                => type == typeof(bool) ? longName : $"{longName} <{type.ToColloquialString().ToLowerInvariant()}>";

            var lines = new List<(string Item, string Description)>();

            void Map(Type type)
            {
                try
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
                catch
                {
                    return;
                }
            }

            Map(targetType);

            var longestItem = lines.Max(l => l.Item.Length);

            Log.Information("\nusage: slskd [arguments]\n");
            Log.Information("arguments:\n");

            foreach (var line in lines)
            {
                Log.Information($"  {line.Item.PadRight(longestItem)}   {line.Description}");
            }
        }

        private static void PrintEnvironmentVariables(Type targetType, string prefix)
        {
            static string GetName(string name, Type type) => $"{name} <{type.ToColloquialString().ToLowerInvariant()}>";

            var lines = new List<(string Item, string Description)>();

            void Map(Type type)
            {
                try
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
                catch
                {
                    return;
                }
            }

            Map(targetType);

            var longestItem = lines.Max(l => l.Item.Length);

            Log.Information("\nenvironment variables (arguments and config file have precedence):\n");

            foreach (var line in lines)
            {
                Log.Information($"  {line.Item.PadRight(longestItem)}   {line.Description}");
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
                   ▄▄▄▄         ▄▄▄▄       ▄▄▄▄
           ▄▄▄▄▄▄▄ █  █ ▄▄▄▄▄▄▄ █  █▄▄▄ ▄▄▄█  █
           █__ --█ █  █ █__ --█ █    ◄█ █  -  █
           █▄▄▄▄▄█ █▄▄█ █▄▄▄▄▄█ █▄▄█▄▄█ █▄▄▄▄▄█",
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

            if (IsDevelopment)
            {
                banner += "\n│■■■■■■■■■■■■■■■■■■■■► DEVELOPMENT ◄■■■■■■■■■■■■■■■■■■■■■│";
            }

            if (IsCanary)
            {
                banner += "\n│■■■■■■■■■■■■■■■■■■■■■■■► CANARY ◄■■■■■■■■■■■■■■■■■■■■■■■│";
            }

            banner += "\n└────────────────────────────────────────────────────────┘";

            Console.WriteLine(banner);
        }

        private static void VerifyDirectory(string directory, bool createIfMissing = true, bool verifyWriteable = true)
        {
            if (!System.IO.Directory.Exists(directory))
            {
                if (createIfMissing)
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(directory);
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