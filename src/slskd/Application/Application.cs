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

namespace slskd
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Serilog;
    using Serilog.Events;
    using slskd.Configuration;
    using slskd.Integrations.Pushbullet;
    using slskd.Messaging;
    using slskd.Peer;
    using slskd.Search;
    using slskd.Transfer;
    using Soulseek;
    using Soulseek.Diagnostics;

    public class Application : IHostedService
    {
        private static readonly int ReconnectMaxDelayMilliseconds = 300000; // 5 minutes

        public Application(
            OptionsAtStartup optionsAtStartup,
            IOptionsMonitor<Options> optionsMonitor,
            IStateMonitor<ApplicationState> stateMonitor,
            ITransferTracker transferTracker,
            IBrowseTracker browseTracker,
            IConversationTracker conversationTracker,
            IRoomTracker roomTracker,
            ISharedFileCache sharedFileCache,
            IPushbulletService pushbulletService)
        {
            OptionsAtStartup = optionsAtStartup;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(async options => await OptionsMonitor_OnChange(options));

            PreviousOptions = OptionsMonitor.CurrentValue;

            StateMonitor = stateMonitor;
            StateMonitor.OnChange(state => StateMonitor_OnChange(state));

            SharedFileCache = sharedFileCache;
            SharedFileCache.State.OnChange(state => SharedFileCacheState_OnChange(state));

            TransferTracker = transferTracker;
            BrowseTracker = browseTracker;
            ConversationTracker = conversationTracker;
            RoomTracker = roomTracker;
            Pushbullet = pushbulletService;

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
                connectTimeout: OptionsAtStartup.Soulseek.Connection.Timeout.Connect,
                inactivityTimeout: OptionsAtStartup.Soulseek.Connection.Timeout.Inactivity,
                proxyOptions: proxyOptions);

            var clientOptions = new SoulseekClientOptions(
                listenPort: OptionsAtStartup.Soulseek.ListenPort,
                enableListener: true,
                userEndPointCache: new UserEndPointCache(),
                distributedChildLimit: OptionsAtStartup.Soulseek.DistributedNetwork.ChildLimit,
                enableDistributedNetwork: !OptionsAtStartup.Soulseek.DistributedNetwork.Disabled,
                minimumDiagnosticLevel: OptionsAtStartup.Soulseek.DiagnosticLevel,
                autoAcknowledgePrivateMessages: false,
                acceptPrivateRoomInvitations: true,
                serverConnectionOptions: connectionOptions,
                peerConnectionOptions: connectionOptions,
                transferConnectionOptions: connectionOptions,
                distributedConnectionOptions: connectionOptions,
                userInfoResponseResolver: UserInfoResponseResolver,
                browseResponseResolver: BrowseResponseResolver,
                directoryContentsResponseResolver: DirectoryContentsResponseResolver,
                enqueueDownloadAction: (username, endpoint, filename) => EnqueueDownloadAction(username, endpoint, filename, TransferTracker),
                searchResponseCache: new SearchResponseCache(),
                searchResponseResolver: SearchResponseResolver);

            Client = new SoulseekClient(options: clientOptions);

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
            Client.RoomJoined += Client_RoomJoined;
            Client.RoomLeft += Client_RoomLeft;
            Client.Disconnected += Client_Disconnected;
            Client.Connected += Client_Connected;
            Client.LoggedIn += Client_LoggedIn;

            SoulseekClient = Client;
        }

        public static ISoulseekClient SoulseekClient { get; private set; }

        private IBrowseTracker BrowseTracker { get; set; }
        private ISoulseekClient Client { get; set; }
        private IConversationTracker ConversationTracker { get; set; }
        private ILogger Logger { get; set; } = Log.ForContext<Application>();
        private ConcurrentDictionary<string, ILogger> Loggers { get; } = new ConcurrentDictionary<string, ILogger>();
        private IOptionsMonitor<Options> OptionsMonitor { get; set; }
        private Options Options => OptionsMonitor.CurrentValue;
        private OptionsAtStartup OptionsAtStartup { get; set; }
        private Options PreviousOptions { get; set; }
        private IRoomTracker RoomTracker { get; set; }
        private IStateMonitor<ApplicationState> StateMonitor { get; set; }
        private ApplicationState State => StateMonitor.CurrentValue;
        private ISharedFileCache SharedFileCache { get; set; }
        private ITransferTracker TransferTracker { get; set; }
        private IPushbulletService Pushbullet { get; }
        private ReaderWriterLockSlim OptionsSyncRoot { get; } = new ReaderWriterLockSlim();
        private DateTime SharesRefreshStarted { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (OptionsAtStartup.NoShareScan)
            {
                Logger.Warning("Not scanning shares; 'no-share-scan' option is enabled.  Search and browse results will remain disabled until a manual scan is completed.");
            }
            else
            {
                _ = SharedFileCache.FillAsync();
            }

            if (OptionsAtStartup.Soulseek.Connection.Proxy.Enabled)
            {
                Logger.Information($"Using Proxy {OptionsAtStartup.Soulseek.Connection.Proxy.Address}:{OptionsAtStartup.Soulseek.Connection.Proxy.Port}");
            }

            Logger.Information("Client started");
            Logger.Information("Listening on port {Port}", OptionsAtStartup.Soulseek.ListenPort);

            if (OptionsAtStartup.NoConnect)
            {
                Logger.Warning("Not connecting to the Soulseek server; 'no-connect' option is enabled");
            }
            else if (string.IsNullOrEmpty(OptionsAtStartup.Soulseek.Username) || string.IsNullOrEmpty(OptionsAtStartup.Soulseek.Password))
            {
                Logger.Warning($"Not connecting to the Soulseek server; username and/or password invalid.  Specify valid credentials and manually connect, or update config and restart.");
            }
            else
            {
                await Client.ConnectAsync(OptionsAtStartup.Soulseek.Username, OptionsAtStartup.Soulseek.Password).ConfigureAwait(false);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Client.Disconnect("Shutting down", new ApplicationShutdownException("Shutting down"));
            Client.Dispose();
            Logger.Information("Client stopped");
            return Task.CompletedTask;
        }

        private async Task OptionsMonitor_OnChange(Options newOptions)
        {
            // this code is known to fire more than once per update.  i'm not sure
            // whether these might be executed concurrently. lock to be safe, because
            // we need to accurately track the last value of Options for diffing purposes.
            // threading shenanigans here could lead to missed updates.
            OptionsSyncRoot.EnterWriteLock();

            try
            {
                var pendingRestart = false;
                var pendingReconnect = false;
                var soulseekRequiresReconnect = false;

                var diff = PreviousOptions.DiffWith(newOptions);

                // don't react to duplicate/no-change events
                // https://github.com/slskd/slskd/issues/126
                if (!diff.Any())
                {
                    return;
                }

                foreach (var (property, fqn, left, right) in diff)
                {
                    static bool HasAttribute<T>(PropertyInfo property) => property.CustomAttributes.Any(a => a.AttributeType == typeof(T));

                    var requiresRestart = HasAttribute<RequiresRestartAttribute>(property);
                    var requiresReconnect = HasAttribute<RequiresReconnectAttribute>(property);

                    Logger.Debug($"{fqn} changed from '{left.ToJson() ?? "<null>"}' to '{right.ToJson() ?? "<null>"}'{(requiresRestart ? "; Restart required to take effect." : string.Empty)}{(requiresReconnect ? "; Reconnect required to take effect." : string.Empty)}");

                    pendingRestart |= requiresRestart;
                    pendingReconnect |= requiresReconnect;
                }

                if (PreviousOptions.Directories.Shared.Except(newOptions.Directories.Shared).Any()
                    || newOptions.Directories.Shared.Except(PreviousOptions.Directories.Shared).Any())
                {
                    StateMonitor.SetValue(state => state with { PendingShareRescan = true });
                    Logger.Information("Shared directory configuration changed.  Shares must be re-scanned for changes to take effect.");
                }

                // determine whether any Soulseek options changed.  if so, we need to construct a patch
                // and invoke ReconfigureOptionsAsync().
                var slskDiff = PreviousOptions.Soulseek.DiffWith(newOptions.Soulseek);

                if (slskDiff.Any())
                {
                    var old = PreviousOptions.Soulseek;
                    var update = newOptions.Soulseek;

                    Logger.Debug("Soulseek options changed from {Previous} to {Current}", old.ToJson(), update.ToJson());

                    // determine whether any Connection options changed. if so, replace the whole object.
                    // Soulseek.NET doesn't offer a way to patch parts of connection options. the updates only affect
                    // new connections, so a partial patch doesn't make a lot of sense anyway.
                    var connectionDiff = old.Connection.DiffWith(update.Connection);

                    ConnectionOptions connectionPatch = null;

                    if (connectionDiff.Any())
                    {
                        var connection = update.Connection;

                        ProxyOptions proxyPatch = null;

                        if (connection.Proxy.Enabled)
                        {
                            proxyPatch = new ProxyOptions(
                                connection.Proxy.Address,
                                connection.Proxy.Port.Value,
                                connection.Proxy.Username,
                                connection.Proxy.Password);
                        }

                        connectionPatch = new ConnectionOptions(
                            connection.Buffer.Read,
                            connection.Buffer.Write,
                            connection.Timeout.Connect,
                            connection.Timeout.Inactivity,
                            proxyOptions: proxyPatch);
                    }

                    var patch = new SoulseekClientOptionsPatch(
                        listenPort: old.ListenPort == update.ListenPort ? null : update.ListenPort,
                        enableDistributedNetwork: old.DistributedNetwork.Disabled == update.DistributedNetwork.Disabled ? null : !update.DistributedNetwork.Disabled,
                        distributedChildLimit: old.DistributedNetwork.ChildLimit == update.DistributedNetwork.ChildLimit ? null : update.DistributedNetwork.ChildLimit,
                        serverConnectionOptions: connectionPatch,
                        peerConnectionOptions: connectionPatch,
                        transferConnectionOptions: connectionPatch,
                        incomingConnectionOptions: connectionPatch,
                        distributedConnectionOptions: connectionPatch);

                    Logger.Debug("Patching Soulseek options with {Patch}", patch.ToJson());

                    soulseekRequiresReconnect = await SoulseekClient.ReconfigureOptionsAsync(patch);
                }

                // require a reconnect if the client is connected and any options marked [RequiresReconnect] changed,
                // OR if the call to reconfigure the client requires a reconnect
                if ((Client.State.HasFlag(SoulseekClientStates.Connected) && pendingReconnect) || soulseekRequiresReconnect)
                {
                    StateMonitor.SetValue(state => state with { PendingReconnect = true });
                    Logger.Information("One or more updated Soulseek options requires the client to be disconnected, then reconnected to the network to take effect.");
                }

                if (pendingRestart)
                {
                    StateMonitor.SetValue(state => state with { PendingRestart = true });
                    Logger.Information("One or more updated options requires an application restart to take effect.");
                }

                Logger.Information("Options updated successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply option update: {Message}", ex.Message);
            }
            finally
            {
                PreviousOptions = OptionsMonitor.CurrentValue;
                OptionsSyncRoot.ExitWriteLock();
            }
        }

        private void StateMonitor_OnChange((ApplicationState Previous, ApplicationState Current) state)
        {
            Logger.Debug("State changed from {Previous} to {Current}", state.Previous.ToJson(), state.Current.ToJson());
        }

        private void SharedFileCacheState_OnChange((SharedFileCacheState Previous, SharedFileCacheState Current) state)
        {
            if (!state.Previous.Filling && state.Current.Filling)
            {
                SharesRefreshStarted = DateTime.UtcNow;

                StateMonitor.SetValue(s => s with { SharedFileCache = state.Current });
                Logger.Information("Scanning shares");
            }

            var lastProgress = Math.Round(state.Previous.FillProgress * 100);
            var currentProgress = Math.Round(state.Current.FillProgress * 100);

            if (lastProgress != currentProgress && Math.Round(currentProgress, 0) % 10 == 0)
            {
                StateMonitor.SetValue(s => s with { SharedFileCache = state.Current });
                Logger.Information("Scanned {Percent}% of shared directories.  Found {Files} files so far.", currentProgress, state.Current.Files);
            }

            if (state.Previous.Filling && !state.Current.Filling)
            {
                StateMonitor.SetValue(s => s with { SharedFileCache = state.Current });

                if (state.Current.Faulted)
                {
                    Logger.Error("Failed to scan shares.");
                }
                else
                {
                    StateMonitor.SetValue(s => s with { PendingShareRescan = false });
                    Logger.Information("Shares scanned successfully. Found {Directories} directories and {Files} files in {Duration}ms", state.Current.Directories, state.Current.Files, (DateTime.UtcNow - SharesRefreshStarted).TotalMilliseconds);

                    SharesRefreshStarted = default;

                    if (Client.State.HasFlag(SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn))
                    {
                        _ = SoulseekClient.SetSharedCountsAsync(State.SharedFileCache.Directories, State.SharedFileCache.Files);
                    }
                }
            }
        }

        /// <summary>
        ///     Creates and returns an instances of <see cref="BrowseResponse"/> in response to a remote request.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="endpoint">The IP endpoint of the requesting user.</param>
        /// <returns>A Task resolving an IEnumerable of Soulseek.Directory.</returns>
        private Task<BrowseResponse> BrowseResponseResolver(string username, IPEndPoint endpoint)
        {
            var directories = SharedFileCache.Browse()
                .Select(d => new Soulseek.Directory(d.Name.Replace('/', '\\'), d.Files)); // Soulseek NS requires backslashes

            return Task.FromResult(new BrowseResponse(directories));
        }

        private void Client_BrowseProgressUpdated(object sender, BrowseProgressUpdatedEventArgs args)
        {
            BrowseTracker.AddOrUpdate(args.Username, args);
        }

        private void Client_DiagnosticGenerated(object sender, DiagnosticEventArgs args)
        {
            static LogEventLevel TranslateLogLevel(DiagnosticLevel diagnosticLevel) => diagnosticLevel switch
            {
                DiagnosticLevel.Debug => LogEventLevel.Debug,
                DiagnosticLevel.Info => LogEventLevel.Information,
                DiagnosticLevel.Warning => LogEventLevel.Warning,
                DiagnosticLevel.None => default,
                _ => default,
            };

            var logger = Loggers.GetOrAdd(sender.GetType().FullName, Log.ForContext("SourceContext", "Soulseek").ForContext("SoulseekContext", sender.GetType().FullName));

            logger.Write(TranslateLogLevel(args.Level), "{@Message}", args.Message);
        }

        private void Client_Connected(object sender, EventArgs e)
        {
            Logger.Information("Connected to the Soulseek server");
        }

        private void Client_LoggedIn(object sender, EventArgs e)
        {
            Logger.Information("Logged in to the Soulseek server as {Username}", Client.Username);

            // send whatever counts we have currently. we'll probably connect before the cache is primed,
            // so these will be zero initially, but we'll update them when the cache is filled.
            _ = SoulseekClient.SetSharedCountsAsync(State.SharedFileCache.Directories, State.SharedFileCache.Files);
        }

        private async void Client_Disconnected(object sender, SoulseekClientDisconnectedEventArgs args)
        {
            if (State.PendingReconnect)
            {
                StateMonitor.SetValue(state => state with { PendingReconnect = false });
            }

            if (args.Exception is ObjectDisposedException || args.Exception is ApplicationShutdownException)
            {
                Logger.Information("Disconnected from the Soulseek server: the client is shutting down");
            }
            else if (args.Exception is IntentionalDisconnectException)
            {
                Logger.Information("Disconnected from the Soulseek server: disconnected by the user");
            }
            else if (args.Exception is LoginRejectedException)
            {
                Logger.Error("Disconnected from the Soulseek server: invalid username or password");
            }
            else if (args.Exception is KickedFromServerException)
            {
                Logger.Error("Disconnected from the Soulseek server: another client logged in using the username {Username}", Client.Username);
            }
            else
            {
                Logger.Error("Disconnected from the Soulseek server: {Message}", args.Exception?.Message ?? args.Message);

                if (string.IsNullOrEmpty(Options.Soulseek.Username) || string.IsNullOrEmpty(Options.Soulseek.Password))
                {
                    Logger.Warning($"Not reconnecting to the Soulseek server; username and/or password invalid.  Specify valid credentials and manually connect, or update config and restart.");
                    return;
                }

                var attempts = 1;

                while (true)
                {
                    var (delay, jitter) = Compute.ExponentialBackoffDelay(
                        iteration: attempts,
                        maxDelayInMilliseconds: ReconnectMaxDelayMilliseconds);

                    var approximateDelay = (int)Math.Ceiling((double)(delay + jitter) / 1000);
                    Logger.Information($"Waiting about {(approximateDelay == 1 ? "a second" : $"{approximateDelay} seconds")} before reconnecting");
                    await Task.Delay(delay + jitter);

                    Logger.Information($"Attempting to reconnect (#{attempts})...", attempts);

                    try
                    {
                        // reconnect with the latest configuration values we have for username and password, instead of the options that were
                        // captured at startup.  if a user has updated these values prior to the disconnect, the changes will take effect now.
                        await Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);
                        break;
                    }
                    catch (Exception ex)
                    {
                        attempts++;
                        Logger.Error("Failed to reconnect: {Message}", ex.Message);
                    }
                }
            }
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

        private void Client_RoomJoined(object sender, RoomJoinedEventArgs args)
        {
            // this will fire when we join a room; track that through the join operation.
            if (args.Username != Client.Username)
            {
                RoomTracker.TryAddUser(args.RoomName, args.UserData);
            }
        }

        private void Client_RoomLeft(object sender, RoomLeftEventArgs args)
        {
            RoomTracker.TryRemoveUser(args.RoomName, args.Username);
        }

        private void Client_RoomMessageReceived(object sender, RoomMessageReceivedEventArgs args)
        {
            var message = RoomMessage.FromEventArgs(args, DateTime.UtcNow);
            RoomTracker.AddOrUpdateMessage(args.RoomName, message);

            if (Options.Integration.Pushbullet.Enabled && message.Message.Contains(Client.Username))
            {
                _ = Pushbullet.PushAsync($"Room Mention by {message.Username} in {message.RoomName}", message.RoomName, message.Message);
            }
        }

        private void Client_TransferProgressUpdated(object sender, TransferProgressUpdatedEventArgs args)
        {
            // this is really verbose. Console.WriteLine($"[{args.Transfer.Direction.ToString().ToUpper()}]
            // [{args.Transfer.Username}/{Path.GetFileName(args.Transfer.Filename)}]
            // {args.Transfer.BytesTransferred}/{args.Transfer.Size} {args.Transfer.PercentComplete}% {args.Transfer.AverageSpeed}kb/s");
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

            Console.WriteLine($"[{direction}] [{user}/{file}] {oldState} => {state}{(completed ? $" ({xfer.BytesTransferred}/{xfer.Size} = {xfer.PercentComplete}%) @ {xfer.AverageSpeed.SizeSuffix()}/s" : string.Empty)}");

            if (xfer.Direction == TransferDirection.Upload && xfer.State.HasFlag(TransferStates.Completed | TransferStates.Succeeded))
            {
                _ = Client.SendUploadSpeedAsync((int)args.Transfer.AverageSpeed);
            }
        }

        private void Client_UserStatusChanged(object sender, UserStatusChangedEventArgs args)
        {
            Console.WriteLine($"[USER] {args.Username}: {args.Status}");
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
            var result = new Soulseek.Directory(directory.Replace("/", @"\"), System.IO.Directory.GetFiles(directory)
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
            var localFilename = SharedFileCache.Resolve(filename).ToLocalOSPath();

            var fileInfo = new FileInfo(localFilename);

            if (!fileInfo.Exists)
            {
                Console.WriteLine($"[UPLOAD REJECTED] File {localFilename} not found.");
                throw new DownloadEnqueueException($"File not shared.");
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
                await Client.UploadAsync(username, filename, fileInfo.Length, stream, options: topts, cancellationToken: cts.Token);
            }).ContinueWith(t =>
            {
                Console.WriteLine($"[UPLOAD FAILED] {t.Exception}");
            }, TaskContinuationOptions.NotOnRanToCompletion); // fire and forget

            // return a completed task so that the invoking code can respond to the remote client.
            return Task.CompletedTask;
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
            // some bots continually query for very common strings. blacklist known names here.
            var blacklist = new[] { "Lola45", "Lolo51", "rajah" };
            if (blacklist.Contains(username))
            {
                return null;
            }

            // some bots and perhaps users search for very short terms. only respond to queries >= 3 characters. sorry, U2 fans.
            if (query.Query.Length < 3)
            {
                return null;
            }

            var results = await SharedFileCache.SearchAsync(query);

            if (results.Any())
            {
                Console.WriteLine($"[SENDING SEARCH RESULTS]: {results.Count()} records to {username} for query {query.SearchText}");

                return new SearchResponse(
                    SoulseekClient.Username,
                    token,
                    freeUploadSlots: 1,
                    uploadSpeed: 0,
                    queueLength: 0,
                    fileList: results);
            }

            // if no results, either return null or an instance of SearchResponse with a fileList of length 0 in either case, no
            // response will be sent to the requestor.
            return null;
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
                picture: Array.Empty<byte>(),
                uploadSlots: 1,
                queueLength: 0,
                hasFreeUploadSlot: false);

            return Task.FromResult(info);
        }
    }
}