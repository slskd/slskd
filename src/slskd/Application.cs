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
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
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
    using slskd.Relay;
    using slskd.Search;
    using slskd.Shares;
    using slskd.Transfers;
    using slskd.Transfers.API;
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
        public const string DefaultGroup = "default";

        /// <summary>
        ///     The name of the privileged user group.
        /// </summary>
        public const string PrivilegedGroup = "privileged";

        /// <summary>
        ///     The name of the leecher user group.
        /// </summary>
        public const string LeecherGroup = "leechers";

        /// <summary>
        ///     The name of the blacklisted user group.
        /// </summary>
        public const string BlacklistedGroup = "blacklisted";

        private static readonly string ApplicationShutdownTransferExceptionMessage = "Application shut down";

        public Application(
            OptionsAtStartup optionsAtStartup,
            IOptionsMonitor<Options> optionsMonitor,
            IManagedState<State> state,
            ISoulseekClient soulseekClient,
            IConnectionWatchdog connectionWatchdog,
            ITransferService transferService,
            IBrowseTracker browseTracker,
            IRoomTracker roomTracker,
            IRoomService roomService,
            IUserService userService,
            IMessagingService messagingService,
            IShareService shareService,
            IPushbulletService pushbulletService,
            IRelayService relayService,
            IHubContext<ApplicationHub> applicationHub,
            IHubContext<LogsHub> logHub)
        {
            Console.CancelKeyPress += (_, args) =>
            {
                ShuttingDown = true;
                Program.MasterCancellationTokenSource.Cancel();
                Log.Warning("Received SIGINT");
            };

            foreach (var signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM })
            {
                PosixSignalRegistration.Create(signal, context =>
                {
                    ShuttingDown = true;
                    Program.MasterCancellationTokenSource.Cancel();
                    Log.Fatal("Received {Signal}", signal);
                    Environment.Exit(1);
                });
            }

            OptionsAtStartup = optionsAtStartup;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(async options => await OptionsMonitor_OnChange(options));

            PreviousOptions = OptionsMonitor.CurrentValue;

            Flags = Program.Flags;

            var regexOptions = RegexOptions.Compiled;

            if (!Flags.CaseSensitiveRegEx)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }

            CompiledSearchResponseFilters = OptionsAtStartup.Filters.Search.Request.Select(f => new Regex(f, regexOptions));

            State = state;
            State.OnChange(state => State_OnChange(state));

            Shares = shareService;
            Shares.StateMonitor.OnChange(state => ShareState_OnChange(state));

            Transfers = transferService;
            BrowseTracker = browseTracker;
            Pushbullet = pushbulletService;

            RoomService = roomService;
            Users = userService;
            Messaging = messagingService;
            ApplicationHub = applicationHub;

            Relay = relayService;
            Relay.StateMonitor.OnChange(relayState => State.SetValue(state => state with { Relay = relayState.Current }));

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
            Clock.EveryFiveMinutes += Clock_EveryFiveMinutes;
            Clock.EveryThirtyMinutes += Clock_EveryThirtyMinutes;
            Clock.EveryHour += Clock_EveryHour;
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
        private IMessagingService Messaging { get; set; }
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
        private IRelayService Relay { get; set; }
        private IMemoryCache Cache { get; set; } = new MemoryCache(new MemoryCacheOptions());
        private IEnumerable<Regex> CompiledSearchResponseFilters { get; set; }
        private IEnumerable<Guid> ActiveDownloadIdsAtPreviousShutdown { get; set; } = Enumerable.Empty<Guid>();
        private Options.FlagsOptions Flags { get; set; }

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
            Log.Information("Application started");

            // if the application shut down "uncleanly", transfers may need to be cleaned up. we deliberately don't allow these
            // records to be updated if the application has started to shut down so that we can do this cleanup and properly
            // disposition them as having failed due to an application shutdown, instead of some random exception thrown while
            // things are being disposed.
            var activeUploads = Transfers.Uploads.List(t => !t.State.HasFlag(TransferStates.Completed), includeRemoved: false)
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
                listenIPAddress: IPAddress.Parse(OptionsAtStartup.Soulseek.ListenIpAddress),
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
                enqueueDownload: EnqueueDownload,
                searchResponseCache: new SearchResponseCache(),
                searchResponseResolver: SearchResponseResolver,
                placeInQueueResolver: PlaceInQueueResolver);

            await Client.ReconfigureOptionsAsync(patch);

            Log.Debug("Client configured");
            Log.Information("Listening for incoming connections on {IP}:{Port}", OptionsAtStartup.Soulseek.ListenIpAddress, OptionsAtStartup.Soulseek.ListenPort);

            if (OptionsAtStartup.Soulseek.Connection.Proxy.Enabled)
            {
                Log.Information($"Using Proxy {OptionsAtStartup.Soulseek.Connection.Proxy.Address}:{OptionsAtStartup.Soulseek.Connection.Proxy.Port}");
            }

            if (!OptionsAtStartup.Flags.NoVersionCheck)
            {
                _ = CheckVersionAsync();
            }

            if (!OptionsAtStartup.Flags.NoShareScan)
            {
                try
                {
                    await Shares.InitializeAsync(forceRescan: OptionsAtStartup.Flags.ForceShareScan);
                }
                catch (Exception)
                {
                    Log.Error("Failed to initialize shares. Sharing is disabled");
                }
            }

            // the placement of this may be a bit contentious long term; when the clock starts, all of the
            // scheduled tasks will fire for the first time. the clock EventArgs contain a FirstRun flag
            // to help timed logic decide whether to run, but as a safeguard the start is delayed until
            // *just* before the application connects to the server/controller, meaning all configuration
            // and bootup tasks are complete. if this needs to be moved higher, a way to defer tasks from
            // the first run of the clock will need to be introduced, and those deferred tasks will need
            // to be executed around here somewhere.
            Log.Information("Starting system clock...");
            await Clock.StartAsync();
            Log.Information("System clock started");

            if (OptionsAtStartup.Relay.Enabled && OptionsAtStartup.Relay.Mode.ToEnum<RelayMode>() == RelayMode.Agent)
            {
                Log.Information("Running in Agent relay mode; not connecting to the Soulseek server.");
                await Relay.Client.StartAsync(cancellationToken);
            }
            else
            {
                if (OptionsAtStartup.Relay.Enabled)
                {
                    Log.Information("Running in Controller relay mode.  Listening for incoming Agent connections.");

                    if (OptionsAtStartup.Relay.Mode.ToEnum<RelayMode>() == RelayMode.Debug)
                    {
                        Log.Warning("Running in Debug relay mode; connecting to controller");
                        _ = Relay.Client.StartAsync(cancellationToken);
                    }
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
                    var opt = OptionsAtStartup.Soulseek;

                    await Client.ConnectAsync(
                        address: opt.Address,
                        port: opt.Port,
                        username: opt.Username,
                        password: opt.Password).ConfigureAwait(false);
                }
            }
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            ShuttingDown = true;
            Log.Warning("Application is shutting down");

            Clock.Stop();

            Client.Disconnect("Shutting down", new ApplicationShutdownException("Shutting down"));
            Client.Dispose();
            Log.Information("Client stopped");
            return Task.CompletedTask;
        }

        private async Task EnqueueDownload(string username, IPEndPoint endpoint, string filename)
        {
            if (Users.IsBlacklisted(username, endpoint.Address))
            {
                Log.Information("Rejected enqueue request for blacklisted user {Username} ({IP})", username, endpoint.Address);
                throw new DownloadEnqueueException("File not shared.");
            }

            // get the user's group. this will be the name of the user's group, if they have been added to a
            // user defined group, or one of the built-ins; 'default', 'privileged', 'leecher', or 'blacklisted'
            var group = await Users.GetOrFetchGroupAsync(username);

            // privileged users aren't subject to limits (for now)
            // i'm putting this off because 1) limits are unique to slskd, so all other clients are "unlimited"
            // and 2) i can't figure out what the limits would be, if not unlimited. users should get some level
            // of control, but i'd need to figure out a lower bound
            if (string.Equals(group, PrivilegedGroup))
            {
                await Transfers.Uploads.EnqueueAsync(username, filename);
            }

            Options.GroupsOptions.LimitsOptions limits;

            if (Options.Groups.UserDefined.TryGetValue(group, out var userDefinedOptions))
            {
                limits = userDefinedOptions.Limits;
            }
            else
            {
                limits = group switch
                {
                    DefaultGroup => Options.Groups.Default.Limits,
                    LeecherGroup => Options.Groups.Leechers.Limits,
                    _ => Options.Groups.Default.Limits,
                };
            }

            // if no limits are set, we're good to go; just enqueue the file.
            if (limits is null || (limits.Daily is null && limits.Weekly is null && limits.Queued is null))
            {
                await Transfers.Uploads.EnqueueAsync(username, filename);
            }

            var sw = new Stopwatch();
            sw.Start();

            // in order to properly determine if the requested file would exceed any limits, we need to know the size of the file
            // this is unfortunately not very straightforward; the size depends on which share repository the file is in and which
            // host is hosting the file. figure all that out here
            (string Host, string Filename, long Size) resolved;

            try
            {
                resolved = await Shares.ResolveFileAsync(filename);
            }
            catch (NotFoundException)
            {
                throw new DownloadEnqueueException("File not shared.");
            }

            /*
             * we have limits set, so now we have to fetch the data and compare to see if any would be hit if we allow this transfer to be enqueued.
             * the strategy here is to summarize all uploads:
             * 1) belonging to this user
             * 2) that were started within the time period
             * 3) that did not end due to an error (state includes errored, exception column is set)
            */
            bool OverLimits((int Transfers, int Files, int Directories, long Bytes) stats, Options.GroupsOptions.LimitsExtendedOptions options, out string reason)
            {
                reason = null;
                var byteLimit = options.Megabytes * 1000 * 1000;

                if (stats.Bytes > byteLimit)
                {
                    reason = $"bytes {stats.Bytes} exceeds limit of {byteLimit}";
                    return true;
                }

                if (stats.Files > options.Files)
                {
                    reason = $"files {stats.Files} exceeds limit of {options.Files}";
                    return true;
                }

                if (stats.Directories > options.Directories)
                {
                    reason = $"directories {stats.Directories} exceeds limit of {options.Directories}";
                    return true;
                }

                return false;
            }

            // start with the queue, since that should contain the fewest files and should be the least expensive to check
            // "queued" includes both queued and in progress; records with a null EndedAt property, which is guaranteed to be set
            // for terminal transfers.
            var queued = Transfers.Uploads.Hypothesize(
                expression: t => t.Username == username && t.EndedAt == null,
                hypothetical: new Transfers.Transfer()
                {
                    Filename = filename,
                    Size = resolved.Size,
                });

            Log.Debug("Fetched queue stats: {@Stats} ({Time}ms)", queued, sw.ElapsedMilliseconds);

            if (OverLimits(queued, limits.Queued, out var queuedReason))
            {
                Log.Information("Rejected enqueue request for user {Username}: Queued {Reason}", username, queuedReason);
                throw new DownloadEnqueueException($"Queued {queuedReason}");
            }

            // start with weekly, as this is the most likely limit to be hit and we want to keep the work to a minimum
            var erroredState = TransferStates.Completed | TransferStates.Errored;
            var cutoffDateTime = DateTime.UtcNow.AddDays(-7);
            var weekly = Transfers.Uploads.Hypothesize(
                expression: t =>
                    t.Username == username
                    && t.StartedAt >= cutoffDateTime
                    && !t.State.HasFlag(erroredState)
                    && t.Exception == null,
                hypothetical: new Transfers.Transfer()
                {
                    Filename = filename,
                    Size = resolved.Size,
                });

            Log.Debug("Fetched weekly stats: {Stats} ({Time}ms)", weekly, sw.ElapsedMilliseconds);

            if (OverLimits(weekly, limits.Weekly, out var weeklyReason))
            {
                Log.Information("Rejected enqueue request for user {Username}: Weekly {Reason}", weeklyReason);
                throw new DownloadEnqueueException($"Weekly {weeklyReason}");
            }

            cutoffDateTime = DateTime.UtcNow.AddDays(-1);
            var daily = Transfers.Uploads.Hypothesize(
                expression: t =>
                    t.Username == username
                    && t.StartedAt >= cutoffDateTime
                    && !t.State.HasFlag(erroredState)
                    && t.Exception == null,
                hypothetical: new Transfers.Transfer()
                {
                    Filename = filename,
                    Size = resolved.Size,
                });

            Log.Debug("Fetched daily stats: {Stats} ({Time}ms)", weekly, sw.ElapsedMilliseconds);

            if (OverLimits(daily, limits.Daily, out var dailyReason))
            {
                Log.Information("Rejected enqueue request for user {Username}: Daily {Reason}", dailyReason);
                throw new DownloadEnqueueException($"Daily {dailyReason}");
            }

            sw.Stop();
            Log.Debug("Enqueue decision made in {Duration}ms", sw.ElapsedMilliseconds);

            await Transfers.Uploads.EnqueueAsync(username, filename);
        }

        /// <summary>
        ///     Creates and returns an instances of <see cref="BrowseResponse"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving an IEnumerable of Soulseek.Directory.</returns>
        private async Task<BrowseResponse> BrowseResponseResolver(string username, IPEndPoint endpoint)
        {
            Metrics.Browse.RequestsReceived.Inc(1);

            if (Users.IsBlacklisted(username, endpoint.Address))
            {
                Log.Information("Returned empty browse listing for blacklisted user {Username} ({IP})", username, endpoint.Address);
                return new BrowseResponse();
            }

            try
            {
                var sw = new Stopwatch();
                sw.Start();

                BrowseResponse response = default;

                var cacheFilename = Path.Combine(Program.DataDirectory, "browse.cache");
                var cacheFileInfo = new FileInfo(cacheFilename);

                if (!cacheFileInfo.Exists)
                {
                    Log.Warning("Browse response not cached. Rebuilding...");
                    response = await CacheBrowseResponse();
                }
                else
                {
                    var stream = new FileStream(cacheFilename, FileMode.Open, FileAccess.Read);
                    response = new RawBrowseResponse(cacheFileInfo.Length, stream);
                }

                Log.Information("Sent browse response to {User}", username);

                sw.Stop();

                Metrics.Browse.ResponseLatency.Observe(sw.ElapsedMilliseconds);
                Metrics.Browse.CurrentResponseLatency.Update(sw.ElapsedMilliseconds);
                Metrics.Browse.ResponsesSent.Inc(1);

                return response;
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
                var stats = await Client.GetUserStatisticsAsync(Client.Username);
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
            Messaging.Conversations.HandleMessageAsync(args.Username, PrivateMessage.FromEventArgs(args));

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

        private void Clock_EveryMinute(object sender, ClockEventArgs e)
        {
            Metrics.DistributedNetwork.BroadcastLatency.Observe(Client.DistributedNetwork.AverageBroadcastLatency ?? 0);
            Metrics.DistributedNetwork.CurrentBroadcastLatency.Set(Client.DistributedNetwork.AverageBroadcastLatency ?? 0);
        }

        private void Clock_EveryFiveMinutes(object sender, ClockEventArgs e)
        {
            _ = Task.Run(() => PruneTransfers());
        }

        private void Clock_EveryThirtyMinutes(object sender, ClockEventArgs e)
        {
            _ = Task.Run(() => PruneFiles());
        }

        private void Clock_EveryHour(object sender, ClockEventArgs e)
        {
            _ = Task.Run(() => MaybeRescanShares());
        }

        private async Task MaybeRescanShares()
        {
            // ignore this if we're already scanning. there are multiple safeguards using
            // SemaphoreSlim later in the call chain if we manage to slip by somehow, though
            if (Shares.StateMonitor.CurrentValue.Scanning)
            {
                return;
            }

            var ttl = Options.Shares.Cache.Retention;

            // no configured TTL; never re-scan automatically
            if (!ttl.HasValue)
            {
                return;
            }

            // "x minutes ago"
            var cutoffTimestamp = DateTimeOffset.UtcNow
                .AddMinutes(-ttl.Value)
                .ToUnixTimeMilliseconds();

            // get a list of all scans that 1) started after our cutoff timestamp
            var scansInRange = await Shares.ListScansAsync(startedAtOrAfter: cutoffTimestamp);

            // 2) that succeeded (have a valid EndedAt)
            // and 3) have not been marked suspect
            // if there's no scan that meets all 3 criteria, try to re-scan
            if (!scansInRange.Any(s => s.EndedAt is not null && !s.Suspect))
            {
                Log.Information("Beginning scheduled re-scan of shares (previous scan is older than {Minutes} minutes)", ttl);

                // fire and forget. if something slips through the underlying logic will log.
                _ = Task.Run(() => Shares.ScanAsync());
            }
        }

        private void PruneFiles()
        {
            void PruneDirectory(int? age, string directory)
            {
                try
                {
                    if (!age.HasValue)
                    {
                        return;
                    }

                    Log.Debug("Pruning files older than {Age} minutes from {Directory}", age, directory);

                    var options = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                        RecurseSubdirectories = true,
                    };

                    var files = System.IO.Directory.GetFiles(directory, "*", options)
                        .Select(filename => new FileInfo(filename))
                        .Where(file => file.LastAccessTimeUtc <= DateTime.UtcNow.AddMinutes(-age.Value));

                    Log.Debug("Found {Count} files of need of pruning", files.Count());

                    int errors = 0;

                    foreach (var file in files)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            Log.Warning(ex, "Failed to prune file {File}: {Message}", file, ex.Message);
                        }
                    }

                    Log.Debug("Pruning complete. Deleted: {Deleted}, Errors: {Errors}", files.Count() - errors, errors);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to prune files in directory {Directory}: {Message}", directory, ex.Message);
                }
            }

            PruneDirectory(age: Options.Retention.Files.Incomplete, directory: Options.Directories.Incomplete);
            PruneDirectory(age: Options.Retention.Files.Complete, directory: Options.Directories.Downloads);
        }

        private void PruneTransfers()
        {
            var options = OptionsMonitor.CurrentValue.Retention;

            void PruneUpload(int? age, TransferStates state)
            {
                if (age.HasValue)
                {
                    Transfers.Uploads.Prune(age.Value, TransferStates.Completed | state);
                }
            }

            void PruneDownload(int? age, TransferStates state)
            {
                if (age.HasValue)
                {
                    Transfers.Downloads.Prune(age.Value, TransferStates.Completed | state);
                }
            }

            try
            {
                PruneUpload(options.Transfers.Upload.Succeeded, TransferStates.Succeeded);
                PruneUpload(options.Transfers.Upload.Cancelled, TransferStates.Cancelled);
                PruneUpload(options.Transfers.Upload.Errored, TransferStates.Errored);

                PruneDownload(options.Transfers.Download.Succeeded, TransferStates.Succeeded);
                PruneDownload(options.Transfers.Download.Cancelled, TransferStates.Cancelled);
                PruneDownload(options.Transfers.Download.Errored, TransferStates.Errored);
            }
            catch
            {
                Log.Error("Encountered one or more errors while pruning transfers");
            }
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

            if (xfer.Direction == TransferDirection.Upload && xfer.State.HasFlag(TransferStates.Completed | TransferStates.Succeeded) && args.Transfer.AverageSpeed > 0)
            {
                try
                {
                    _ = Client.SendUploadSpeedAsync(Convert.ToInt32(Math.Ceiling(args.Transfer.AverageSpeed)));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to report upload speed");
                }
            }
        }

        private void Client_UserStatusChanged(object sender, UserStatus args)
        {
            // todo: react to watched user status changes
        }

        private Task<int?> PlaceInQueueResolver(string username, IPEndPoint endpoint, string filename)
        {
            try
            {
                var place = Transfers.Uploads.Queue.EstimatePosition(username, filename);
                return Task.FromResult((int?)place);
            }
            catch (NotFoundException)
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
            if (Users.IsBlacklisted(username, endpoint.Address))
            {
                Log.Information("Returned empty directory listing for blacklisted user {Username} ({IP})", username, endpoint.Address);
                return new Soulseek.Directory(directory);
            }

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

                if (PreviousOptions.Shares.Directories.Except(newOptions.Shares.Directories).Any()
                    || newOptions.Shares.Directories.Except(PreviousOptions.Shares.Directories).Any())
                {
                    State.SetValue(state => state with { Shares = state.Shares with { ScanPending = true } });
                    Log.Information("Shared directory configuration changed.  Shares must be re-scanned for changes to take effect.");
                }

                if (PreviousOptions.Shares.Filters.Except(newOptions.Shares.Filters).Any()
                    || newOptions.Shares.Filters.Except(PreviousOptions.Shares.Filters).Any())
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
                        listenIPAddress: old.ListenIpAddress == update.ListenIpAddress ? null : IPAddress.Parse(update.ListenIpAddress),
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
            Metrics.Search.RequestsReceived.Inc(1);

            if (Users.IsBlacklisted(username))
            {
                Log.Information("Returned empty search response for blacklisted user {Username}", username);
                return new SearchResponse(username, token, hasFreeUploadSlot: false, uploadSpeed: 0, queueLength: int.MaxValue, fileList: Enumerable.Empty<Soulseek.File>());
            }

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

                    // note: the following uses cached user data to determine group, so if the user's data
                    // isn't cached they may get a forecast based on the wrong group.  this is a hot path though,
                    // and we don't want to incur the massive penalties that would caching data for each request.
                    var forecastedPosition = Transfers.Uploads.Queue.ForecastPosition(username);

                    Log.Information("[{Context}]: Sending {Count} records to {Username} for query '{Query}'", "SEARCH RESULT SENT", results.Count(), username, query.SearchText);

                    Metrics.Search.ResponsesSent.Inc(1);

                    return new SearchResponse(
                        Client.Username,
                        token,
                        uploadSpeed: State.CurrentValue.User.Statistics.AverageSpeed,
                        hasFreeUploadSlot: forecastedPosition == 0,
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

        private void ShareState_OnChange((ShareState Previous, ShareState Current) state)
        {
            var (previous, current) = state;
            bool rebuildBrowseCache = false;

            if (!previous.Scanning && current.Scanning)
            {
                // the scan is starting. update the application state without manipulation
                SharesRefreshStarted = DateTime.UtcNow;

                State.SetValue(s => s with { Shares = current });

                Log.Information("Share scan started");
            }
            else if (previous.Scanning && !current.Scanning)
            {
                // the scan is finishing. update the application state without manipulation...
                State.SetValue(s => s with { Shares = current });

                if (current.Faulted)
                {
                    Log.Error("Failed to scan shares.");
                }
                else
                {
                    // ...but if it completed successfully, immediately update again to lower the pending flag
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
            else if (!previous.Ready && current.Ready)
            {
                // the share transitioned into ready without completing a scan; it was loaded from disk
                // this will (or should) also be true when the scan completes, which is why the code is using an else if here,
                // but only if it happens without a corresponding lowering of the scanning flag do we want to execute this.
                State.SetValue(state => state with { Shares = current with { ScanPending = false } });
                Log.Information("Share cache loaded from disk successfully. Sharing {Directories} directories and {Files} files", current.Directories, current.Files);
                rebuildBrowseCache = true;
            }
            else
            {
                // the scan is neither starting nor finishing; this is a status update of some sort,
                // not a state change. we want to clamp the frequency of these during a scan to avoid overwhelming clients,
                // only update if the progress has changed by at least 1%.
                var lastProgress = Math.Round(previous.ScanProgress * 100);
                var currentProgress = Math.Round(current.ScanProgress * 100);

                if (lastProgress != currentProgress && Math.Round(currentProgress, 0) % 1d == 0)
                {
                    State.SetValue(s => s with { Shares = current });
                    Log.Information("Scanned {Percent}% of shared directories. Found {Files} files so far.", currentProgress, current.Files);
                }
            }

            // if the host configuration changed, update *just* the hosts to avoid stepping on anything that might
            // have also happened above. a change in hosts will invalidate the cache, so do that too.
            if (previous.Hosts.ToJson() != current.Hosts.ToJson())
            {
                State.SetValue(state => state with
                {
                    Shares = state.Shares with
                    {
                        Hosts = current.Hosts,
                        Directories = current.Directories,
                        Files = current.Files,
                    },
                });

                rebuildBrowseCache = true;
            }

            // if a scan just completed successfully (but not failed!), shares were loaded from disk, or
            // host configuration changed, rebuild caches and upload shares to the network controller (if connected)
            if (rebuildBrowseCache)
            {
                _ = CacheBrowseResponse();
                _ = Relay.Client.SynchronizeAsync();
            }
        }

        private async Task<BrowseResponse> CacheBrowseResponse()
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();

                var directories = await Shares.BrowseAsync();
                var response = new BrowseResponse(directories);
                var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                var destination = Path.Combine(Program.DataDirectory, "browse.cache");

                Log.Information("Warming browse response cache...");
                await System.IO.File.WriteAllBytesAsync(temp, response.ToByteArray());

                Log.Debug("Saved cache to temp file {File}", temp);

                System.IO.File.Move(temp, destination, overwrite: true);

                sw.Stop();
                Log.Information("Browse response cached successfully in {Duration}ms", sw.ElapsedMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error caching browse response: {Message}", ex.Message);
                throw;
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
        private async Task<UserInfo> UserInfoResolver(string username, IPEndPoint endpoint)
        {
            if (Users.IsBlacklisted(username, endpoint.Address))
            {
                return new UserInfo(
                    description: Options.Soulseek.Description,
                    uploadSlots: 0,
                    queueLength: int.MaxValue,
                    hasFreeUploadSlot: false);
            }

            try
            {
                // note: users must first be watched or cached for leech and privilege detection to work.
                // we are deliberately skipping it here; if the username is watched
                // leech detection works and they get accurate info, if not, they won't
                var groupName = await Users.GetOrFetchGroupAsync(username);
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

                return info;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve user info: {Message}", ex.Message);
                throw;
            }
        }
    }
}
