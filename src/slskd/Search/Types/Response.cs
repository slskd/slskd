// <copyright file="Response.cs" company="slskd Team">
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

namespace slskd.Search
{
    using System.Collections.Generic;
    using System.Linq;

    public class Response
    {
        public int FileCount { get; init; }
        public ICollection<File> Files { get; init; } = new List<File>();
        public bool HasFreeUploadSlot { get; init; }
        public int LockedFileCount { get; init; }
        public ICollection<File> LockedFiles { get; init; } = new List<File>();
        public long QueueLength { get; init; }
        public int Token { get; init; }
        public int UploadSpeed { get; init; }
        public string Username { get; init; }

        public static Response FromSoulseekSearchResponse(Soulseek.SearchResponse searchResponse)
        {
            return new Response()
            {
                FileCount = searchResponse.FileCount,
                Files = searchResponse.Files.Select(file => File.FromSoulseekFile(file)).ToList(),
                HasFreeUploadSlot = searchResponse.HasFreeUploadSlot,
                LockedFileCount = searchResponse.LockedFileCount,
                LockedFiles = searchResponse.LockedFiles.Select(file => File.FromSoulseekFile(file)).ToList(),
                QueueLength = searchResponse.QueueLength,
                Token = searchResponse.Token,
                UploadSpeed = searchResponse.UploadSpeed,
                Username = searchResponse.Username,
            };
        }
    }
}