// <copyright file="IApplication.cs" company="slskd Team">
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
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Soulseek;

    public interface IApplication : IHostedService
    {
        /// <summary>
        ///     Sends an acknowledgement for the specified private message <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The id of the private message to acknowledge.</param>
        /// <returns>The operation context.</returns>
        public Task AcknowledgePrivateMessageAsync(int id);

        /// <summary>
        ///     Adds a the specified <paramref name="username"/> as a member of the specified private <paramref name="roomName"/>.
        /// </summary>
        /// <param name="roomName">The room to which to add the member.</param>
        /// <param name="username">The username of the member to add.</param>
        /// <returns>The operation context.</returns>
        public Task AddPrivateRoomMemberAsync(string roomName, string username);

        public Task<UserData> AddUserAsync(string username);

        public Task<BrowseResponse> BrowseAsync(string username);

        public Task CheckVersionAsync();

        public Task ConnectAsync();

        public void Disconnect(string message = null, Exception exception = null);

        public Task DownloadAsync(string username, string filename, Stream outputStream, long? size, long startOffset = 0, int? token = null, TransferOptions options = null, CancellationToken? cancellationToken = null);

        public Task<int> GetDownloadPlaceInQueueAsync(string username, string filename);

        public int GetNextToken();

        public Task<RoomList> GetRoomListAsync();

        public Task<IPEndPoint> GetUserEndPointAsync(string username);

        public Task<UserInfo> GetUserInfoAsync(string username);

        public Task<bool> GetUserPrivilegedAsync(string username);

        public Task<UserStatus> GetUserStatusAsync(string username);

        public Task GrantUserPrivilegesAsync(string username, int days);

        public Task LeaveRoomAsync(string roomName);

        public Task RescanSharesAsync();

        public Task<Soulseek.Search> SearchAsync(SearchQuery query, Action<SearchResponse> responseReceived, SearchScope scope = null, int? token = null, SearchOptions options = null, CancellationToken? cancellationToken = null);

        public Task SendPrivateMessageAsync(string username, string message);

        public Task SendRoomMessageAsync(string roomName, string message);

        public Task SetRoomTickerAsync(string roomName, string message);

        public Task StartPublicChatAsync();

        public Task StopPublicChatAsync();
    }
}