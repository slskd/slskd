// <copyright file="File.cs" company="slskd Team">
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
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Text.Json.Serialization;

    public class File
    {
        [ForeignKey("SearchResponse")]
        [JsonIgnore]
        public Guid SearchResponseId { get; set; }

        [Key]
        public Guid Id { get; set; }
        public int? BitDepth { get; init; }
        public int? BitRate { get; init; }
        public int Code { get; init; }
        public string Extension { get; init; }
        public string Filename { get; init; }
        public bool? IsVariableBitRate { get; init; }
        public int? Length { get; init; }
        public int? SampleRate { get; init; }
        public long Size { get; init; }
        public bool IsLocked { get; init; }

        public static File FromSoulseekFile(Soulseek.File file, Guid searchResponseId, bool isLocked = false)
        {
            return new File()
            {
                SearchResponseId = searchResponseId,
                Id = Guid.NewGuid(),
                BitDepth = file.BitDepth,
                BitRate = file.BitRate,
                Code = file.Code,
                Extension = file.Extension,
                Filename = file.Filename,
                IsVariableBitRate = file.IsVariableBitRate,
                Length = file.Length,
                SampleRate = file.SampleRate,
                Size = file.Size,
                IsLocked = isLocked,
            };
        }
    }
}