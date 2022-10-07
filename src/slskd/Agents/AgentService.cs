// <copyright file="AgentService.cs" company="slskd Team">
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

namespace slskd.Agents
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.AspNetCore.SignalR.Client;
    using Serilog;

    public interface IAgentService
    {
        /// <summary>
        ///     Gets the collection of pending Agent uploads.
        /// </summary>
        ConcurrentDictionary<Guid, (TaskCompletionSource<Stream> Upload, TaskCompletionSource Completion)> PendingUploads { get; }

        /// <summary>
        ///     Retrieves an upload of the specified <paramref name="filename"/> from the specified <paramref name="agent"/>.
        /// </summary>
        /// <param name="agent">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        Task<(Stream Stream, TaskCompletionSource Completion)> GetUpload(string agent, string filename, int timeout = 3000);
    }

    public class AgentService : IAgentService
    {
        public AgentService(
            IOptionsMonitor<Options> optionsMonitor,
            IHttpClientFactory httpClientFactory,
            IHubContext<AgentHub> agentHub)
        {
            AgentHub = agentHub;
            HttpClientFactory = httpClientFactory;

            OptionsMonitor = optionsMonitor;
            OptionsMonitor.OnChange(options => Configure(options));

            Configure(OptionsMonitor.CurrentValue);
        }

        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHttpClientFactory HttpClientFactory { get; }
        private HttpClient HttpClient { get; set; }
        private HubConnection HubConnection { get; set; }
        private IHubContext<AgentHub> AgentHub { get; set; }
        private string LastOptionsHash { get; set; }
        private SemaphoreSlim SyncRoot { get; } = new SemaphoreSlim(1, 1);
        private ILogger Log { get; } = Serilog.Log.ForContext<AgentService>();

        /// <summary>
        ///     Gets the collection of pending Agent uploads.
        /// </summary>
        public ConcurrentDictionary<Guid, (TaskCompletionSource<Stream> Upload, TaskCompletionSource Completion)> PendingUploads { get; } = new();

        /// <summary>
        ///     Retrieves an upload of the specified <paramref name="filename"/> from the specified <paramref name="agent"/>.
        /// </summary>
        /// <param name="agent">The agent from which to retrieve the file.</param>
        /// <param name="filename">The file to retrieve.</param>
        /// <param name="timeout">An optional timeout value.</param>
        /// <returns>The operation context, including a stream containing the requested file, and an upload TaskCompletionSource.</returns>
        public async Task<(Stream Stream, TaskCompletionSource Completion)> GetUpload(string agent, string filename, int timeout = 3000)
        {
            var id = Guid.NewGuid();

            // create a TCS for the upload stream. this is awaited below and completed in
            // the API controller when the agent POSTs the file
            var upload = new TaskCompletionSource<Stream>();

            // create a TCS for the upload itself. this is awaited by the API controller
            // and completed by the transfer service when the upload to the remote user is complete
            // the API controller needs to wait until the remote transfer is complete in order to
            // keep the stream open for the duration
            var completion = new TaskCompletionSource();

            PendingUploads.TryAdd(id, (upload, completion));

            await AgentHub.RequestFileAsync(agent, filename, id);
            Log.Information("Requested file {Filename} from Agent {Agent} with ID {Id}. Waiting for incoming connection.", filename, agent, id);

            var task = await Task.WhenAny(upload.Task, Task.Delay(timeout));

            if (task == upload.Task)
            {
                var stream = await upload.Task;
                return (stream, completion);
            }
            else
            {
                throw new TimeoutException($"Timed out attempting to retrieve the file {filename} from agent {agent}");
            }
        }

        private async Task HandleFileRequest(Guid id, string filename)
        {
            Log.Information("Controller requested file {Filename} with ID {Id}", filename, id);

            try
            {
                using var stream = new FileStream(Path.Combine(@"C:\slsk-downloads", "1.mp3"), FileMode.Open, FileAccess.Read);
                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v0/agents/files/{id}");
                using var content = new MultipartFormDataContent
                {
                    { new StreamContent(stream), "file", filename },
                };

                request.Content = content;

                Log.Information("Beginning upload of file {Filename} with ID {Id}", filename, id);
                var response = await HttpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Log.Error("Upload of file {Filename} with ID {Id} failed: {StatusCode}", response.StatusCode);
                    Console.WriteLine("Failed");
                    Console.WriteLine(response.StatusCode);
                }
                else
                {
                    Console.WriteLine($"{filename} sent!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void Configure(Options options)
        {
            SyncRoot.Wait();

            try
            {
                var optionsHash = Compute.Sha1Hash(string.Join(';', options.Shares.Directories));

                if (optionsHash == LastOptionsHash)
                {
                    return;
                }

                var mode = options.Network.OperationMode.ToEnum<NetworkOperationMode>();

                //if (mode == NetworkOperationMode.Agent
                //{
                HttpClient = HttpClientFactory.CreateClient();
                HttpClient.BaseAddress = new(options.Network.Controller.Address);

                Console.WriteLine("-------------------------- building hub ------------------------");
                HubConnection = new HubConnectionBuilder()
                    .WithUrl($"{options.Network.Controller.Address}/hub/agents")
                    .WithAutomaticReconnect()
                    .Build();

                HubConnection.On<Guid, string>(AgentHubMethods.RequestFile, HandleFileRequest);
                HubConnection.Reconnected += HubConnection_Reconnected;
                HubConnection.Reconnecting += HubConnection_Reconnecting;
                HubConnection.Closed += HubConnection_Closed;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1000);
                        Console.WriteLine("------------------- connecting hub -------------------");
                        await HubConnection.StartAsync();
                        Log.Information("Controller connection established");
                        Console.WriteLine("---------------------------- hub connected ----------------------");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
                //}

                LastOptionsHash = optionsHash;
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        private Task HubConnection_Closed(Exception arg)
        {
            Log.Warning("Controller connection closed: {Message}", arg.Message);
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnecting(Exception arg)
        {
            Log.Warning("Controller connection reconnecting: {Message}", arg.Message);
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string arg)
        {
            Log.Warning("Controller connection reconnected");
            return Task.CompletedTask;
        }
    }
}
