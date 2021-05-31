// <copyright file="PeerService.cs" company="slskd Team">
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

namespace slskd.Peer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using ISoulseekClient = Soulseek.ISoulseekClient;

    public class PeerService : IPeerService
    {
        private const int CacheTTLSeconds = 300;

        public PeerService(
            ISoulseekClient client,
            IDbContextFactory<PeerDbContext> contextFactory,
            ILogger<PeerService> log)
        {
            Client = client;
            ContextFactory = contextFactory;
            Log = log;
        }

        private ISoulseekClient Client { get; }
        private IDbContextFactory<PeerDbContext> ContextFactory { get; }
        private ILogger<PeerService> Log { get; set; }

        public async Task<Peer> GetAsync(string username)
        {
            using var context = ContextFactory.CreateDbContext();

            var peer = await context.Peers.FirstOrDefaultAsync(peer => peer.Username == username);
            var peerExists = peer != default;

            if (peerExists && peer.UpdatedAt < DateTime.UtcNow.AddSeconds(CacheTTLSeconds))
            {
                return peer;
            }

            var infoTask = Client.GetUserInfoAsync(username);
            var statusTask = Client.GetUserStatusAsync(username);
            var endpointTask = Client.GetUserEndPointAsync(username);

            await Task.WhenAll(infoTask, statusTask, endpointTask);

            var info = await infoTask;
            var status = await statusTask;
            var endpoint = await endpointTask;

            peer = new Peer()
            {
                Username = username,
                Description = info.Description,
                HasFreeUploadSlot = info.HasFreeUploadSlot,
                HasPicture = info.HasPicture,
                Picture = info.Picture,
                QueueLength = info.QueueLength,
                UploadSlots = info.UploadSlots,
                IsPrivileged = status.IsPrivileged,
                Presence = status.Presence,
                IPAddress = endpoint.Address,
                Port = endpoint.Port,
                UpdatedAt = DateTime.UtcNow,
            };

            if (peerExists)
            {
                context.Update(peer);
            }
            else
            {
                context.Add(peer);
            }

            await context.SaveChangesAsync();

            return peer;
        }
    }
}
