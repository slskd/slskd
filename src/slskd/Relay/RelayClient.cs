// <copyright file="NetworkClient.cs" company="slskd Team">
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

namespace slskd.Network
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Serilog;
    using slskd.Cryptography;
    using slskd.Shares;

    /// <summary>
    ///     Network client (agent).
    /// </summary>
    public interface INetworkClient : IDisposable
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
    ///     Network client (agent).
    /// </summary>
    public class RelayClient : INetworkClient
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

        private ManagedState<RelayClientState> State { get; } = new();
        private IShareService Shares { get; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private string LastOptionsHash { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHttpClientFactory HttpClientFactory { get; }
        private HubConnection HubConnection { get; set; }
        private bool StartRequested { get; set; }
        private CancellationTokenSource StartCancellationTokenSource { get; set; }
        private ILogger Log { get; } = Serilog.Log.ForContext<RelayClient>();
        private bool LoggedIn { get; set; }
        private TaskCompletionSource LoggedInTaskCompletionSource { get; set; }
        private bool Disposed { get; set; }

        /// <summary>
        ///     Starts the client and connects to the controller.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            var mode = OptionsMonitor.CurrentValue.Network.Mode.ToEnum<OperationMode>();

            if (mode != OperationMode.Agent && mode != OperationMode.Debug)
            {
                throw new InvalidOperationException($"Network client can only be started when operation mode is {OperationMode.Agent}");
            }

            StartCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            LoggedInTaskCompletionSource = new TaskCompletionSource();
            StartRequested = true;

            Log.Information("Attempting to connect to the network controller {Address}", OptionsMonitor.CurrentValue.Network.Controller.Address);

            State.SetValue(_ => TranslateState(HubConnectionState.Connecting));

            // retry indefinitely
            await Retry.Do(
                task: () => HubConnection.StartAsync(StartCancellationTokenSource.Token),
                isRetryable: (attempts, ex) => true,
                onFailure: (attempts, ex) =>
                {
                    Log.Debug(ex, "Network hub connection failure");
                    Log.Warning("Failed attempt #{Attempts} to connect to network controller: {Message}", attempts, ex.Message);
                },
                maxAttempts: int.MaxValue,
                maxDelayInMilliseconds: 30000,
                StartCancellationTokenSource.Token);

            State.SetValue(_ => TranslateState(HubConnection.State));
            Log.Information("Network controller connection established. Awaiting authentication...");

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
                Log.Error("Disconnecting from the network controller");
                await StopAsync();
                throw;
            }

            Log.Information("Shares uploaded. Ready to upload files.");
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

                Log.Information("Network controller connection disconnected");
            }
        }

        /// <summary>
        ///     Synchronizes state with the controller.
        /// </summary>
        /// <returns>The operation context.</returns>
        public Task SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            return UploadSharesAsync();
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

            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Program.AppName));

            try
            {
                Log.Debug("Backing up shares to {Filename}", temp);
                await Shares.DumpAsync(temp);
                Log.Debug("Share backup successful");

                Log.Debug("Requesting share upload token...");
                var token = await HubConnection.InvokeAsync<Guid>(nameof(RelayHub.BeginShareUpload));
                Log.Debug("Share upload token {Token}", token);

                var stream = new FileStream(temp, FileMode.Open, FileAccess.Read);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/network/shares/{token}");
                using var content = new MultipartFormDataContent
                {
                    { new StringContent(OptionsMonitor.CurrentValue.InstanceName), "name" },
                    { new StringContent(ComputeCredential(token)), "credential" },
                    { new StringContent(Shares.LocalHost.Shares.ToJson()), "shares" },
                    { new StreamContent(stream), "database", "shares" },
                };

                request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Network.Controller.ApiKey);
                request.Content = content;

                Log.Information("Beginning upload of shares");
                var response = await CreateHttpClient().SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new NetworkException($"Failed to upload shares to network controller: {response.StatusCode}");
                }

                Log.Information("Upload shares succeeded");
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private string ComputeCredential(string token)
        {
            var options = OptionsMonitor.CurrentValue;

            var key = Pbkdf2.GetKey(options.Network.Controller.Secret, options.InstanceName, 48);
            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);

            return Aes.Encrypt(tokenBytes, key).ToBase62();
        }

        private string ComputeCredential(Guid token) => ComputeCredential(token.ToString());

        private Task HandleFileUploadRequest(string filename, Guid token)
        {
            _ = Task.Run(async () =>
            {
                Log.Information("Network controller requested file {Filename} with ID {Id}", filename, token);

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
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/network/files/{token}");
                    using var content = new MultipartFormDataContent
                    {
                        { new StringContent(OptionsMonitor.CurrentValue.InstanceName), "name" },
                        { new StringContent(ComputeCredential(token)), "credential" },
                        { new StreamContent(stream), "file", filename },
                    };

                    request.Headers.Add("X-API-Key", OptionsMonitor.CurrentValue.Network.Controller.ApiKey);
                    request.Content = content;

                    Log.Information("Beginning upload of file {Filename} with ID {Id}", filename, token);
                    var response = await CreateHttpClient().SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error("Upload of file {Filename} with ID {Id} failed: {StatusCode}", response.StatusCode);
                    }
                    else
                    {
                        Log.Information("Upload of file {Filename} with ID {Id} succeeded.", filename);
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

        private async Task HandleFileInfoRequest(string filename, Guid id)
        {
            Log.Information("Network controller requested file info for {Filename} with ID {Id}", filename, id);

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

        private async Task HandleAuthenticationChallenge(string challengeToken)
        {
            try
            {
                Log.Debug("Network controller sent an authentication challenge");

                var options = OptionsMonitor.CurrentValue;

                var agent = options.InstanceName;
                var response = ComputeCredential(challengeToken);

                Log.Debug("Logging in...");
                await HubConnection.InvokeAsync(nameof(RelayHub.Login), agent, response);
                Log.Debug("Login succeeded.");
                LoggedInTaskCompletionSource?.TrySetResult();
            }
            catch (UnauthorizedAccessException)
            {
                await HubConnection.StopAsync();
                Log.Error("Network controller authentication failed. Check configuration.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle authentication challenge: {Message}", ex.Message);
            }
        }

        private Task HubConnection_Closed(Exception arg)
        {
            Log.Warning("Network controller connection closed: {Message}", arg.Message);
            LoggedIn = false;
            State.SetValue(_ => TranslateState(HubConnection.State));
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnecting(Exception arg)
        {
            Log.Warning("Network controller connection reconnecting: {Message}", arg.Message);
            LoggedIn = false;
            State.SetValue(_ => TranslateState(HubConnection.State));
            return Task.CompletedTask;
        }

        private async Task HubConnection_Reconnected(string arg)
        {
            Log.Warning("Network controller connection reconnected");
            // todo: does this need to log in again? does it retain the same connection id?
            LoggedIn = true;
            State.SetValue(_ => TranslateState(HubConnection.State));
            await UploadSharesAsync();
        }

        private HttpClient CreateHttpClient()
        {
            var options = OptionsMonitor.CurrentValue.Network.Controller;
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

        private void Configure(Options options)
        {
            var mode = options.Network.Mode.ToEnum<OperationMode>();

            if (mode != OperationMode.Agent && mode != OperationMode.Debug)
            {
                return;
            }

            SyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(options.Network.Controller.ToJson());

                if (optionsHash == LastOptionsHash)
                {
                    return;
                }

                Log.Debug("Network options changed. Reconfiguring...");

                // if the client is attempting a connection, cancel it
                // it's going to be dropped when we create a new instance, but we need
                // the retry loop around connection to stop.
                StartCancellationTokenSource?.Cancel();

                HubConnection = new HubConnectionBuilder()
                    .WithUrl($"{options.Network.Controller.Address}/hub/agents", builder =>
                    {
                        builder.AccessTokenProvider = () => Task.FromResult(options.Network.Controller.ApiKey);
                        builder.HttpMessageHandlerFactory = (message) =>
                        {
                            if (message is HttpClientHandler clientHandler && options.Network.Controller.IgnoreCertificateErrors)
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

                HubConnection.On<string, Guid>(nameof(INetworkHub.RequestFileUpload), HandleFileUploadRequest);
                HubConnection.On<string, Guid>(nameof(INetworkHub.RequestFileInfo), HandleFileInfoRequest);
                HubConnection.On<string>(nameof(INetworkHub.Challenge), HandleAuthenticationChallenge);

                LastOptionsHash = optionsHash;

                Log.Debug("Network options reconfigured");

                // if start was requested (if StartAsync() was called externally), restart
                // after re-configuration
                if (StartRequested)
                {
                    Log.Information("Reconnecting the network controller connection...");
                    _ = StartAsync();
                }
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private class ControllerRetryPolicy : IRetryPolicy
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

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
