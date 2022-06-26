// <copyright file="Application.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using Serilog.Events;
    using slskd.Configuration;
    using slskd.Core.API;
    using slskd.Integrations.Pushbullet;
    using slskd.Messaging;
    using slskd.Search;
    using slskd.Shares;
    using slskd.Transfers;
    using slskd.Users;
    using Soulseek;
    using Soulseek.Diagnostics;

    public interface IApplication : IHostedService
    {
        public Task CheckVersionAsync();
        public void CollectGarbage();
    }

    public sealed class Application : IApplication
    {
        /// <summary>
        ///     The name of the default user group.
        /// </summary>
        public static readonly string DefaultGroup = "default";

        /// <summary>
        ///     The name of the privileged user group.
        /// </summary>
        public static readonly string PrivilegedGroup = "privileged";

        /// <summary>
        ///     The name of the leecher user group.
        /// </summary>
        public static readonly string LeecherGroup = "leechers";

        private static readonly int ReconnectMaxDelayMilliseconds = 300000; // 5 minutes

        public Application(
            OptionsAtStartup optionsAtStartup,
            IOptionsMonitor<Options> optionsMonitor,
            IManagedState<State> state,
            ISoulseekClient soulseekClient,
            ITransferTracker transferTracker,
            ITransferService transferService,
            IBrowseTracker browseTracker,
            IConversationTracker conversationTracker,
            IRoomTracker roomTracker,
            IRoomService roomService,
            IUserService userService,
            IShareService shareService,
            IPushbulletService pushbulletService,
            IHubContext<ApplicationHub> applicationHub,
            IHubContext<LogsHub> logHub)
        {
            OptionsAtStartup = optionsAtStartup;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(async options => await OptionsMonitor_OnChange(options));

            PreviousOptions = OptionsMonitor.CurrentValue;

            State = state;
            State.OnChange(state => State_OnChange(state));

            Shares = shareService;
            Shares.StateMonitor.OnChange(state => ShareState_OnChange(state));

            TransferTracker = transferTracker;
            Transfers = transferService;
            BrowseTracker = browseTracker;
            ConversationTracker = conversationTracker;
            Pushbullet = pushbulletService;

            RoomService = roomService;
            Users = userService;
            ApplicationHub = applicationHub;

            LogHub = logHub;
            Program.LogEmitted += (_, log) => LogHub.EmitLogAsync(log);

            Client = soulseekClient;

            Client.DiagnosticGenerated += Client_DiagnosticGenerated;

            Client.TransferStateChanged += Client_TransferStateChanged;
            Client.TransferProgressUpdated += Client_TransferProgressUpdated;

            Client.BrowseProgressUpdated += Client_BrowseProgressUpdated;
            Client.UserStatusChanged += Client_UserStatusChanged;
            Client.PrivateMessageReceived += Client_PrivateMessageRecieved;

            Client.PrivateRoomMembershipAdded += (e, room) => Console.WriteLine($"Added to private room {room}");
            Client.PrivateRoomMembershipRemoved += (e, room) => Console.WriteLine($"Removed from private room {room}");
            Client.PrivateRoomModerationAdded += (e, room) => Console.WriteLine($"Promoted to moderator in private room {room}");
            Client.PrivateRoomModerationRemoved += (e, room) => Console.WriteLine($"Demoted from moderator in private room {room}");

            Client.PublicChatMessageReceived += Client_PublicChatMessageReceived;
            Client.RoomMessageReceived += Client_RoomMessageReceived;
            Client.Disconnected += Client_Disconnected;
            Client.Connected += Client_Connected;
            Client.LoggedIn += Client_LoggedIn;
            Client.StateChanged += Client_StateChanged;
            Client.DistributedNetworkStateChanged += Client_DistributedNetworkStateChanged;
            Client.DownloadDenied += (e, args) => Log.Information("Download of {Filename} from {Username} was denied: {Message}", args.Filename, args.Username, args.Message);
            Client.DownloadFailed += (e, args) => Log.Information("Download of {Filename} from {Username} failed", args.Filename, args.Username);
        }

        private ISoulseekClient Client { get; set; }
        private IRoomService RoomService { get; set; }
        private IBrowseTracker BrowseTracker { get; set; }
        private IConversationTracker ConversationTracker { get; set; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<Application>();
        private ConcurrentDictionary<string, ILogger> Loggers { get; } = new ConcurrentDictionary<string, ILogger>();
        private Options Options => OptionsMonitor.CurrentValue;
        private OptionsAtStartup OptionsAtStartup { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }
        private SemaphoreSlim OptionsSyncRoot { get; } = new SemaphoreSlim(1, 1);
        private Options PreviousOptions { get; set; }
        private IPushbulletService Pushbullet { get; }
        private DateTime SharesRefreshStarted { get; set; }
        private IManagedState<State> State { get; }
        private ITransferTracker TransferTracker { get; set; }
        private ITransferService Transfers { get; init; }
        private IHubContext<ApplicationHub> ApplicationHub { get; set; }
        private IHubContext<LogsHub> LogHub { get; set; }
        private IUserService Users { get; set; }
        private IShareService Shares { get; set; }
        private IMemoryCache Cache { get; set; } = new MemoryCache(new MemoryCacheOptions());

        public void CollectGarbage()
        {
            var sw = new Stopwatch();

            Log.Debug("Collecting garbage");

            sw.Start();

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
#pragma warning disable S1215 // "GC.Collect" should not be called
            GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
#pragma warning restore S1215 // "GC.Collect" should not be called

            sw.Stop();

            Log.Debug("Garbage collection completed in {Duration}ms", sw.ElapsedMilliseconds);
        }

        /// <summary>
        ///     Gets the version of the latest application release.
        /// </summary>
        /// <returns>The operation context.</returns>
        public async Task CheckVersionAsync()
        {
            if (Program.IsDevelopment)
            {
                Log.Information("Skipping version check for Development build");
                return;
            }

            if (Program.IsCanary)
            {
                // todo: use the docker hub API to find the latest canary tag
                Log.Information("Skipping version check for Canary build; check for updates manually.");
                return;
            }

            Log.Information("Checking GitHub Releases for latest version");

            try
            {
                var latestVersion = await GitHub.GetLatestReleaseVersion(
                    organization: Program.AppName,
                    repository: Program.AppName,
                    userAgent: $"{Program.AppName} v{Program.FullVersion}");

                if (latestVersion > Version.Parse(Program.SemanticVersion))
                {
                    State.SetValue(state => state with { Version = state.Version with { Latest = latestVersion.ToString(), IsUpdateAvailable = true } });
                    Log.Information("A new version is available! {CurrentVersion} -> {LatestVersion}", Program.SemanticVersion, latestVersion);
                }
                else
                {
                    State.SetValue(state => state with { Version = state.Version with { Latest = Program.SemanticVersion, IsUpdateAvailable = false } });
                    Log.Information("Version {CurrentVersion} is up to date.", Program.SemanticVersion);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to check version: {Message}", ex.Message);
                throw;
            }
        }

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            Log.Information("Configuring client");

            ProxyOptions proxyOptions = default;

            if (OptionsAtStartup.Soulseek.Connection.Proxy.Enabled)
            {
                proxyOptions = new ProxyOptions(
                    address: OptionsAtStartup.Soulseek.Connection.Proxy.Address,
                    port: OptionsAtStartup.Soulseek.Connection.Proxy.Port.Value,
                    username: OptionsAtStartup.Soulseek.Connection.Proxy.Username,
                    password: OptionsAtStartup.Soulseek.Connection.Proxy.Password);
            }

            var connectionOptions = new ConnectionOptions(
                readBufferSize: OptionsAtStartup.Soulseek.Connection.Buffer.Read,
                writeBufferSize: OptionsAtStartup.Soulseek.Connection.Buffer.Write,
                writeQueueSize: OptionsAtStartup.Soulseek.Connection.Buffer.WriteQueue,
                connectTimeout: OptionsAtStartup.Soulseek.Connection.Timeout.Connect,
                inactivityTimeout: OptionsAtStartup.Soulseek.Connection.Timeout.Inactivity,
                proxyOptions: proxyOptions);

            var configureKeepAlive = new Action<Socket>(socket =>
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, OptionsAtStartup.Soulseek.Connection.Timeout.Inactivity / 1000);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, OptionsAtStartup.Soulseek.Connection.Timeout.Inactivity / 1000);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
            });

            var serverOptions = connectionOptions.With(configureSocketAction: configureKeepAlive);
            var distributedOptions = connectionOptions.With(configureSocketAction: configureKeepAlive);

            var transferOptions = connectionOptions.With(
                readBufferSize: OptionsAtStartup.Soulseek.Connection.Buffer.Transfer,
                writeBufferSize: OptionsAtStartup.Soulseek.Connection.Buffer.Transfer);

            var patch = new SoulseekClientOptionsPatch(
                listenPort: OptionsAtStartup.Soulseek.ListenPort,
                enableListener: true,
                userEndPointCache: new UserEndPointCache(),
                distributedChildLimit: OptionsAtStartup.Soulseek.DistributedNetwork.ChildLimit,
                enableDistributedNetwork: !OptionsAtStartup.Soulseek.DistributedNetwork.Disabled,
                acceptDistributedChildren: !OptionsAtStartup.Soulseek.DistributedNetwork.DisableChildren,
                maximumUploadSpeed: OptionsAtStartup.Global.Upload.SpeedLimit,
                maximumDownloadSpeed: OptionsAtStartup.Global.Download.SpeedLimit,
                autoAcknowledgePrivateMessages: false,
                acceptPrivateRoomInvitations: true,
                serverConnectionOptions: serverOptions,
                peerConnectionOptions: connectionOptions,
                transferConnectionOptions: transferOptions,
                distributedConnectionOptions: distributedOptions,
                userInfoResolver: UserInfoResolver,
                browseResponseResolver: BrowseResponseResolver,
                directoryContentsResolver: DirectoryContentsResponseResolver,
                enqueueDownload: (username, endpoint, filename) => EnqueueDownloadAction(username, endpoint, filename, TransferTracker),
                searchResponseCache: new SearchResponseCache(),
                searchResponseResolver: SearchResponseResolver);

            await Client.ReconfigureOptionsAsync(patch);

            Log.Information("Client configured");
            Log.Information("Listening on port {Port}", OptionsAtStartup.Soulseek.ListenPort);

            if (OptionsAtStartup.Soulseek.Connection.Proxy.Enabled)
            {
                Log.Information($"Using Proxy {OptionsAtStartup.Soulseek.Connection.Proxy.Address}:{OptionsAtStartup.Soulseek.Connection.Proxy.Port}");
            }

            if (!OptionsAtStartup.NoVersionCheck)
            {
                _ = CheckVersionAsync();
            }

            if (OptionsAtStartup.NoShareScan)
            {
                Log.Warning("Not scanning shares; 'no-share-scan' option is enabled.  Search and browse results will remain disabled until a manual scan is completed.");
            }
            else
            {
                _ = Shares.StartScanAsync();
            }

            if (OptionsAtStartup.NoConnect)
            {
                Log.Warning("Not connecting to the Soulseek server; 'no-connect' option is enabled");
            }
            else if (string.IsNullOrEmpty(OptionsAtStartup.Soulseek.Username) || string.IsNullOrEmpty(OptionsAtStartup.Soulseek.Password))
            {
                Log.Warning($"Not connecting to the Soulseek server; username and/or password invalid.  Specify valid credentials and manually connect, or update config and restart.");
            }
            else
            {
                await Client.ConnectAsync(OptionsAtStartup.Soulseek.Username, OptionsAtStartup.Soulseek.Password).ConfigureAwait(false);
            }
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            Client.Disconnect("Shutting down", new ApplicationShutdownException("Shutting down"));
            Client.Dispose();
            Log.Information("Client stopped");
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Creates and returns an instances of <see cref="BrowseResponse"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving an IEnumerable of Soulseek.Directory.</returns>
        private async Task<BrowseResponse> BrowseResponseResolver(string username, IPEndPoint endpoint)
        {
            var directories = (await Shares.BrowseAsync())
                .Select(d => new Soulseek.Directory(d.Name.Replace('/', '\\'), d.Files)); // Soulseek NS requires backslashes

            return new BrowseResponse(directories);
        }

        private void Client_BrowseProgressUpdated(object sender, BrowseProgressUpdatedEventArgs args)
        {
            BrowseTracker.AddOrUpdate(args.Username, args);
        }

        private void Client_Connected(object sender, EventArgs e)
        {
            Log.Information("Connected to the Soulseek server");
        }

        private void Client_DiagnosticGenerated(object sender, DiagnosticEventArgs args)
        {
            static LogEventLevel TranslateLogLevel(DiagnosticLevel diagnosticLevel) => diagnosticLevel switch
            {
                DiagnosticLevel.Debug => LogEventLevel.Debug,
                DiagnosticLevel.Info => LogEventLevel.Debug,
                DiagnosticLevel.Warning => LogEventLevel.Warning,
                DiagnosticLevel.None => default,
                _ => default,
            };

            var source = sender.GetType().FullName;

            if (source.EndsWith("DistributedConnectionManager") && !Options.Soulseek.DistributedNetwork.Logging)
            {
                return;
            }

            var logger = Loggers.GetOrAdd(source, Log.ForContext("Context", "Soulseek").ForContext("SubContext", source));

            logger.Write(TranslateLogLevel(args.Level), "{@Message}", args.Message);
        }

        private async void Client_Disconnected(object sender, SoulseekClientDisconnectedEventArgs args)
        {
            if (State.CurrentValue.PendingReconnect)
            {
                State.SetValue(state => state with { PendingReconnect = false });
            }

            if (args.Exception is ObjectDisposedException || args.Exception is ApplicationShutdownException)
            {
                Log.Information("Disconnected from the Soulseek server: the client is shutting down");
            }
            else if (args.Exception is IntentionalDisconnectException)
            {
                Log.Information("Disconnected from the Soulseek server: disconnected by the user");
            }
            else if (args.Exception is LoginRejectedException)
            {
                Log.Error("Disconnected from the Soulseek server: invalid username or password");
            }
            else if (args.Exception is KickedFromServerException)
            {
                Log.Error("Disconnected from the Soulseek server: another client logged in using the username {Username}", Client.Username);
            }
            else
            {
                Log.Error("Disconnected from the Soulseek server: {Message}", args.Exception?.Message ?? args.Message);

                if (string.IsNullOrEmpty(Options.Soulseek.Username) || string.IsNullOrEmpty(Options.Soulseek.Password))
                {
                    Log.Warning($"Not reconnecting to the Soulseek server; username and/or password invalid.  Specify valid credentials and manually connect, or update config and restart.");
                    return;
                }

                var attempts = 1;

                while (true)
                {
                    var (delay, jitter) = Compute.ExponentialBackoffDelay(
                        iteration: attempts,
                        maxDelayInMilliseconds: ReconnectMaxDelayMilliseconds);

                    var approximateDelay = (int)Math.Ceiling((double)(delay + jitter) / 1000);
                    Log.Information($"Waiting about {(approximateDelay == 1 ? "a second" : $"{approximateDelay} seconds")} before reconnecting");
                    await Task.Delay(delay + jitter);

                    Log.Information($"Attempting to reconnect (#{attempts})...", attempts);

                    try
                    {
                        // reconnect with the latest configuration values we have for username and password, instead of the
                        // options that were captured at startup. if a user has updated these values prior to the disconnect, the
                        // changes will take effect now.
                        await Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);
                        break;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        Log.Error("Failed to reconnect: {Message}", ex.Message);
                    }
                }
            }
        }

        private async Task RefreshUserStatisticsIfNeeded(bool force = false)
        {
            if (force || !Cache.TryGetValue(CacheKeys.UserStatisticsToken, out _))
            {
                var stats = await Client.GetUserStatisticsAsync(OptionsAtStartup.Soulseek.Username);

                State.SetValue(state => state with
                {
                    Server = state.Server with
                    {
                        Statistics = stats,
                    },
                });

                Cache.Set(CacheKeys.UserStatisticsToken, true, TimeSpan.FromHours(4));
            }
        }

        private void Client_LoggedIn(object sender, EventArgs e)
        {
            Log.Information("Logged in to the Soulseek server as {Username}", Client.Username);

            // send whatever counts we have currently. we'll probably connect before the cache is primed, so these will be zero
            // initially, but we'll update them when the cache is filled.
            _ = Client.SetSharedCountsAsync(State.CurrentValue.Shares.Directories, State.CurrentValue.Shares.Files);

            // fetch our average upload speed from the server, so we can provide it along with search results
            _ = RefreshUserStatisticsIfNeeded(force: true);
        }

        private void Client_PrivateMessageRecieved(object sender, PrivateMessageReceivedEventArgs args)
        {
            ConversationTracker.AddOrUpdate(args.Username, PrivateMessage.FromEventArgs(args));

            if (Options.Integration.Pushbullet.Enabled && !args.Replayed)
            {
                Console.WriteLine("Pushing...");
                _ = Pushbullet.PushAsync($"Private Message from {args.Username}", args.Username, args.Message);
            }
        }

        private void Client_PublicChatMessageReceived(object sender, PublicChatMessageReceivedEventArgs args)
        {
            Console.WriteLine($"[PUBLIC CHAT] [{args.RoomName}] [{args.Username}]: {args.Message}");

            if (Options.Integration.Pushbullet.Enabled && args.Message.Contains(Client.Username))
            {
                _ = Pushbullet.PushAsync($"Room Mention by {args.Username} in {args.RoomName}", args.RoomName, args.Message);
            }
        }

        private void Client_RoomMessageReceived(object sender, RoomMessageReceivedEventArgs args)
        {
            var message = RoomMessage.FromEventArgs(args, DateTime.UtcNow);

            if (Options.Integration.Pushbullet.Enabled && message.Message.Contains(Client.Username))
            {
                _ = Pushbullet.PushAsync($"Room Mention by {message.Username} in {message.RoomName}", message.RoomName, message.Message);
            }
        }

        private void Client_StateChanged(object sender, SoulseekClientStateChangedEventArgs e)
        {
            State.SetValue(state => state with
            {
                Server = state.Server with
                {
                    Address = Client.Address,
                    IPEndPoint = Client.IPEndPoint,
                    State = Client.State,
                    Username = Client.Username,
                },
            });
        }

        private void Client_DistributedNetworkStateChanged(object sender, DistributedNetworkInfo e)
        {
            State.SetValue(state => state with
            {
                DistributedNetwork = new DistributedNetworkState()
                {
                    BranchLevel = e.BranchLevel,
                    BranchRoot = e.BranchRoot,
                    CanAcceptChildren = e.CanAcceptChildren,
                    ChildLimit = e.ChildLimit,
                    Children = e.Children.Select(c => c.Username).ToList().AsReadOnly(),
                    HasParent = e.HasParent,
                    IsBranchRoot = e.IsBranchRoot,
                    Parent = e.Parent.Username,
                },
            });
        }

        private void Client_TransferProgressUpdated(object sender, TransferProgressUpdatedEventArgs args)
        {
            // no-op. this is really verbose, use for troubleshooting.
        }

        private void Client_TransferStateChanged(object sender, TransferStateChangedEventArgs args)
        {
            var xfer = args.Transfer;
            var direction = xfer.Direction.ToString().ToUpper();
            var user = xfer.Username;
            var file = Path.GetFileName(xfer.Filename);
            var oldState = args.PreviousState;
            var state = xfer.State;

            var completed = xfer.State.HasFlag(TransferStates.Completed);

            Log.Information($"[{direction}] [{user}/{file}] {oldState} => {state}{(completed ? $" ({xfer.BytesTransferred}/{xfer.Size} = {xfer.PercentComplete}%) @ {xfer.AverageSpeed.SizeSuffix()}/s" : string.Empty)}");

            if (xfer.Direction == TransferDirection.Upload && xfer.State.HasFlag(TransferStates.Completed | TransferStates.Succeeded))
            {
                try
                {
                    _ = Client.SendUploadSpeedAsync((int)args.Transfer.AverageSpeed);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to report upload speed");
                }
            }
        }

        private void Client_UserStatusChanged(object sender, UserStatus args)
        {
            Console.WriteLine($"[USER] {args.Username}: {args.Presence}");
        }

        /// <summary>
        ///     Creates and returns a <see cref="Soulseek.Directory"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <param name="token">The unique token for the request, supplied by the requesting user.</param>
        /// <param name="directory">The requested directory.</param>
        /// <returns>A Task resolving an instance of Soulseek.Directory containing the contents of the requested directory.</returns>
        private async Task<Soulseek.Directory> DirectoryContentsResponseResolver(string username, IPEndPoint endpoint, int token, string directory)
        {
            var dir = await Shares.ListDirectoryAsync(directory);
            return dir;
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
        private async Task EnqueueDownloadAction(string username, IPEndPoint endpoint, string filename, ITransferTracker tracker)
        {
            _ = endpoint;
            string localFilename;
            FileInfo fileInfo = default;

            Console.WriteLine($"[UPLOAD REQUESTED] [{username}/{filename}]");

            try
            {
                localFilename = (await Shares.ResolveFilenameAsync(filename)).ToLocalOSPath();

                fileInfo = new FileInfo(localFilename);

                if (!fileInfo.Exists)
                {
                    throw new NotFoundException();
                }
            }
            catch (NotFoundException)
            {
                Console.WriteLine($"[UPLOAD REJECTED] File {filename} not found.");
                throw new DownloadEnqueueException($"File not shared.");
            }

            if (tracker.TryGet(TransferDirection.Upload, username, filename, out _))
            {
                // in this case, a re-requested file is a no-op. normally we'd want to respond with a PlaceInQueueResponse
                Console.WriteLine($"[UPLOAD RE-REQUESTED] [{username}/{filename}]");
                return;
            }

            // create a new cancellation token source so that we can cancel the upload from the UI.
            var cts = new CancellationTokenSource();
            var topts = new TransferOptions(
                stateChanged: (e) =>
                {
                    tracker.AddOrUpdate(e, cts);

                    if (e.Transfer.State.HasFlag(TransferStates.Queued))
                    {
                        Transfers.Uploads.Queue.Enqueue(e.Transfer);
                    }
                },
                progressUpdated: (e) => tracker.AddOrUpdate(e, cts),
                governor: (tx, req, ct) => Transfers.Uploads.Governor.GetBytesAsync(tx, req, ct),
                reporter: (tx, att, grant, act) => Transfers.Uploads.Governor.ReturnBytes(tx, att, grant, act),
                slotAwaiter: (tx, ct) => Transfers.Uploads.Queue.AwaitStartAsync(tx),
                slotReleased: (tx) => Transfers.Uploads.Queue.Complete(tx));

            // accept all download requests, and begin the upload immediately. normally there would be an internal queue, and
            // uploads would be handled separately.
            _ = Task.Run(async () =>
            {
                // users with uploads must be watched so that we can keep informed of their
                // online status, privileges, and statistics.  this is so that we can accurately
                // determine their effective group.
                if (!Users.IsWatched(username))
                {
                    await Users.WatchAsync(username);
                }

                using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read);
                await Client.UploadAsync(username, filename, fileInfo.FullName, options: topts, cancellationToken: cts.Token);
            }).ContinueWith(t =>
            {
                Console.WriteLine($"[UPLOAD FAILED] {t.Exception}");
            }, TaskContinuationOptions.NotOnRanToCompletion); // fire and forget
        }

        private async Task OptionsMonitor_OnChange(Options newOptions)
        {
            // this code is known to fire more than once per update. i'm not sure whether these might be executed concurrently.
            // lock to be safe, because we need to accurately track the last value of Options for diffing purposes. threading
            // shenanigans here could lead to missed updates.
            await OptionsSyncRoot.WaitAsync();

            try
            {
                var pendingRestart = false;
                var pendingReconnect = false;
                var soulseekRequiresReconnect = false;

                var diff = PreviousOptions.DiffWith(newOptions);

                // don't react to duplicate/no-change events https://github.com/slskd/slskd/issues/126
                if (!diff.Any())
                {
                    return;
                }

                foreach (var (property, fqn, left, right) in diff)
                {
                    static bool HasAttribute<T>(PropertyInfo property) => property.CustomAttributes.Any(a => a.AttributeType == typeof(T));

                    var requiresRestart = HasAttribute<RequiresRestartAttribute>(property);
                    var requiresReconnect = HasAttribute<RequiresReconnectAttribute>(property);

                    Log.Debug($"{fqn} changed from '{left.ToJson() ?? "<null>"}' to '{right.ToJson() ?? "<null>"}'{(requiresRestart ? ". Restart required to take effect." : string.Empty)}{(requiresReconnect ? "; Reconnect required to take effect." : string.Empty)}");

                    pendingRestart |= requiresRestart;
                    pendingReconnect |= requiresReconnect;
                }

                if (PreviousOptions.Directories.Shared.Except(newOptions.Directories.Shared).Any()
                    || newOptions.Directories.Shared.Except(PreviousOptions.Directories.Shared).Any())
                {
                    State.SetValue(state => state with { Shares = state.Shares with { ScanPending = true } });
                    Log.Information("Shared directory configuration changed.  Shares must be re-scanned for changes to take effect.");
                }

                if (PreviousOptions.Filters.Share.Except(newOptions.Filters.Share).Any()
                    || newOptions.Filters.Share.Except(PreviousOptions.Filters.Share).Any())
                {
                    State.SetValue(state => state with { Shares = state.Shares with { ScanPending = true } });
                    Log.Information("File filter configuration changed.  Shares must be re-scanned for changes to take effect.");
                }

                if (PreviousOptions.Rooms.Except(newOptions.Rooms).Any()
                    || newOptions.Rooms.Except(PreviousOptions.Rooms).Any())
                {
                    Log.Information("Room configuration changed.  Joining any newly added rooms.");
                    _ = RoomService.TryJoinAsync(newOptions.Rooms);
                }

                // determine whether any Soulseek options changed. if so, we need to construct a patch and invoke ReconfigureOptionsAsync().
                var slskDiff = PreviousOptions.Soulseek.DiffWith(newOptions.Soulseek);
                var globalDiff = PreviousOptions.Global.DiffWith(newOptions.Global);

                if (slskDiff.Any() || globalDiff.Any())
                {
                    var old = PreviousOptions.Soulseek;
                    var update = newOptions.Soulseek;

                    Log.Debug("Soulseek options changed from {Previous} to {Current}", old.ToJson(), update.ToJson());

                    // determine whether any Connection options changed. if so, replace the whole object. Soulseek.NET doesn't
                    // offer a way to patch parts of connection options. the updates only affect new connections, so a partial
                    // patch doesn't make a lot of sense anyway.
                    var connectionDiff = old.Connection.DiffWith(update.Connection);

                    ConnectionOptions connectionPatch = null;
                    ConnectionOptions serverPatch = null;
                    ConnectionOptions distributedPatch = null;
                    ConnectionOptions transferPatch = null;

                    if (connectionDiff.Any())
                    {
                        var connection = update.Connection;

                        ProxyOptions proxyPatch = null;

                        if (connection.Proxy.Enabled)
                        {
                            proxyPatch = new ProxyOptions(
                                address: connection.Proxy.Address,
                                port: connection.Proxy.Port.Value,
                                username: connection.Proxy.Username,
                                password: connection.Proxy.Password);
                        }

                        connectionPatch = new ConnectionOptions(
                            readBufferSize: connection.Buffer.Read,
                            writeBufferSize: connection.Buffer.Write,
                            writeQueueSize: connection.Buffer.WriteQueue,
                            connectTimeout: connection.Timeout.Connect,
                            inactivityTimeout: connection.Timeout.Inactivity,
                            proxyOptions: proxyPatch);

                        var configureKeepAlive = new Action<Socket>(socket =>
                        {
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, connection.Timeout.Inactivity / 1000);
                            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, connection.Timeout.Inactivity / 1000);
                            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
                        });

                        serverPatch = connectionPatch.With(configureSocketAction: configureKeepAlive);
                        distributedPatch = connectionPatch.With(configureSocketAction: configureKeepAlive);

                        transferPatch = connectionPatch.With(
                            readBufferSize: OptionsAtStartup.Soulseek.Connection.Buffer.Transfer,
                            writeBufferSize: OptionsAtStartup.Soulseek.Connection.Buffer.Transfer);
                    }

                    var patch = new SoulseekClientOptionsPatch(
                        listenPort: old.ListenPort == update.ListenPort ? null : update.ListenPort,
                        enableDistributedNetwork: old.DistributedNetwork.Disabled == update.DistributedNetwork.Disabled ? null : !update.DistributedNetwork.Disabled,
                        distributedChildLimit: old.DistributedNetwork.ChildLimit == update.DistributedNetwork.ChildLimit ? null : update.DistributedNetwork.ChildLimit,
                        acceptDistributedChildren: old.DistributedNetwork.DisableChildren == update.DistributedNetwork.DisableChildren ? null : !update.DistributedNetwork.DisableChildren,
                        maximumUploadSpeed: newOptions.Global.Upload.SpeedLimit,
                        maximumDownloadSpeed: newOptions.Global.Download.SpeedLimit,
                        serverConnectionOptions: serverPatch,
                        peerConnectionOptions: connectionPatch,
                        transferConnectionOptions: transferPatch,
                        incomingConnectionOptions: connectionPatch,
                        distributedConnectionOptions: distributedPatch);

                    soulseekRequiresReconnect = await Client.ReconfigureOptionsAsync(patch);
                }

                // require a reconnect if the client is connected and any options marked [RequiresReconnect] changed, OR if the
                // call to reconfigure the client requires a reconnect
                if ((Client.State.HasFlag(SoulseekClientStates.Connected) && pendingReconnect) || soulseekRequiresReconnect)
                {
                    State.SetValue(state => state with { PendingReconnect = true });
                    Log.Information("One or more updated Soulseek options requires the client to be disconnected, then reconnected to the network to take effect.");
                }

                if (pendingRestart)
                {
                    State.SetValue(state => state with { PendingRestart = true });
                    Log.Information("One or more updated options requires an application restart to take effect.");
                }

                PreviousOptions = newOptions;
                _ = ApplicationHub.BroadcastOptionsAsync(newOptions);

                Log.Information("Options updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply option update: {Message}", ex.Message);
            }
            finally
            {
                OptionsSyncRoot.Release();
            }
        }

        /// <summary>
        ///     Creates and returns a <see cref="Response"/> in response to the given <paramref name="query"/>.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="token">The search token.</param>
        /// <param name="query">The search query.</param>
        /// <returns>A Task resolving a SearchResponse, or null.</returns>
        private async Task<SearchResponse> SearchResponseResolver(string username, int token, SearchQuery query)
        {
            // some bots and perhaps users search for very short terms. only respond to queries >= 3 characters. sorry, U2 fans.
            if (query.Query.Length < 3)
            {
                return null;
            }

            var results = await Shares.SearchAsync(query);

            if (results.Any())
            {
                Console.WriteLine($"[SENDING SEARCH RESULTS]: {results.Count()} records to {username} for query {query.SearchText}");
                await RefreshUserStatisticsIfNeeded();

                return new SearchResponse(
                    Client.Username,
                    token,
                    freeUploadSlots: 1,
                    uploadSpeed: State.CurrentValue.Server.Statistics.AverageSpeed,
                    queueLength: 0,
                    fileList: results);
            }

            // if no results, either return null or an instance of SearchResponse with a fileList of length 0 in either case, no
            // response will be sent to the requestor.
            return null;
        }

        private void ShareState_OnChange((ShareState Previous, ShareState Current) state)
        {
            var (previous, current) = state;

            if (!previous.Scanning && current.Scanning)
            {
                SharesRefreshStarted = DateTime.UtcNow;

                State.SetValue(s => s with { Shares = current });
                Log.Information("Scanning shares");
            }

            var lastProgress = Math.Round(previous.ScanProgress * 100);
            var currentProgress = Math.Round(current.ScanProgress * 100);

            if (lastProgress != currentProgress && Math.Round(currentProgress, 0) % 10 == 0)
            {
                State.SetValue(s => s with { Shares = current });
                Log.Information("Scanned {Percent}% of shared directories.  Found {Files} files so far.", currentProgress, current.Files);
            }

            if (previous.Scanning && !current.Scanning)
            {
                State.SetValue(s => s with { Shares = current });

                if (current.Faulted)
                {
                    Log.Error("Failed to scan shares.");
                }
                else
                {
                    State.SetValue(state => state with { Shares = state.Shares with { ScanPending = false } });
                    Log.Information("Shares scanned successfully. Found {Directories} directories and {Files} files in {Duration}ms", current.Directories, current.Files, (DateTime.UtcNow - SharesRefreshStarted).TotalMilliseconds);

                    SharesRefreshStarted = default;

                    if (Client.State.HasFlag(SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn))
                    {
                        _ = Client.SetSharedCountsAsync(State.CurrentValue.Shares.Directories, State.CurrentValue.Shares.Files);
                    }
                }
            }
        }

        private void State_OnChange((State Previous, State Current) state)
        {
            _ = ApplicationHub.BroadcastStateAsync(state.Current);
        }

        /// <summary>
        ///     Creates and returns a <see cref="UserInfo"/> object in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving the UserInfo instance.</returns>
        private Task<UserInfo> UserInfoResolver(string username, IPEndPoint endpoint)
        {
            var info = new UserInfo(
                description: Options.Soulseek.Description,
                uploadSlots: 1,
                queueLength: 0,
                hasFreeUploadSlot: false);

            return Task.FromResult(info);
        }
    }
}
