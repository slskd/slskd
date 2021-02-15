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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi.Models;
    using Prometheus;
    using Prometheus.SystemMetrics;
    using Serilog;
    using Serilog.Events;
    using slskd.API.Authentication;
    using slskd.Entities;
    using slskd.Security;
    using slskd.Trackers;
    using Soulseek;
    using Soulseek.Diagnostics;

    /// <summary>
    ///     ASP.NET Startup.
    /// </summary>
    public class Startup
    {
        private static readonly int MaxReconnectAttempts = 3;
        private static int currentReconnectAttempts = 0;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            Options = new Options();
            Configuration.GetSection(Program.AppName).Bind(Options, (o) =>
            {
                o.BindNonPublicProperties = true;
            });

            SharedFileCache = new SharedFileCache(Options.Directories.Shared, 3600000);

            UrlBase = Options.Web.UrlBase;
            UrlBase = UrlBase.StartsWith("/") ? UrlBase : "/" + UrlBase;

            ContentPath = Path.GetFullPath(Options.Web.ContentPath);

            JwtSigningKey = new SymmetricSecurityKey(Pbkdf2.GetKey(Options.Web.Authentication.Jwt.Key));
        }

        private SoulseekClient Client { get; set; }
        private IConfiguration Configuration { get; }
        private string ContentPath { get; set; }
        private SymmetricSecurityKey JwtSigningKey { get; set; }
        private ConcurrentDictionary<string, ILogger> Loggers { get; } = new ConcurrentDictionary<string, ILogger>();
        private Options Options { get; }
        private ISharedFileCache SharedFileCache { get; set; }
        private string UrlBase { get; set; }

        /// <summary>
        ///     Configure services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            var logger = Log.ForContext<Startup>();

            services.AddOptions<Options>()
                .Bind(Configuration.GetSection(Program.AppName), o => { o.BindNonPublicProperties = true; });

            services.AddCors(options => options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            services.AddSingleton(JwtSigningKey);

            if (!Options.Web.Authentication.Disable)
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

            services.AddHealthChecks();

            services.AddApiVersioning(options => options.ReportApiVersions = true);
            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            if (Options.Feature.Swagger)
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

                    if (System.IO.File.Exists(Program.DefaultXmlDocumentaitonFile))
                    {
                        options.IncludeXmlComments(Program.DefaultXmlDocumentaitonFile);
                    }
                    else
                    {
                        logger.Warning($"Unable to find XML documentation in {Program.DefaultXmlDocumentaitonFile}, Swagger will not include metadata");
                    }
                });
            }

            if (Options.Feature.Prometheus)
            {
                services.AddSystemMetrics();
            }

            services.AddSingleton<ISoulseekClient, SoulseekClient>(serviceProvider => Client);
            services.AddSingleton<ITransferTracker, TransferTracker>();
            services.AddSingleton<ISearchTracker, SearchTracker>();
            services.AddSingleton<IBrowseTracker, BrowseTracker>();
            services.AddSingleton<IConversationTracker, ConversationTracker>();
            services.AddSingleton<IRoomTracker, RoomTracker>(_ => new RoomTracker(messageLimit: 250));
        }

        /// <summary>
        ///     Configure middleware.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="provider">The api version description provider.</param>
        /// <param name="tracker">The transfer tracker.</param>
        /// <param name="browseTracker">The browse tracker.</param>
        /// <param name="conversationTracker">The conversation tracker.</param>
        /// <param name="roomTracker">The room tracker.</param>
        public void Configure(
            IApplicationBuilder app,
            IApiVersionDescriptionProvider provider,
            ITransferTracker tracker,
            IBrowseTracker browseTracker,
            IConversationTracker conversationTracker,
            IRoomTracker roomTracker)
        {
            var logger = Log.ForContext<Startup>();

            app.UseCors("AllowAll");

            if (Options.Web.Https.Force)
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

            app.UseSerilogRequestLogging();

            if (Options.Feature.Prometheus)
            {
                app.UseHttpMetrics();
                logger.Information("Publishing Prometheus metrics to /metrics");
            }

            app.UseAuthentication();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");

                if (Options.Feature.Prometheus)
                {
                    endpoints.MapMetrics();
                }
            });

            if (Options.Feature.Swagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(options => provider.ApiVersionDescriptions.ToList()
                    .ForEach(description => options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName)));

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

            // ---------------------------------------------------------------------------------------------------------------------------------------------
            // begin SoulseekClient implementation
            // ---------------------------------------------------------------------------------------------------------------------------------------------
            var connectionOptions = new ConnectionOptions(
                readBufferSize: Options.Soulseek.Connection.Buffer.Read,
                writeBufferSize: Options.Soulseek.Connection.Buffer.Write,
                connectTimeout: Options.Soulseek.Connection.Timeout.Connect,
                inactivityTimeout: Options.Soulseek.Connection.Timeout.Inactivity);

            var defaults = new SoulseekClientOptions();

            // create options for the client. see the implementation of Func<> and Action<> options for detailed info.
            var clientOptions = new SoulseekClientOptions(
                listenPort: Options.Soulseek.ListenPort,
                enableListener: true,
                userEndPointCache: new UserEndPointCache(),
                distributedChildLimit: Options.Soulseek.DistributedNetwork.ChildLimit,
                enableDistributedNetwork: !Options.Soulseek.DistributedNetwork.Disabled,
                minimumDiagnosticLevel: Options.Soulseek.DiagnosticLevel,
                autoAcknowledgePrivateMessages: false,
                acceptPrivateRoomInvitations: true,
                serverConnectionOptions: connectionOptions,
                peerConnectionOptions: connectionOptions,
                transferConnectionOptions: connectionOptions,
                userInfoResponseResolver: UserInfoResponseResolver,
                browseResponseResolver: BrowseResponseResolver,
                directoryContentsResponseResolver: DirectoryContentsResponseResolver,
                enqueueDownloadAction: (username, endpoint, filename) => EnqueueDownloadAction(username, endpoint, filename, tracker),
                searchResponseResolver: SearchResponseResolver);

            var username = Options.Soulseek.Username;
            var password = Options.Soulseek.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Soulseek credentials are not configured");
            }

            Client = new SoulseekClient(options: clientOptions);

            // bind the DiagnosticGenerated event so we can trap and display diagnostic messages. this is optional, and if the
            // event isn't bound the minimumDiagnosticLevel should be set to None.
            Client.DiagnosticGenerated += (e, args) =>
            {
                static LogEventLevel TranslateLogLevel(DiagnosticLevel diagnosticLevel) => diagnosticLevel switch
                {
                    DiagnosticLevel.Debug => LogEventLevel.Debug,
                    DiagnosticLevel.Info => LogEventLevel.Information,
                    DiagnosticLevel.Warning => LogEventLevel.Warning,
                    DiagnosticLevel.None => default,
                    _ => default
                };

                var logger = Loggers.GetOrAdd(e.GetType().FullName, Log.ForContext("SourceContext", "Soulseek").ForContext("SoulseekContext", e.GetType().FullName));

                logger.Write(TranslateLogLevel(args.Level), "{@Message}", args.Message);
            };

            // bind transfer events. see TransferStateChangedEventArgs and TransferProgressEventArgs.
            Client.TransferStateChanged += (e, args) =>
            {
                var direction = args.Transfer.Direction.ToString().ToUpper();
                var user = args.Transfer.Username;
                var file = Path.GetFileName(args.Transfer.Filename);
                var oldState = args.PreviousState;
                var state = args.Transfer.State;

                var completed = args.Transfer.State.HasFlag(TransferStates.Completed);

                Console.WriteLine($"[{direction}] [{user}/{file}] {oldState} => {state}{(completed ? $" ({args.Transfer.BytesTransferred}/{args.Transfer.Size} = {args.Transfer.PercentComplete}%) @ {args.Transfer.AverageSpeed.SizeSuffix()}/s" : string.Empty)}");
            };

            Client.TransferProgressUpdated += (e, args) =>
            {
                // this is really verbose. Console.WriteLine($"[{args.Transfer.Direction.ToString().ToUpper()}]
                // [{args.Transfer.Username}/{Path.GetFileName(args.Transfer.Filename)}]
                // {args.Transfer.BytesTransferred}/{args.Transfer.Size} {args.Transfer.PercentComplete}% {args.Transfer.AverageSpeed}kb/s");
            };

            // bind BrowseProgressUpdated to track progress of browse response payload transfers. these can take a while depending
            // on number of files shared.
            Client.BrowseProgressUpdated += (e, args) =>
            {
                browseTracker.AddOrUpdate(args.Username, args);
            };

            // bind UserStatusChanged to monitor the status of users added via AddUserAsync().
            Client.UserStatusChanged += (e, args) =>
            {
                // Console.WriteLine($"[USER] {args.Username}: {args.Status}");
            };

            Client.PrivateMessageReceived += (e, args) =>
            {
                conversationTracker.AddOrUpdate(args.Username, PrivateMessage.FromEventArgs(args));
            };

            Client.PrivateRoomMembershipAdded += (e, room) => Console.WriteLine($"Added to private room {room}");
            Client.PrivateRoomMembershipRemoved += (e, room) => Console.WriteLine($"Removed from private room {room}");
            Client.PrivateRoomModerationAdded += (e, room) => Console.WriteLine($"Promoted to moderator in private room {room}");
            Client.PrivateRoomModerationRemoved += (e, room) => Console.WriteLine($"Demoted from moderator in private room {room}");

            Client.PublicChatMessageReceived += (e, args) =>
            {
                Console.WriteLine($"[PUBLIC CHAT] [{args.RoomName}] [{args.Username}]: {args.Message}");
            };

            Client.RoomMessageReceived += (e, args) =>
            {
                var message = RoomMessage.FromEventArgs(args, DateTime.UtcNow);
                roomTracker.AddOrUpdateMessage(args.RoomName, message);
            };

            Client.RoomJoined += (e, args) =>
            {
                if (args.Username != username) // this will fire when we join a room; track that through the join operation.
                {
                    roomTracker.TryAddUser(args.RoomName, args.UserData);
                }
            };

            Client.RoomLeft += (e, args) =>
            {
                roomTracker.TryRemoveUser(args.RoomName, args.Username);
            };

            Client.Disconnected += async (e, args) =>
            {
                Console.WriteLine($"Disconnected from Soulseek server: {args.Message}");

                // don't reconnect if the disconnecting Exception is either of these types. if KickedFromServerException, another
                // client was most likely signed in, and retrying will cause a connect loop. if ObjectDisposedException, the
                // client is shutting down.
                if (!(args.Exception is KickedFromServerException || args.Exception is ObjectDisposedException))
                {
                    Interlocked.Increment(ref currentReconnectAttempts);

                    if (currentReconnectAttempts <= MaxReconnectAttempts)
                    {
                        var wait = currentReconnectAttempts ^ 3;
                        Console.WriteLine($"Waiting {wait} second(s) before reconnect...");
                        await Task.Delay(wait);

                        Console.WriteLine($"Attepting to reconnect...");
                        await Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);
                    }
                    else
                    {
                        Console.WriteLine($"Unable to reconnect after {currentReconnectAttempts} tries.");
                    }
                }
            };

            Task.Run(async () =>
            {
                await Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);
            }).GetAwaiter().GetResult();

            logger.Information("Connected and logged in as {Username}", username);
        }

        /// <summary>
        ///     Creates and returns an <see cref="IEnumerable{T}"/> of <see cref="Soulseek.Directory"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving an IEnumerable of Soulseek.Directory.</returns>
        private Task<BrowseResponse> BrowseResponseResolver(string username, IPEndPoint endpoint)
        {
            var directories = System.IO.Directory
                .GetDirectories(Options.Directories.Shared, "*", SearchOption.AllDirectories)
                .Select(dir => new Soulseek.Directory(dir, System.IO.Directory.GetFiles(dir)
                    .Select(f => new Soulseek.File(1, Path.GetFileName(f), new FileInfo(f).Length, Path.GetExtension(f)))));

            return Task.FromResult(new BrowseResponse(directories));
        }

        /// <summary>
        ///     Creates and returns a <see cref="Soulseek.Directory"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <param name="token">The unique token for the request, supplied by the requesting user.</param>
        /// <param name="directory">The requested directory.</param>
        /// <returns>A Task resolving an instance of Soulseek.Directory containing the contents of the requested directory.</returns>
        private Task<Soulseek.Directory> DirectoryContentsResponseResolver(string username, IPEndPoint endpoint, int token, string directory)
        {
            var result = new Soulseek.Directory(directory, System.IO.Directory.GetFiles(directory)
                    .Select(f => new Soulseek.File(1, Path.GetFileName(f), new FileInfo(f).Length, Path.GetExtension(f))));

            return Task.FromResult(result);
        }

        /// <summary>
        ///     Invoked upon a remote request to download a file.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <param name="filename">The filename of the requested file.</param>
        /// <param name="tracker">(for example purposes) the ITransferTracker used to track progress.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        /// <exception cref="DownloadEnqueueException">
        ///     Thrown when the download is rejected. The Exception message will be passed to the remote user.
        /// </exception>
        /// <exception cref="Exception">
        ///     Thrown on any other Exception other than a rejection. A generic message will be passed to the remote user for
        ///     security reasons.
        /// </exception>
        private Task EnqueueDownloadAction(string username, IPEndPoint endpoint, string filename, ITransferTracker tracker)
        {
            _ = endpoint;
            filename = filename.ToLocalOSPath();
            var fileInfo = new FileInfo(filename);

            if (!fileInfo.Exists)
            {
                Console.WriteLine($"[UPLOAD REJECTED] File {filename} not found.");
                throw new DownloadEnqueueException($"File not found.");
            }

            if (tracker.TryGet(TransferDirection.Upload, username, filename, out _))
            {
                // in this case, a re-requested file is a no-op. normally we'd want to respond with a PlaceInQueueResponse
                Console.WriteLine($"[UPLOAD RE-REQUESTED] [{username}/{filename}]");
                return Task.CompletedTask;
            }

            // create a new cancellation token source so that we can cancel the upload from the UI.
            var cts = new CancellationTokenSource();
            var topts = new TransferOptions(stateChanged: (e) => tracker.AddOrUpdate(e, cts), progressUpdated: (e) => tracker.AddOrUpdate(e, cts), governor: (t, c) => Task.Delay(1, c));

            // accept all download requests, and begin the upload immediately. normally there would be an internal queue, and
            // uploads would be handled separately.
            Task.Run(async () =>
            {
                using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                await Client.UploadAsync(username, fileInfo.FullName, fileInfo.Length, stream, options: topts, cancellationToken: cts.Token);
            }).ContinueWith(t =>
            {
                Console.WriteLine($"[UPLOAD FAILED] {t.Exception}");
            }, TaskContinuationOptions.NotOnRanToCompletion); // fire and forget

            // return a completed task so that the invoking code can respond to the remote client.
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Creates and returns a <see cref="SearchResponse"/> in response to the given <paramref name="query"/>.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="token">The search token.</param>
        /// <param name="query">The search query.</param>
        /// <returns>A Task resolving a SearchResponse, or null.</returns>
        private Task<SearchResponse> SearchResponseResolver(string username, int token, SearchQuery query)
        {
            var defaultResponse = Task.FromResult<SearchResponse>(null);

            // some bots continually query for very common strings. blacklist known names here.
            var blacklist = new[] { "Lola45", "Lolo51", "rajah" };
            if (blacklist.Contains(username))
            {
                return defaultResponse;
            }

            // some bots and perhaps users search for very short terms. only respond to queries >= 3 characters. sorry, U2 fans.
            if (query.Query.Length < 3)
            {
                return defaultResponse;
            }

            var results = SharedFileCache.Search(query);

            if (results.Any())
            {
                Console.WriteLine($"[SENDING SEARCH RESULTS]: {results.Count()} records to {username} for query {query.SearchText}");

                return Task.FromResult(new SearchResponse(
                    username,
                    token,
                    freeUploadSlots: 1,
                    uploadSpeed: 0,
                    queueLength: 0,
                    fileList: results));
            }

            // if no results, either return null or an instance of SearchResponse with a fileList of length 0 in either case, no
            // response will be sent to the requestor.
            return Task.FromResult<SearchResponse>(null);
        }

        /// <summary>
        ///     Creates and returns a <see cref="UserInfo"/> object in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving the UserInfo instance.</returns>
        private Task<UserInfo> UserInfoResponseResolver(string username, IPEndPoint endpoint)
        {
            var info = new UserInfo(
                description: $"Soulseek.NET Web Example! also, your username is {username}, and IP endpoint is {endpoint}",
                picture: System.IO.File.ReadAllBytes(@"slsk_bird.jpg"),
                uploadSlots: 1,
                queueLength: 0,
                hasFreeUploadSlot: false);

            return Task.FromResult(info);
        }

        private class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override bool CanConvert(Type objectType) => objectType == typeof(IPAddress);

            public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => IPAddress.Parse(reader.GetString());

            public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
        }

        private class UserEndPointCache : IUserEndPointCache
        {
            public UserEndPointCache()
            {
                Cache = new MemoryCache(new MemoryCacheOptions());
            }

            private IMemoryCache Cache { get; }

            public void AddOrUpdate(string username, IPEndPoint endPoint)
            {
                Cache.Set(username, endPoint, TimeSpan.FromSeconds(60));
            }

            public bool TryGet(string username, out IPEndPoint endPoint)
            {
                return Cache.TryGetValue(username, out endPoint);
            }
        }
    }
}