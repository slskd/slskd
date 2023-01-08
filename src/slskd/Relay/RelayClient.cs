// <copyright file="RelayClient.cs" company="slskd Team">
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

namespace slskd.Relay
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Serilog;
    using slskd.Cryptography;
    using slskd.Shares;

    /// <summary>
    ///     Relay client (agent).
    /// </summary>
    public interface IRelayClient : IDisposable
    {
        /// <summary>
        ///     Gets the client state.
        /// </summary>
        IStateMonitor<RelayClientState> StateMonitor { get; }

        /// <summary>
        ///     Starts the client and connects to the controller.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Stops the client and disconnects from the controller.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Synchronizes state with the controller.
        /// </summary>
        /// <returns>The operation context.</returns>
        Task SynchronizeAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Relay client (agent).
    /// </summary>
    public class RelayClient : IRelayClient
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RelayClient"/> class.
        /// </summary>
        /// <param name="shareService"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="httpClientFactory"></param>
        public RelayClient(
            IShareService shareService,
            IOptionsMonitor<Options> optionsMonitor,
            IHttpClientFactory httpClientFactory)
        {
            Shares = shareService;

            HttpClientFactory = httpClientFactory;

            StateMonitor = State;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            Configure(OptionsMonitor.CurrentValue);
        }

        /// <summary>
        ///     Gets the client state.
        /// </summary>
        public IStateMonitor<RelayClientState> StateMonitor { get; }

        private SemaphoreSlim ConfigurationSyncRoot { get; } = new SemaphoreSlim(1, 1);
        private bool Disposed { get; set; }
        private IHttpClientFactory HttpClientFactory { get; }
        private HubConnection HubConnection { get; set; }
        private string LastOptionsHash { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<RelayClient>();
        private bool LoggedIn { get; set; }
        private TaskCompletionSource LoggedInTaskCompletionSource { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IShareService Shares { get; }
        private CancellationTokenSource StartCancellationTokenSource { get; set; }
        private bool StartRequested { get; set; }
        private ManagedState<RelayClientState> State { get; } = new();
        private SemaphoreSlim StateSyncRoot { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Starts the client and connects to the controller.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (!StateSyncRoot.Wait(0, cancellationToken))
            {
                // we're already attempting to connect, let the existing attempt handle it
                return;
            }

            try
            {
                var mode = OptionsMonitor.CurrentValue.Relay.Mode.ToEnum<RelayMode>();

                if (mode != RelayMode.Agent && mode != RelayMode.Debug)
                {
                    throw new InvalidOperationException($"Relay client can only be started when operation mode is {RelayMode.Agent}");
                }

                StartCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                LoggedInTaskCompletionSource = new TaskCompletionSource();
                StartRequested = true;

                Log.Information("Attempting to connect to the relay controller {Address}", OptionsMonitor.CurrentValue.Relay.Controller.Address);

                State.SetValue(_ => TranslateState(HubConnectionState.Connecting));

                // retry indefinitely
                await Retry.Do(
                    task: async () =>
                    {
                        await HubConnection.StartAsync(StartCancellationTokenSource.Token);

                        State.SetValue(_ => TranslateState(HubConnection.State));
                        Log.Information("Relay controller connection established. Awaiting authentication...");

                        await LoggedInTaskCompletionSource.Task;

                        LoggedIn = true;

                        Log.Information("Authenticated. Uploading shares...");

                        try
                        {
                            await UploadSharesAsync();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, ex.Message);
                            Log.Error("Disconnecting from the relay controller");
                            await StopAsync();
                            throw;
                        }

                        Log.Information("Shares uploaded. Ready to upload files.");
                    },
                    isRetryable: (attempts, ex) => true,
                    onFailure: (attempts, ex) =>
                    {
                        Log.Debug(ex, "Relay hub connection failure");
                        Log.Warning("Failed attempt #{Attempts} to connect to relay controller: {Message}", attempts, ex.Message);
                    },
                    maxAttempts: int.MaxValue,
                    maxDelayInMilliseconds: 30000,
                    StartCancellationTokenSource.Token);
            }
            finally
            {
                StateSyncRoot.Release();
            }
        }

        /// <summary>
        ///     Stops the client and disconnects from the controller.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            StartRequested = false;

            if (HubConnection != null)
            {
                await HubConnection.StopAsync(cancellationToken);

                LoggedIn = false;

                State.SetValue(_ => TranslateState(HubConnectionState.Disconnected));

                Log.Information("Relay controller connection disconnected");
            }
        }

        /// <summary>
        ///     Synchronizes state with the controller.
        /// </summary>
        /// <returns>The operation context.</returns>
        public Task SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return UploadSharesAsync(cancellationToken);
        }

        /// <summary>
        ///     Disposes this instance.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    _ = HubConnection?.DisposeAsync();
                }

                Disposed = true;
            }
        }

        private string ComputeCredential(string token)
        {
            var options = OptionsMonitor.CurrentValue;

            var key = Pbkdf2.GetKey(options.Relay.Controller.Secret, options.InstanceName, 48);
            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);

            return Aes.Encrypt(tokenBytes, key).ToBase62();
        }

        private string ComputeCredential(Guid token) => ComputeCredential(token.ToString());

        private void Configure(Options options)
        {
            var mode = options.Relay.Mode.ToEnum<RelayMode>();

            if (mode != RelayMode.Agent && mode != RelayMode.Debug)
            {
                return;
            }

            ConfigurationSyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(options.Relay.Controller.ToJson());

                if (optionsHash == LastOptionsHash)
                {
                    return;
                }

                Log.Debug("Relay options changed. Reconfiguring...");

                // if the client is attempting a connection, cancel it it's going to be dropped when we create a new instance, but
                // we need the retry loop around connection to stop.
                StartCancellationTokenSource?.Cancel();

                HubConnection = new HubConnectionBuilder()
                    .WithUrl($"{options.Relay.Controller.Address}/hub/relay", builder =>
                    {
                        builder.AccessTokenProvider = () => Task.FromResult(options.Relay.Controller.ApiKey);
                        builder.HttpMessageHandlerFactory = (message) =>
                        {
                            if (message is HttpClientHandler clientHandler && options.Relay.Controller.IgnoreCertificateErrors)
                            {
                                clientHandler.ServerCertificateCustomValidationCallback += (_, _, _, _) => true;
                            }

                            return message;
                        };
                    })
                    .WithAutomaticReconnect(new ControllerRetryPolicy(0, 1, 3, 10, 30, 60))
                    .Build();

                HubConnection.Reconnected += HubConnection_Reconnected;
                HubConnection.Reconnecting += HubConnection_Reconnecting;
                HubConnection.Closed += HubConnection_Closed;

                HubConnection.On<string, long, Guid>(nameof(IRelayHub.RequestFileUpload), HandleFileUploadRequest);
                HubConnection.On<string, Guid>(nameof(IRelayHub.RequestFileInfo), HandleFileInfoRequest);
                HubConnection.On<string>(nameof(IRelayHub.Challenge), HandleAuthenticationChallenge);
                HubConnection.On<string, Guid>(nameof(IRelayHub.NotifyFileDownloadCompleted), HandleNotifyFileDownloadCompleted);

                LastOptionsHash = optionsHash;

                Log.Debug("Relay options reconfigured");

                // if start was requested (if StartAsync() was called externally), restart after re-configuration
                if (StartRequested)
                {
                    Log.Information("Reconnecting the relay controller connection...");
                    _ = StartAsync();
                }
            }
            finally
            {
                ConfigurationSyncRoot.Release();
            }
        }

        private HttpClient CreateHttpClient()
        {
            var options = OptionsMonitor.CurrentValue.Relay.Controller;
            HttpClient client;

            if (options.IgnoreCertificateErrors)
            {
                client = new HttpClient(new HttpClientHandler()
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                });
            }
            else
            {
                client = new HttpClient();
            }

            client.Timeout = TimeSpan.FromMilliseconds(int.MaxValue);
            client.BaseAddress = new(options.Address);
            return client;
        }

        private async Task HandleAuthenticationChallenge(string challengeToken)
        {
            try
            {
                Log.Information("Relay controller sent an authentication challenge");

                var options = OptionsMonitor.CurrentValue;

                var agent = options.InstanceName;
                var response = ComputeCredential(challengeToken);

                Log.Information("Logging in...");

                await HubConnection.InvokeAsync(nameof(RelayHub.Login), agent, response);

                LoggedIn = true;
                LoggedInTaskCompletionSource.TrySetResult();

                Log.Information("Login succeeded.");
            }
            catch (UnauthorizedAccessException)
            {
                await HubConnection.StopAsync();
                Log.Error("Relay controller authentication failed. Check configuration.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle authentication challenge: {Message}", ex.Message);
            }
        }

        private async Task HandleFileInfoRequest(string filename, Guid id)
        {
            Log.Information("Relay controller requested file info for {Filename} with ID {Id}", filename, id);

            try
            {
                var (_, localFilename) = await Shares.ResolveFileAsync(filename);

                var localFileInfo = new FileInfo(localFilename);

                await HubConnection.InvokeAsync(nameof(RelayHub.ReturnFileInfo), id, localFileInfo.Exists, localFileInfo.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle file info request: {Message}", ex.Message);
                await HubConnection.InvokeAsync(nameof(RelayHub.ReturnFileInfo), id, false, 0);
            }
        }

        private Task HandleFileUploadRequest(string filename, long startOffset, Guid token)
        {
            _ = Task.Run(async () =>
            {
                Log.Information("Relay controller requested file {Filename} with ID {Id}", filename, token);

                try
                {
                    var (_, localFilename) = await Shares.ResolveFileAsync(filename);

                    var localFileInfo = new FileInfo(localFilename);

                    if (!localFileInfo.Exists)
                    {
                        Shares.RequestScan();
                        throw new NotFoundException($"The file '{localFilename}' could not be located on disk. A share scan should be performed.");
                    }

                    using var stream = new FileStream(localFileInfo.FullName, FileMode.Open, FileAccess.Read);

                    stream.Seek(startOffset, SeekOrigin.Begin);

                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/relay/files/{token}");

                    request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Relay.Controller.ApiKey);
                    request.Headers.Add("X-Relay-Agent", OptionsMonitor.CurrentValue.InstanceName);
                    request.Headers.Add("X-Relay-Credential", ComputeCredential(token));

                    using var content = new MultipartFormDataContent
                    {
                        { new StreamContent(stream), "file", filename },
                    };

                    request.Content = content;

                    Log.Information("Beginning upload of file {Filename} with ID {Id}", filename, token);
                    var response = await CreateHttpClient().SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error("Upload of file {Filename} with ID {Id} failed: {StatusCode}", filename, token, response.StatusCode);
                    }
                    else
                    {
                        Log.Information("Upload of file {Filename} with ID {Id} succeeded.", filename, token);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to handle file request: {Message}", ex.Message);

                    // report the failure to the controller. this avoids a failure due to timeout.
                    await HubConnection.InvokeAsync(nameof(RelayHub.NotifyFileUploadFailed), token);
                }
            });

            return Task.CompletedTask;
        }

        private Task HandleNotifyFileDownloadCompleted(string filename, Guid token)
        {
            if (!OptionsMonitor.CurrentValue.Relay.Controller.Downloads)
            {
                return Task.CompletedTask;
            }

            Log.Information("Relay controller sent a download notification for {Filename} ({Token})", filename, token);

            _ = Task.Run(async () =>
            {
                var destinationFile = Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, filename);

                if (OptionsMonitor.CurrentValue.Relay.Mode.ToEnum<RelayMode>() == RelayMode.Debug)
                {
                    // if we're debugging, we're referencing the same file for both the controller and agent which will lead to an
                    // access violation. prefix the destination file to avoid this.
                    destinationFile = Path.Combine(OptionsMonitor.CurrentValue.Directories.Downloads, $"{filename}.relayed");
                }

                // if the controller is Windows and the agent is Linux or vice versa, we need to translate the filename to the
                // local OS or we're going to get funny results when we go to write the file
                destinationFile = destinationFile.LocalizePath();

                await Retry.Do(task: async () =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v0/relay/downloads/{token}");

                    request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Relay.Controller.ApiKey);
                    request.Headers.Add("X-Relay-Agent", OptionsMonitor.CurrentValue.InstanceName);
                    request.Headers.Add("X-Relay-Credential", ComputeCredential(token));
                    request.Headers.Add("X-Relay-Filename", filename);

                    using var client = CreateHttpClient();
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    response.EnsureSuccessStatusCode();

                    using var remoteStream = await response.Content.ReadAsStreamAsync();

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                    using var localStream = new FileStream(destinationFile, FileMode.Create);
                    await remoteStream.CopyToAsync(localStream);
                },
                isRetryable: (_, _) => true,
                onFailure: (_, ex) => Log.Error(ex, "Failed to handle file download notification for {Filename} ({Token})", filename, token),
                maxAttempts: 3,
                maxDelayInMilliseconds: 60000);

                Log.Information("File {Filename} successfully downloaded to {Destination}", filename, destinationFile);
            });

            return Task.CompletedTask;
        }

        private Task HubConnection_Closed(Exception arg)
        {
            Log.Warning("Relay controller connection closed: {Message}", arg.Message);
            LoggedIn = false;
            State.SetValue(_ => TranslateState(HubConnection.State));
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string arg)
        {
            // upon reconnection, the authentication flow is started again.
            // there's nothing that needs to be done upon reconnection, only log.
            Log.Warning("Relay controller connection reconnected");
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnecting(Exception arg)
        {
            Log.Warning("Relay controller connection reconnecting: {Message}", arg.Message);
            LoggedIn = false;
            State.SetValue(_ => TranslateState(HubConnection.State));
            return Task.CompletedTask;
        }

        private RelayClientState TranslateState(HubConnectionState hub) => hub switch
        {
            HubConnectionState.Disconnected => RelayClientState.Disconnected,
            HubConnectionState.Connected => RelayClientState.Connected,
            HubConnectionState.Connecting => RelayClientState.Connecting,
            HubConnectionState.Reconnecting => RelayClientState.Reconnecting,
            _ => throw new ArgumentException($"Unexpected HubConnectionState {hub}"),
        };

        private async Task UploadSharesAsync(CancellationToken cancellationToken = default)
        {
            if (!LoggedIn)
            {
                return;
            }

            var temp = Path.Combine(Path.GetTempPath(), Program.AppName, $"share_backup_{Path.GetRandomFileName()}.db");

            try
            {
                Log.Debug("Backing up shares to {Filename}", temp);

                Directory.CreateDirectory(Path.GetDirectoryName(temp));

                await Shares.DumpAsync(temp);

                Log.Debug("Share backup successful");
                Log.Debug("Requesting share upload token...");

                Guid token = Guid.Empty;

                // retry this a few times. it can throw if a race condition materializes
                // between this logic and the authentication flow, and it'll almost certainly be
                // worked out properly if we just wait a second and try again
                await Retry.Do(task: async () =>
                    {
                        token = await HubConnection.InvokeAsync<Guid>(nameof(RelayHub.BeginShareUpload));
                    },
                    isRetryable: (_, _) => true,
                    onFailure: (count, ex) => Log.Warning("Failed attempt #{Attempts} to obtain share upload token: {Message}", count, ex.Message),
                    maxAttempts: 3,
                    maxDelayInMilliseconds: 5000,
                    cancellationToken);

                Log.Debug("Share upload token {Token}", token);

                var stream = new FileStream(temp, FileMode.Open, FileAccess.Read);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/relay/shares/{token}");

                request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Relay.Controller.ApiKey);
                request.Headers.Add("X-Relay-Agent", OptionsMonitor.CurrentValue.InstanceName);
                request.Headers.Add("X-Relay-Credential", ComputeCredential(token));

                using var content = new MultipartFormDataContent
                {
                    { new StringContent(Shares.LocalHost.Shares.ToJson()), "shares" },
                    { new StreamContent(stream), "database", "shares" },
                };

                request.Content = content;

                var size = ((double)stream.Length).SizeSuffix();
                var sw = new Stopwatch();
                sw.Start();

                Log.Information("Beginning upload of shares ({Size})", size);
                Log.Debug("Shares: {Shares}", Shares.LocalHost.Shares.ToJson());
                var response = await CreateHttpClient().SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new RelayException($"Failed to upload shares to relay controller: {response.StatusCode}");
                }

                sw.Stop();
                Log.Information("Upload of shares succeeded ({Size} in {Duration}ms)", size, sw.ElapsedMilliseconds);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private sealed class ControllerRetryPolicy : IRetryPolicy
        {
            public ControllerRetryPolicy(params int[] intervals)
            {
                Intervals = intervals;
            }

            private int[] Intervals { get; set; }

            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                return TimeSpan.FromSeconds(Intervals[Math.Min(retryContext.PreviousRetryCount, Intervals.Length - 1)]);
            }
        }
    }
}