namespace slskd
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.OpenApi.Models;
    using Soulseek;
    using Soulseek.Diagnostics;
    using slskd.Trackers;
    using System.Collections.Concurrent;
    using Microsoft.IdentityModel.Tokens;
    using slskd.Security;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Prometheus.SystemMetrics;
    using Microsoft.Extensions.Options;
    using Serilog;
    using Microsoft.Extensions.Hosting;
    using Prometheus;
    using Microsoft.Extensions.FileProviders;
    using Serilog.Events;
    using slskd.Entities;

    public class Startup
    {
        internal static string OutputDirectory { get; set; }
        internal static string SharedDirectory { get; set; }
        internal static long SharedCacheTTL { get; set; }
        internal static DiagnosticLevel DiagnosticLevel { get; set; }
        internal static int RoomMessageLimit { get; set; }
        internal static string XmlDocFile { get; set; }

        private SoulseekClient Client { get; set; }
        private object ConsoleSyncRoot { get; } = new object();
        private ISharedFileCache SharedFileCache { get; set; }
        private string UrlBase { get; set; }
        private string ContentPath { get; set; }
        private SymmetricSecurityKey JwtSigningKey { get; set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            OutputDirectory = Configuration.GetValue<string>("OUTPUT_DIR");
            SharedDirectory = Configuration.GetValue<string>("SHARED_DIR");
            SharedCacheTTL = Configuration.GetValue<long>("SHARED_CACHE_TTL", 3600000); // 1 hour
            DiagnosticLevel = Configuration.GetValue<DiagnosticLevel>("DIAGNOSTIC", DiagnosticLevel.Info);
            RoomMessageLimit = Configuration.GetValue<int>("ROOM_MESSAGE_LIMIT", 250);
            XmlDocFile = Configuration.GetValue<string>("XML_DOC_FILE", Path.Combine(AppContext.BaseDirectory, typeof(Startup).GetTypeInfo().Assembly.GetName().Name + ".xml"));
            
            SharedFileCache = new SharedFileCache(SharedDirectory, SharedCacheTTL);
        }

        public IConfiguration Configuration { get; }
        public ConcurrentDictionary<string, ILogger> Loggers { get; } = new ConcurrentDictionary<string, ILogger>();

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions<Configuration.Program>()
                .Bind(Configuration.GetSection("soulseek"));

            services.AddOptions<Configuration.Authentication>()
                .Bind(Configuration.GetSection("authentication"));

            UrlBase = Program.Options.Web.UrlBase;
            UrlBase = UrlBase.StartsWith("/") ? UrlBase : "/" + UrlBase;
            
            ContentPath = Path.GetFullPath(Program.Options.Web.ContentPath);

            JwtSigningKey = new SymmetricSecurityKey(PBKDF2.GetKey(Program.Options.Web.Jwt.Key));

            services.AddCors(options => options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

            services.AddSingleton(JwtSigningKey);

            if (!Program.Options.NoAuth)
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
                            ValidIssuer = "slskd",
                            ValidateIssuer = true,
                            ValidateAudience = false,
                            IssuerSigningKey = JwtSigningKey,
                            ValidateIssuerSigningKey = true,
                        };
                    });
            }
            else
            {
                services.AddAuthentication(PassthroughAuthentication.AuthenticationScheme)
                    .AddScheme<PassthroughAuthenticationOptions, PassthroughAuthenticationHandler>(PassthroughAuthentication.AuthenticationScheme, options =>
                    {
                        options.Username = "n/a";
                    });
            }

            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new IPAddressConverter());
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.IgnoreNullValues = true;
            });

            services.AddApiVersioning(options => options.ReportApiVersions = true);
            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            services.AddSwaggerGen(options =>
            {
                options.DescribeAllParametersInCamelCase();
                options.SwaggerDoc("v0",
                    new OpenApiInfo
                    {
                        Title = "slskd",
                        Version = "v0"
                    }
                    );

                if (System.IO.File.Exists(XmlDocFile))
                {
                    options.IncludeXmlComments(XmlDocFile);
                }
            });

            if (Program.Options.Feature.Prometheus)
            {
                services.AddSystemMetrics();
            }

            services.AddSingleton<ISoulseekClient, SoulseekClient>(serviceProvider => Client);
            services.AddSingleton<ITransferTracker, TransferTracker>();
            services.AddSingleton<ISearchTracker, SearchTracker>();
            services.AddSingleton<IBrowseTracker, BrowseTracker>();
            services.AddSingleton<IConversationTracker, ConversationTracker>();
            services.AddSingleton<IRoomTracker, RoomTracker>(_ => new RoomTracker(messageLimit: RoomMessageLimit));
        }

        public void Configure(
            IApplicationBuilder app, 
            IWebHostEnvironment env,
            IApiVersionDescriptionProvider provider, 
            ITransferTracker tracker, 
            IBrowseTracker browseTracker, 
            IConversationTracker conversationTracker,
            IOptionsMonitor<Configuration.Program> soulseekOptions,
            IOptionsMonitor<Configuration.Authentication> authenticationOptions,
            IRoomTracker roomTracker)
        {
            var logger = Log.ForContext<Startup>();

            if (!env.IsDevelopment())
            {
                app.UseHsts();
            }

            Console.WriteLine(JsonSerializer.Serialize(authenticationOptions.CurrentValue));

            authenticationOptions.OnChange(o => Console.WriteLine(JsonSerializer.Serialize(o)));

            app.UseCors("AllowAll");

            app.UsePathBase(Program.Options.Web.UrlBase);
            logger.Information("Using base url {UrlBase}", Program.Options.Web.UrlBase);

            // remove any errant double forward slashes which may have been introduced
            // by a reverse proxy or having the base path removed
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
                    RequestPath = "",
                    EnableDirectoryBrowsing = false,
                    EnableDefaultFiles = true
                };

                app.UseFileServer(fileServerOptions);
                logger.Information("Serving static content from {ContentPath}", ContentPath);
            }

            app.UseSerilogRequestLogging();

            if (Program.Options.Feature.Prometheus)
            {
                app.UseHttpMetrics();
            }

            app.UseAuthentication();

            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                if (Program.Options.Feature.Prometheus)
                {
                    endpoints.MapMetrics();
                }
            });

            if (Program.Options.Feature.Swagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(options => provider.ApiVersionDescriptions.ToList()
                    .ForEach(description => options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName)));
            }

            // if we made it this far and the route still wasn't matched, return the index unless it's an api route
            // this is required so that SPA routing (React Router, etc) can work properly
            app.Use(async (context, next) =>
            {
                // exclude API routes which are not matched or return a 404
                if (!context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Request.Path = "/";
                }

                await next();
            });

            // finally, hit the fileserver again.  if the path was modified to return the index above, the index document will be returned
            // otherwise it will throw a final 404 back to the client.
            if (System.IO.Directory.Exists(ContentPath))
            {
                app.UseFileServer(fileServerOptions);
            }

            // ---------------------------------------------------------------------------------------------------------------------------------------------
            // begin SoulseekClient implementation
            // ---------------------------------------------------------------------------------------------------------------------------------------------

            var connectionOptions = new ConnectionOptions(
                readBufferSize: soulseekOptions.CurrentValue.Soulseek.Connection.Buffer.Read,
                writeBufferSize: soulseekOptions.CurrentValue.Soulseek.Connection.Buffer.Write,
                connectTimeout: soulseekOptions.CurrentValue.Soulseek.Connection.Timeout.Connect,
                inactivityTimeout: soulseekOptions.CurrentValue.Soulseek.Connection.Timeout.Inactivity);

            // create options for the client.
            // see the implementation of Func<> and Action<> options for detailed info.
            var clientOptions = new SoulseekClientOptions(
                listenPort: soulseekOptions.CurrentValue.Soulseek.ListenPort,
                userEndPointCache: new UserEndPointCache(),
                distributedChildLimit: soulseekOptions.CurrentValue.Soulseek.DistributedNetwork.ChildLimit,
                enableDistributedNetwork: soulseekOptions.CurrentValue.Soulseek.DistributedNetwork.Enabled,
                minimumDiagnosticLevel: DiagnosticLevel,
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

            var username = soulseekOptions.CurrentValue.Soulseek.Username;
            var password = soulseekOptions.CurrentValue.Soulseek.Password;

            Client = new SoulseekClient(options: clientOptions);

            // bind the DiagnosticGenerated event so we can trap and display diagnostic messages.  this is optional, and if the event 
            // isn't bound the minimumDiagnosticLevel should be set to None.
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

            // bind transfer events.  see TransferStateChangedEventArgs and TransferProgressEventArgs.
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
                // this is really verbose.
                // Console.WriteLine($"[{args.Transfer.Direction.ToString().ToUpper()}] [{args.Transfer.Username}/{Path.GetFileName(args.Transfer.Filename)}] {args.Transfer.BytesTransferred}/{args.Transfer.Size} {args.Transfer.PercentComplete}% {args.Transfer.AverageSpeed}kb/s");
            };

            // bind BrowseProgressUpdated to track progress of browse response payload transfers.  
            // these can take a while depending on number of files shared.
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
                logger.Warning("Disconnected from Soulseek server: {Message}", args.Message, args.Exception);

                // don't reconnect if the disconnecting Exception is either of these types.
                // if KickedFromServerException, another client was most likely signed in, and retrying will cause a connect loop.
                // if ObjectDisposedException, the client is shutting down.
                if (!(args.Exception is KickedFromServerException || args.Exception is ObjectDisposedException))
                {
                    logger.Warning("Attepting to reconnect...");
                    await Client.ConnectAsync(username, password);
                }
            };

            //Task.Run(async () =>
            //{
            //    await Client.ConnectAsync(Username, Password);
            //}).GetAwaiter().GetResult();

            logger.Information("Connected and logged in as {Username}", username);
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

        /// <summary>
        ///     Creates and returns an <see cref="IEnumerable{T}"/> of <see cref="Soulseek.Directory"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving an IEnumerable of Soulseek.Directory.</returns>
        private Task<BrowseResponse> BrowseResponseResolver(string username, IPEndPoint endpoint)
        {
            var directories = System.IO.Directory
                .GetDirectories(SharedDirectory, "*", SearchOption.AllDirectories)
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
        /// <exception cref="DownloadEnqueueException">Thrown when the download is rejected.  The Exception message will be passed to the remote user.</exception>
        /// <exception cref="Exception">Thrown on any other Exception other than a rejection.  A generic message will be passed to the remote user for security reasons.</exception>
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
                // in this case, a re-requested file is a no-op.  normally we'd want to respond with a 
                // PlaceInQueueResponse
                Console.WriteLine($"[UPLOAD RE-REQUESTED] [{username}/{filename}]");
                return Task.CompletedTask;
            }

            // create a new cancellation token source so that we can cancel the upload from the UI.
            var cts = new CancellationTokenSource();
            var topts = new TransferOptions(stateChanged: (e) => tracker.AddOrUpdate(e, cts), progressUpdated: (e) => tracker.AddOrUpdate(e, cts), governor: (t, c) => Task.Delay(1, c));

            // accept all download requests, and begin the upload immediately.
            // normally there would be an internal queue, and uploads would be handled separately.
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

            // some bots continually query for very common strings.  blacklist known names here.
            var blacklist = new[] { "Lola45", "Lolo51", "rajah" };
            if (blacklist.Contains(username))
            {
                return defaultResponse;
            }

            // some bots and perhaps users search for very short terms.  only respond to queries >= 3 characters.  sorry, U2 fans.
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

            // if no results, either return null or an instance of SearchResponse with a fileList of length 0
            // in either case, no response will be sent to the requestor.
            return Task.FromResult<SearchResponse>(null);
        }

        class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override bool CanConvert(Type objectType) => (objectType == typeof(IPAddress));

            public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => IPAddress.Parse(reader.GetString());

            public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
        }

        class UserEndPointCache : IUserEndPointCache
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