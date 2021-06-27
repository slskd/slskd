// <copyright file="FTPService.cs" company="slskd Team">
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

namespace slskd.Integrations.FTP
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using FluentFTP;
    using Microsoft.Extensions.Logging;
    using static slskd.Options.IntegrationOptions;

    public class FTPClient : IFTPClient
    {
        public FTPClient(
            IFTPClientFactory ftpClientFactory,
            Microsoft.Extensions.Options.IOptionsMonitor<Options> optionsMonitor,
            ILogger<FTPClient> log)
        {
            Factory = ftpClientFactory;
            Options = optionsMonitor.CurrentValue;
            Log = log;

            try
            {
                EncryptionMode = (FtpEncryptionMode)Enum.Parse(typeof(FtpEncryptionMode), FTPOptions.EncryptionMode, ignoreCase: true);
            }
            catch (Exception ex)
            {
                // Options should validate that the given string is parsable to FtpEncryptionMode through EnumAttribute; if this throws there's a bug somewhere.
                throw new ArgumentException($"Failed to parse {typeof(FtpEncryptionMode).Name} from application Options. This is most likely a programming error; please file a GitHub issue and include your FTP configuration.", ex);
            }
        }

        public bool Enabled => !string.IsNullOrEmpty(Options.Integration.FTP.Address);

        private Options Options { get; }
        private FTPOptions FTPOptions => Options.Integration.FTP;
        private ILogger<FTPClient> Log { get; set; }
        private IFTPClientFactory Factory { get; set; }
        private FtpEncryptionMode EncryptionMode { get; set; }

        public FtpClient CreateFtpClient()
        {
            var client = new FtpClient(FTPOptions.Address, FTPOptions.Port, FTPOptions.Username, FTPOptions.Password);
            client.EncryptionMode = EncryptionMode;

            return client;
        }

        public async Task UploadAsync(string filename)
        {
            if (!Enabled)
            {
                Log.LogDebug("Skipping FTP upload of {filename}; FTP integration is disabled");
                return;
            }

            // todo: add retries up to configured count with exponential backoff
            try
            {
                var fileOnly = Path.GetFileName(filename);
                var fileAndParentDirectory = Path.Combine(Path.GetDirectoryName(filename).Replace(Path.GetDirectoryName(Path.GetDirectoryName(filename)), string.Empty), fileOnly).TrimStart('/').TrimStart('\\');

                var remotePath = FTPOptions.RemotePath.TrimEnd('/').TrimEnd('\\');
                // todo: sanitize filename
                var remoteFilename = $"{remotePath}/{fileAndParentDirectory}";

                var existsMode = FTPOptions.OverwriteExisting ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip;

                Log.LogInformation("Uploading {Filename} to FTP {Address}:{Port} as {RemoteFilename}", fileAndParentDirectory, FTPOptions.Address, FTPOptions.Port, remoteFilename);
                var client = Factory.CreateFtpClient();

                // todo: add cancellation token for timeout
                await client.ConnectAsync();

                var status = await client.UploadFileAsync(filename, remoteFilename, existsMode, createRemoteDir: true);

                if (status == FtpStatus.Failed)
                {
                    throw new FtpException("FTP client reported a failed transfer");
                }

                Log.LogInformation("FTP upload of {Filename} to {Address}:{Port} complete", fileAndParentDirectory, FTPOptions.Address, FTPOptions.Port);
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, "Failed to upload {Filename} to FTP: {Message}", ex.Message);
                throw;
            }
        }
    }
}
