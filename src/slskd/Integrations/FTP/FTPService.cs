// <copyright file="FTPService.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Integrations.FTP
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentFTP;
    using FluentFTP.Exceptions;
    using Microsoft.Extensions.Logging;
    using static slskd.Options.IntegrationsOptions;

    /// <summary>
    ///     FTP Integration service.
    /// </summary>
    public class FTPService : IFTPService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="FTPService"/> class.
        /// </summary>
        /// <param name="ftpClientFactory">The FTP client factory to use.</param>
        /// <param name="optionsMonitor">The options monitor used to derive application options.</param>
        /// <param name="log">The logger.</param>
        public FTPService(
            IFTPClientFactory ftpClientFactory,
            IOptionsMonitor<Options> optionsMonitor,
            ILogger<FTPService> log)
        {
            Factory = ftpClientFactory;
            OptionsMonitor = optionsMonitor;
            Log = log;
        }

        private IFTPClientFactory Factory { get; set; }
        private FtpOptions FtpOptions => OptionsMonitor.CurrentValue.Integrations.Ftp;
        private ILogger<FTPService> Log { get; set; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }

        /// <summary>
        ///     Uploads the specified <paramref name="filename"/> to the configured FTP server.
        /// </summary>
        /// <param name="filename">The fully qualified name of the file to upload.</param>
        /// <returns>The operation context.</returns>
        public async Task UploadAsync(string filename)
        {
            if (!FtpOptions.Enabled)
            {
                Log.LogDebug("Skipping FTP upload of {filename}; FTP integration is disabled", filename);
                return;
            }

            var fileAndParentDirectory = GetFileAndParentDirectoryFromFilename(filename);

            try
            {
                await Retry.Do(
                    task: () => AttemptUploadAsync(filename),
                    isRetryable: (attempts, ex) => true,
                    onFailure: (attempts, ex) => Log.LogInformation("Failed attempt #{Attempts} to upload {Filename} to FTP: {Message}", attempts, fileAndParentDirectory, ex.Message),
                    maxAttempts: FtpOptions.RetryAttempts,
                    baseDelayInMilliseconds: 1000,
                    maxDelayInMilliseconds: 30000);
            }
            catch (RetryException ex)
            {
                Log.LogError(ex, "Fatal error retrying upload of {Filename} to FTP: {Message}", fileAndParentDirectory, ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to upload {Filename} to FTP after {Attempts} attempts: {Message}", fileAndParentDirectory, FtpOptions.RetryAttempts, ex.Message);
                throw;
            }
        }

        private async Task AttemptUploadAsync(string filename)
        {
            var fileAndParentDirectory = GetFileAndParentDirectoryFromFilename(filename);
            var remotePath = FtpOptions.RemotePath.TrimEnd('/').TrimEnd('\\');
            var remoteFilename = $"{remotePath}/{fileAndParentDirectory}";

            var existsMode = FtpOptions.OverwriteExisting ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip;

            using var client = Factory.CreateFtpClient();

            var timeoutTaskCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(FtpOptions.ConnectionTimeout));
            using var timeoutCancellationTokenRegistration =
                timeoutCancellationTokenSource.Token.Register(() => timeoutTaskCompletionSource.TrySetResult(true));

            Log.LogDebug("Connecting to FTP at {Address}:{Port} using encryption mode {EncryptionMode}", client.Host, client.Port, client.Config.EncryptionMode);

            var connectTask = client.Connect();
            var completedTask = await Task.WhenAny(connectTask, timeoutTaskCompletionSource.Task);

            if (completedTask == timeoutTaskCompletionSource.Task)
            {
                throw new TimeoutException($"Failed to connect to server after {FtpOptions.ConnectionTimeout}ms");
            }

            if (connectTask.Exception != null)
            {
                throw connectTask.Exception;
            }

            Log.LogInformation("Uploading {Filename} to FTP {Address}:{Port} as {RemoteFilename}", fileAndParentDirectory, FtpOptions.Address, FtpOptions.Port, remoteFilename);
            var status = await client.UploadFile(filename, remoteFilename, existsMode, createRemoteDir: true);

            if (status == FtpStatus.Failed)
            {
                throw new FtpException("FTP client reported a failed transfer");
            }

            Log.LogInformation("FTP upload of {Filename} to {Address}:{Port} complete", fileAndParentDirectory, FtpOptions.Address, FtpOptions.Port);
        }

        private string GetFileAndParentDirectoryFromFilename(string filename)
        {
            var fileOnly = Path.GetFileName(filename);
            return Path.Combine(Path.GetDirectoryName(filename).Replace(Path.GetDirectoryName(Path.GetDirectoryName(filename)), string.Empty), fileOnly).TrimStart('/').TrimStart('\\');
        }
    }
}