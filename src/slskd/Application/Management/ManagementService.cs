// <copyright file="ManagementService.cs" company="slskd Team">
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

namespace slskd.Management
{
    using System.Threading.Tasks;
    using Soulseek;

    public class ManagementService : IManagementService
    {
        public ManagementService(
            Microsoft.Extensions.Options.IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient)
        {
            Options = optionsMonitor.CurrentValue;
            Client = soulseekClient;
        }

        private ISoulseekClient Client { get; }
        private Options Options { get; }

        public void DisconnectServer(string message = null)
            => Client.Disconnect(message, new IntentionalDisconnectException(message));

        public Task ConnectServerAsync()
            => Client.ConnectAsync(Options.Soulseek.Username, Options.Soulseek.Password);

        public ServerState GetServerState() =>
            new ServerState()
            {
                Address = Client.Address,
                IPEndPoint = Client.IPEndPoint,
                State = Client.State,
                Username = Client.Username,
            };
    }
}
