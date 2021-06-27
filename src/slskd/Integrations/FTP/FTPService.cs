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
    using System.Threading.Tasks;
    using FluentFTP;
    using Microsoft.Extensions.Logging;
    using static slskd.Options.IntegrationOptions;

    public class FTPService : IFTPService
    {
        public FTPService(
            Microsoft.Extensions.Options.IOptionsMonitor<Options> optionsMonitor,
            ILogger<FTPService> log)
        {
            Options = optionsMonitor.CurrentValue;
            Log = log;

            FtpEncryptionMode encryptionMode;

            try
            {
                encryptionMode = (FtpEncryptionMode)Enum.Parse(typeof(FtpEncryptionMode), FTPOptions.EncryptionMode, ignoreCase: true);
            }
            catch (Exception ex)
            {
                // Options should validate that the given string is parsable to FtpEncryptionMode through EnumAttribute; if this throws there's a bug somewhere.
                throw new ArgumentException($"Failed to parse {typeof(FtpEncryptionMode).Name} from application Options. This is most likely a programming error; please file a GitHub issue and include your FTP configuration.", ex);
            }

            Client = new FtpClient(FTPOptions.Address, FTPOptions.Port, FTPOptions.Username, FTPOptions.Password);
            Client.EncryptionMode = encryptionMode;
        }

        private Options Options { get; }
        private FTPOptions FTPOptions => Options.Integration.FTP;
        private ILogger<FTPService> Log { get; set; }
        private FtpClient Client { get; set; }

        public Task UploadAsync(string filename)
        {
            throw new NotImplementedException();
        }
    }
}
