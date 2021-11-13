// <copyright file="Startup.cs" company="slskd Team">
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
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi.Models;
    using Prometheus;
    using Prometheus.SystemMetrics;
    using Serilog;
    using slskd.Authentication;
    using slskd.Core.API;
    using slskd.Integrations.FTP;
    using slskd.Integrations.Pushbullet;
    using slskd.Messaging;
    using slskd.Search;
    using slskd.Shares;
    using slskd.Transfers;
    using slskd.Users;
    using slskd.Validation;
    using Soulseek;

    /// <summary>
    ///     ASP.NET Startup.
    /// </summary>
    public class Startup
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration root.</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            OptionsAtStartup = new OptionsAtStartup();
            Configuration.GetSection(Program.AppName).Bind(OptionsAtStartup, (o) =>
            {
                o.BindNonPublicProperties = true;
            });

            UrlBase = OptionsAtStartup.Web.UrlBase;
            UrlBase = UrlBase.StartsWith("/") ? UrlBase : "/" + UrlBase;

            ContentPath = Path.GetFullPath(OptionsAtStartup.Web.ContentPath);

            JwtSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(OptionsAtStartup.Web.Authentication.Jwt.Key));
        }

        private IConfiguration Configuration { get; }
        private string ContentPath { get; set; }
        private SymmetricSecurityKey JwtSigningKey { get; set; }
        private OptionsAtStartup OptionsAtStartup { get; }
        private string UrlBase { get; set; }

        /// <summary>
        ///     Configure services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            var logger = Log.ForContext<Startup>();

            // add the instance of OptionsAtStartup to DI as they were at startup. use when Options might change, but
            // the values at startup are to be used.
            services.AddSingleton(OptionsAtStartup);

            // add IOptionsMonitor and IOptionsSnapshot to DI.  use when the current Options are to be used.
            services.AddOptions<Options>()
                .Bind(Configuration.GetSection(Program.AppName), o => { o.BindNonPublicProperties = true; })
                .Validate(options =>
                {
                    if (!options.TryValidate(out var result))
                    {
                        logger.Warning("Options (re)configuration rejected.");
                        logger.Warning(result.GetResultView());
                        return false;
                    }

                    return true;
                });

            services.AddManagedState<State>();

            services.AddCors(options => options.AddPolicy("AllowAll", builder => builder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed((host) => true)
                .AllowCredentials()));

            services.AddSingleton(JwtSigningKey);

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
                            ValidIssuer = Program.AppName,
                            ValidateIssuer = true,
                            ValidateAudience = false,
                            IssuerSigningKey = JwtSigningKey,
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
                logger.Warning("Authentication of web requests is DISABLED");

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
                options.JsonSerializerOptions.IgnoreNullValues = true;
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
                            Title = Program.AppName,
                            Version = "v0",
                        });

                    if (System.IO.File.Exists(Program.XmlDocumentationFile))
                    {
                        options.IncludeXmlComments(Program.XmlDocumentationFile);
                    }
                    else
                    {
                        logger.Warning($"Unable to find XML documentation in {Program.XmlDocumentationFile}, Swagger will not include metadata");
                    }
                });
            }

            if (OptionsAtStartup.Feature.Prometheus)
            {
                services.AddSystemMetrics();
            }

            services.AddDbContextFactory<SearchDbContext>(options =>
            {
                options.UseSqlite($"Data Source={Path.Combine(Program.AppDirectory, "data", "search.db")}");
            });

            services.AddDbContextFactory<UserDbContext>(options =>
            {
                options.UseSqlite($"Data Source={Path.Combine(Program.AppDirectory, "data", "users.db")}");
            });

            services.AddHttpClient();

            // add a partially configured instance of SoulseekClient. the Application instance will
            // complete configuration at startup.
            services.AddSingleton<ISoulseekClient, SoulseekClient>(_ =>
                new SoulseekClient(options: new SoulseekClientOptions(minimumDiagnosticLevel: OptionsAtStartup.Soulseek.DiagnosticLevel)));

            // add the core application service to DI as well as a hosted service so that other services can
            // access instance methods
            services.AddSingleton<IApplication, Application>();
            services.AddHostedService(p => p.GetRequiredService<IApplication>());

            services.AddSingleton<ITransferTracker, TransferTracker>();
            services.AddSingleton<IBrowseTracker, BrowseTracker>();
            services.AddSingleton<IConversationTracker, ConversationTracker>();
            services.AddSingleton<IRoomTracker, RoomTracker>(_ => new RoomTracker(messageLimit: 250));

            services.AddSingleton<ISharedFileCache, SharedFileCache>();

            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<IRoomService, RoomService>();

            services.AddSingleton<IFTPClientFactory, FTPClientFactory>();
            services.AddSingleton<IFTPService, FTPService>();

            services.AddSingleton<IPushbulletService, PushbulletService>();
        }

        /// <summary>
        ///     Configure middleware.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="apiVersionDescriptionProvider">The api version description provider.</param>
        public void Configure(
            IApplicationBuilder app,
            IApiVersionDescriptionProvider apiVersionDescriptionProvider)
        {
            var logger = Log.ForContext<Startup>();

            // initialize DbContext instances to avoid race conditions when a db needs to be
            // created from scratch. note that this method needs to be updated to include each
            // DbContext explicitly.
            InitializeDbContexts(app);

            app.UseCors("AllowAll");

            if (OptionsAtStartup.Web.Https.Force)
            {
                app.UseHttpsRedirection();
                app.UseHsts();

                logger.Information($"Forcing HTTP requests to HTTPS");
            }

            // allow users to specify a custom path base, for use behind a reverse proxy
            app.UsePathBase(UrlBase);
            logger.Information("Using base url {UrlBase}", UrlBase);

            // remove any errant double forward slashes which may have been introduced by manipulating the path base
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.ToString();

                if (path.StartsWith("//"))
                {
                    context.Request.Path = new string(path.Skip(1).ToArray());
                }

                await next();
            });

            FileServerOptions fileServerOptions = default;

            if (!System.IO.Directory.Exists(ContentPath))
            {
                logger.Warning($"Static content disabled; cannot find content path '{ContentPath}'");
            }
            else
            {
                fileServerOptions = new FileServerOptions
                {
                    FileProvider = new PhysicalFileProvider(ContentPath),
                    RequestPath = string.Empty,
                    EnableDirectoryBrowsing = false,
                    EnableDefaultFiles = true,
                };

                app.UseFileServer(fileServerOptions);
                logger.Information("Serving static content from {ContentPath}", ContentPath);
            }

            if (OptionsAtStartup.Feature.Prometheus)
            {
                app.UseHttpMetrics();
                logger.Information("Publishing Prometheus metrics to /metrics");
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

                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");

                if (OptionsAtStartup.Feature.Prometheus)
                {
                    endpoints.MapMetrics("/metrics");
                }
            });

            if (OptionsAtStartup.Feature.Swagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(options => apiVersionDescriptionProvider.ApiVersionDescriptions.ToList()
                    .ForEach(description => options.SwaggerEndpoint($"{(UrlBase == "/" ? string.Empty : UrlBase)}/swagger/{description.GroupName}/swagger.json", description.GroupName)));

                logger.Information("Publishing Swagger documentation to /swagger");
            }

            // if we made it this far and the route still wasn't matched, return the index unless it's an api route. this is
            // required so that SPA routing (React Router, etc) can work properly
            app.Use(async (context, next) =>
            {
                // exclude API routes which are not matched or return a 404
                if (!context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Request.Path = "/";
                }

                await next();
            });

            // finally, hit the fileserver again. if the path was modified to return the index above, the index document will be
            // returned. otherwise it will throw a final 404 back to the client.
            if (System.IO.Directory.Exists(ContentPath))
            {
                app.UseFileServer(fileServerOptions);
            }
        }

        private void InitializeDbContexts(IApplicationBuilder app)
        {
            var logger = Log.ForContext<Startup>();

            IDbContextFactory<T> GetFactory<T>()
                where T : DbContext
                => app.ApplicationServices.GetRequiredService<IDbContextFactory<T>>();

            logger.Debug("Initializing database contexts");

            try
            {
                using var search = GetFactory<SearchDbContext>().CreateDbContext();
                using var peer = GetFactory<UserDbContext>().CreateDbContext();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to initialize one or more application databases");
                throw;
            }
        }
    }
}