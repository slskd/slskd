// <copyright file="SearchResponse.cs" company="slskd Team">
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
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Text.Json.Serialization;

    public class SearchResponse
    {
        [ForeignKey("Search")]
        [JsonIgnore]
        public Guid SearchId { get; set; }

        [Key]
        public Guid Id { get; init; } = Guid.NewGuid();
        public int FileCount { get; init; }
        public ICollection<File> Files { get; init; } = new List<File>();
        public int FreeUploadSlots { get; init; }
        public int LockedFileCount { get; init; }
        public long QueueLength { get; init; }
        public int Token { get; init; }
        public int UploadSpeed { get; init; }
        public string Username { get; init; }

        public static SearchResponse FromSoulseekSearchResponse(Soulseek.SearchResponse searchResponse, Guid searchId)
        {
            var id = Guid.NewGuid();

            var files = searchResponse.Files.Select(file => File.FromSoulseekFile(file, id, isLocked: false)).ToList();
            files.AddRange(searchResponse.LockedFiles.Select(file => File.FromSoulseekFile(file, id, isLocked: true)));

            return new SearchResponse()
            {
                SearchId = searchId,
                Id = id,
                FileCount = searchResponse.FileCount,
                Files = files,
                FreeUploadSlots = searchResponse.FreeUploadSlots,
                LockedFileCount = searchResponse.LockedFileCount,
                QueueLength = searchResponse.QueueLength,
                Token = searchResponse.Token,
                UploadSpeed = searchResponse.UploadSpeed,
                Username = searchResponse.Username,
            };
        }
    }
}