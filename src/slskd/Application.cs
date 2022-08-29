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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime;
    using System.Text.RegularExpressions;
    using System.Runtime.InteropServices;
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

        private static readonly string ApplicationShutdownTransferExceptionMessage = "Application shut down";

        public Application(
            OptionsAtStartup optionsAtStartup,
            IOptionsMonitor<Options> optionsMonitor,
            IManagedState<State> state,
            ISoulseekClient soulseekClient,
            IConnectionWatchdog connectionWatchdog,
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
            Console.CancelKeyPress += (_, args) =>
            {
                ShuttingDown = true;
                Log.Warning("Received SIGINT");
            };

            foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM })
            {
                PosixSignalRegistration.Create(signal, context =>
                {
                    ShuttingDown = true;
                    Log.Fatal("Received {Signal}", signal);
                });
            }

            OptionsAtStartup = optionsAtStartup;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(async options => await OptionsMonitor_OnChange(options));

            PreviousOptions = OptionsMonitor.CurrentValue;

            CompiledSearchResponseFilters = OptionsAtStartup.Filters.Search.Request.Select(f => new Regex(f, RegexOptions.Compiled));

            State = state;
            State.OnChange(state => State_OnChange(state));

            Shares = shareService;
            Shares.StateMonitor.OnChange(state => ShareState_OnChange(state));

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

            Client.PrivateRoomMembershipAdded += (e, room) => Log.Information("Added to private room {Room}", room);
            Client.PrivateRoomMembershipRemoved += (e, room) => Log.Information("Removed from private room {Room}", room);
            Client.PrivateRoomModerationAdded += (e, room) => Log.Information("Promoted to moderator in private room {Room}", room);
            Client.PrivateRoomModerationRemoved += (e, room) => Log.Information("Demoted from moderator in private room {Room}", room);

            Client.PublicChatMessageReceived += Client_PublicChatMessageReceived;
            Client.RoomMessageReceived += Client_RoomMessageReceived;
            Client.Disconnected += Client_Disconnected;
            Client.Connected += Client_Connected;
            Client.LoggedIn += Client_LoggedIn;
            Client.StateChanged += Client_StateChanged;
            Client.DistributedNetworkStateChanged += Client_DistributedNetworkStateChanged;
            Client.DownloadDenied += (e, args) => Log.Information("Download of {Filename} from {Username} was denied: {Message}", args.Filename, args.Username, args.Message);
            Client.DownloadFailed += (e, args) => Log.Information("Download of {Filename} from {Username} failed", args.Filename, args.Username);

            ConnectionWatchdog = connectionWatchdog;

            Clock.EveryMinute += Clock_EveryMinute;
        }

        /// <summary>
        ///     Gets a value indicating whether the application is in the process of shutting down.
        /// </summary>
        public static bool IsShuttingDown => Environment.HasShutdownStarted || ShuttingDown;

        private static bool ShuttingDown { get; set; } = false;

        private ISoulseekClient Client { get; set; }
        private IRoomService RoomService { get; set; }
        private IBrowseTracker BrowseTracker { get; set; }
        private IConnectionWatchdog ConnectionWatchdog { get; }
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
        private ITransferService Transfers { get; init; }
        private IHubContext<ApplicationHub> ApplicationHub { get; set; }
        private IHubContext<LogsHub> LogHub { get; set; }
        private IUserService Users { get; set; }
        private IShareService Shares { get; set; }
        private IMemoryCache Cache { get; set; } = new MemoryCache(new MemoryCacheOptions());
        private IEnumerable<Regex> CompiledSearchResponseFilters { get; set; }
        private IEnumerable<Guid> ActiveDownloadIdsAtPreviousShutdown { get; set; } = Enumerable.Empty<Guid>();

        public void CollectGarbage()
        {
            var sw = new Stopwatch();

            Log.Debug("Collecting garbage");

            sw.Start();

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
#pragma warning disable S1215 // "GC.Collect" should not be called
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
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
            // if the application shut down "uncleanly", transfers may need to be cleaned up. we deliberately don't allow these
            // records to be updated if the application has started to shut down so that we can do this cleanup and properly
            // disposition them as having failed due to an application shutdown, instead of some random exception thrown while
            // things are being disposed.
            var activeUploads = Transfers.Uploads.List(t => !t.State.HasFlag(TransferStates.Completed) && !t.Removed)
                .Where(t => !t.State.HasFlag(TransferStates.Completed)) // https://github.com/dotnet/efcore/issues/10434
                .ToList();

            foreach (var upload in activeUploads)
            {
                Log.Debug("Cleaning up dangling upload {Filename} to {Username}", upload.Filename, upload.Username);
                upload.State = TransferStates.Completed | TransferStates.Errored;
                upload.Exception = ApplicationShutdownTransferExceptionMessage;
                Transfers.Uploads.Update(upload);
            }

            var activeDownloads = Transfers.Downloads.List(t => !t.State.HasFlag(TransferStates.Completed) && !t.Removed)
                .Where(t => !t.State.HasFlag(TransferStates.Completed)) // https://github.com/dotnet/efcore/issues/10434
                .ToList();

            foreach (var download in activeDownloads)
            {
                Log.Debug("Cleaning up dangling download {Filename} from {Username}", download.Filename, download.Username);
                download.State = TransferStates.Completed | TransferStates.Errored;
                download.Exception = ApplicationShutdownTransferExceptionMessage;
                Transfers.Downloads.Update(download);
            }

            // save the ids of any downloads that were active, so we can re-enqueue them after we've connected and logged in.
            // we need to check the database before we re-request to make sure the user didn't remove them from the UI while
            // the application was running, but before it was logged in. so just save the ids.
            ActiveDownloadIdsAtPreviousShutdown = activeDownloads.Select(d => d.Id);
            Log.Debug("Downloads to resume upon connection: {Ids}", ActiveDownloadIdsAtPreviousShutdown.ToJson());

            Log.Debug("Configuring client");

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
                enqueueDownload: (username, endpoint, filename) => Transfers.Uploads.EnqueueAsync(username, filename),
                searchResponseCache: new SearchResponseCache(),
                searchResponseResolver: SearchResponseResolver,
                placeInQueueResolver: PlaceInQueueResolver);

            await Client.ReconfigureOptionsAsync(patch);

            Log.Debug("Client configured");
            Log.Information("Listening for incoming connections on port {Port}", OptionsAtStartup.Soulseek.ListenPort);

            if (OptionsAtStartup.Soulseek.Connection.Proxy.Enabled)
            {
                Log.Information($"Using Proxy {OptionsAtStartup.Soulseek.Connection.Proxy.Address}:{OptionsAtStartup.Soulseek.Connection.Proxy.Port}");
            }

            if (!OptionsAtStartup.Flags.NoVersionCheck)
            {
                _ = CheckVersionAsync();
            }

            if (OptionsAtStartup.Flags.NoShareScan)
            {
                Log.Warning("Not scanning shares; 'no-share-scan' option is enabled.  Search and browse results will remain disabled until a manual scan is completed.");
            }
            else
            {
                _ = Shares.StartScanAsync();
            }

            if (OptionsAtStartup.Flags.NoConnect)
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
            ShuttingDown = true;
            Log.Warning("Application is shutting down");
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
            try
            {
                var sw = new Stopwatch();
                sw.Start();

                var directories = (await Shares.BrowseAsync())
                    .Select(d => new Soulseek.Directory(d.Name.Replace('/', '\\'), d.Files)); // Soulseek NS requires backslashes

                sw.Stop();

                Metrics.Browse.ResponseLatency.Observe(sw.ElapsedMilliseconds);
                Metrics.Browse.CurrentResponseLatency.Update(sw.ElapsedMilliseconds);
                Metrics.Browse.ResponsesSent.Inc(1);

                return new BrowseResponse(directories);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve browse response: {Message}", ex.Message);
                throw;
            }
        }

        private void Client_BrowseProgressUpdated(object sender, BrowseProgressUpdatedEventArgs args)
        {
            BrowseTracker.AddOrUpdate(args.Username, args);
        }

        private void Client_Connected(object sender, EventArgs e)
        {
            ConnectionWatchdog.Stop();
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

        private void Client_Disconnected(object sender, SoulseekClientDisconnectedEventArgs args)
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
                Log.Error("Disconnected from the Soulseek server: another client logged in using the same username");
            }
            else
            {
                Log.Error("Disconnected from the Soulseek server: {Message}", args.Exception?.Message ?? args.Message);
                ConnectionWatchdog.Start();
            }
        }

        private async Task RefreshUserStatistics(bool force = false)
        {
            if (force || !Cache.TryGetValue(CacheKeys.UserStatisticsToken, out _))
            {
                var stats = await Client.GetUserStatisticsAsync(OptionsAtStartup.Soulseek.Username);
                var privileges = await Client.GetPrivilegesAsync();

                State.SetValue(state => state with
                {
                    User = state.User with
                    {
                        Statistics = stats.ToUserStatisticsState(),
                        Privileges = new UserPrivilegeState()
                        {
                            IsPrivileged = privileges > 0,
                            PrivilegesRemaining = privileges,
                        },
                    },
                });

                Cache.Set(CacheKeys.UserStatisticsToken, true, TimeSpan.FromHours(4));
            }
        }

        private async void Client_LoggedIn(object sender, EventArgs e)
        {
            Log.Information("Logged in to the Soulseek server as {Username}", Client.Username);

            try
            {
                // send whatever counts we have currently. we'll probably connect before the cache is primed, so these will be zero
                // initially, but we'll update them when the cache is filled.
                await Client.SetSharedCountsAsync(State.CurrentValue.Shares.Directories, State.CurrentValue.Shares.Files);

                // fetch our average upload speed from the server, so we can provide it along with search results
                await RefreshUserStatistics(force: true);

                // we previously saved a list of all of the download ids that were active at the previous shutdown; fetch the latest
                // record for those transfers from the db and ensure they haven't been removed from the UI while the application was offline
                // the user doesn't want those transfers anymore and we don't want to add them back.
                var resumeableDownloads = Transfers.Downloads.List(t => !t.Removed && ActiveDownloadIdsAtPreviousShutdown.Contains(t.Id));

                if (resumeableDownloads.Any())
                {
                    Log.Information("Attempting to re-enqueue previously active downloads...");

                    var groups = resumeableDownloads.GroupBy(d => d.Username);

                    // re-request downloads. we use a try/catch here because there's a very good chance that the other user is offline.
                    foreach (var group in groups)
                    {
                        var username = group.Key;
                        var files = group.Select(f => (f.Filename, f.Size));

                        try
                        {
                            await Transfers.Downloads.EnqueueAsync(username, files);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("Failed to re-enqueue {Count} file(s) from {Username}: {Message}", files.Count(), username, ex.Message);
                        }
                    }
                }

                // clear the ids we saved at startup; we don't want to re-request these again if the connection is cycled
                ActiveDownloadIdsAtPreviousShutdown = Enumerable.Empty<Guid>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to execute post-login actions");
            }
        }

        private void Client_PrivateMessageRecieved(object sender, PrivateMessageReceivedEventArgs args)
        {
            ConversationTracker.AddOrUpdate(args.Username, PrivateMessage.FromEventArgs(args));

            if (Options.Integration.Pushbullet.Enabled && !args.Replayed)
            {
                _ = Pushbullet.PushAsync($"Private Message from {args.Username}", args.Username, args.Message);
            }
        }

        private void Client_PublicChatMessageReceived(object sender, PublicChatMessageReceivedEventArgs args)
        {
            Log.Information("[Public Chat/{Room}] [{Username}]: {Message}", args.RoomName, args.Username, args.Message);

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
                },
                User = state.User with
                {
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

            Metrics.DistributedNetwork.HasParent.Set(e.HasParent ? 1 : 0);
            Metrics.DistributedNetwork.BranchLevel.Set(e.BranchLevel);
            Metrics.DistributedNetwork.ChildLimit.Set(e.ChildLimit);
            Metrics.DistributedNetwork.Children.Set(e.Children.Count);
        }

        private void Clock_EveryMinute(object sender, EventArgs e)
        {
            Metrics.DistributedNetwork.BroadcastLatency.Observe(Client.DistributedNetwork.AverageBroadcastLatency ?? 0);
            Metrics.DistributedNetwork.CurrentBroadcastLatency.Set(Client.DistributedNetwork.AverageBroadcastLatency ?? 0);
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
        }

        private Task<int?> PlaceInQueueResolver(string username, IPEndPoint endpoint, string filename)
        {
            try
            {
                var place = Transfers.Uploads.Queue.EstimatePosition(username, filename);
                return Task.FromResult((int?)place);
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult<int?>(null);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to estimate place in queue for {Filename} requested by {Username}", filename, username);
                throw;
            }
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
            try
            {
                var dir = await Shares.ListDirectoryAsync(directory);
                return dir;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve directory contents: {Message}", ex.Message);
                throw;
            }
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

                if (PreviousOptions.Filters.Search.Request.Except(newOptions.Filters.Search.Request).Any()
                    || newOptions.Filters.Search.Request.Except(PreviousOptions.Filters.Search.Request).Any())
                {
                    CompiledSearchResponseFilters = newOptions.Filters.Search.Request.Select(f => new Regex(f, RegexOptions.Compiled));
                    Log.Information("Updated and re-compiled search response filters");
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
            try
            {
                Metrics.Search.RequestsReceived.Inc(1);

                if (CompiledSearchResponseFilters.Any(filter => filter.IsMatch(query.SearchText)))
                {
                    return null;
                }

                // sometimes clients send search queries consisting only of exclusions; drop them.
                // no other clients send search results for these, even though it is technically possible.
                if (!query.Terms.Any())
                {
                    return null;
                }

                try
                {
                    var sw = new Stopwatch();
                    sw.Start();

                    var results = await Shares.SearchAsync(query);

                    sw.Stop();

                    Metrics.Search.ResponseLatency.Observe(sw.ElapsedMilliseconds);
                    Metrics.Search.CurrentResponseLatency.Update(sw.ElapsedMilliseconds);

                    if (results.Any())
                    {
                        // make sure our average speed (as reported by the server) is reasonably up to date
                        await RefreshUserStatistics();

                        var forecastedPosition = Transfers.Uploads.Queue.ForecastPosition(username);

                        Log.Information("[{Context}]: Sending {Count} records to {Username} for query '{Query}'", "SEARCH RESULT SENT", results.Count(), username, query.SearchText);

                        Metrics.Search.ResponsesSent.Inc(1);

                        return new SearchResponse(
                            Client.Username,
                            token,
                            uploadSpeed: State.CurrentValue.User.Statistics.AverageSpeed,
                            freeUploadSlots: forecastedPosition == 0 ? 1 : 0,
                            queueLength: forecastedPosition,
                            fileList: results);
                    }

                    // if no results, either return null or an instance of SearchResponse with a fileList of length 0 in either case, no
                    // response will be sent to the requestor.
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to resolve search response: {Message}", ex.Message);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        private void ShareState_OnChange((ShareState Previous, ShareState Current) state)
        {
            var (previous, current) = state;

            if (!previous.Scanning && current.Scanning)
            {
                // the scan is starting
                SharesRefreshStarted = DateTime.UtcNow;

                State.SetValue(s => s with { Shares = current });
                Log.Information("Scanning shares");
            }
            else if (previous.Scanning && !current.Scanning)
            {
                // the scan is finishing
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
                        _ = Task.Run(async () =>
                        {
                            await Client.SetSharedCountsAsync(State.CurrentValue.Shares.Directories, State.CurrentValue.Shares.Files);
                            await RefreshUserStatistics(force: true);
                        });
                    }
                }
            }
            else
            {
                // the scan is neither starting nor finishing; progress update
                var lastProgress = Math.Round(previous.ScanProgress * 100);
                var currentProgress = Math.Round(current.ScanProgress * 100);

                if (lastProgress != currentProgress && Math.Round(currentProgress, 0) % 5d == 0)
                {
                    State.SetValue(s => s with { Shares = current });
                    Log.Information("Scanned {Percent}% of shared directories. Found {Files} files so far.", currentProgress, current.Files);
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
            try
            {
                var groupName = Users.GetGroup(username);
                var group = Transfers.Uploads.Queue.GetGroupInfo(groupName);

                // forecast the position at which this user would enter the queue if they were to request
                // a file at this moment. this will be zero if a slot is available and the transfer would
                // begin immediately
                var forecastedPosition = Transfers.Uploads.Queue.ForecastPosition(username);

                // if i get a user's info to determine whether i want to download files from them,
                // i want to know how many slots they have, which gives me an idea of how fast their
                // queue moves, and the length of the queue *ahead of me*, meaning how long i'd have to
                // wait until my first download starts.
                var info = new UserInfo(
                    description: Options.Soulseek.Description,
                    uploadSlots: group.Slots,
                    queueLength: forecastedPosition,
                    hasFreeUploadSlot: forecastedPosition == 0);

                return Task.FromResult(info);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve user info: {Message}", ex.Message);
                throw;
            }
        }
    }
}
