// <copyright file="Search.cs" company="slskd Team">
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
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Soulseek;

    public class Search
    {
        public DateTime? EndedAt { get; set; }
        public int FileCount { get; set; }

        [Key]
        public Guid Id { get; init; }

        [NotMapped]
        public bool IsComplete =>
            State.HasFlag(SearchStates.Completed)
            || State.HasFlag(SearchStates.Cancelled)
            || State.HasFlag(SearchStates.TimedOut)
            || State.HasFlag(SearchStates.ResponseLimitReached)
            || State.HasFlag(SearchStates.FileLimitReached)
            || State.HasFlag(SearchStates.Errored);

        public int LockedFileCount { get; set; }
        public int ResponseCount { get; set; }

        [NotMapped]
        public IEnumerable<Response> Responses { get; set; } = new List<Response>();

        [JsonIgnore]
        public string ResponsesJson
        {
            get => JsonSerializer.Serialize(Responses);
            set
            {
                Responses = JsonSerializer.Deserialize<IEnumerable<Response>>(value);
            }
        }

        public string SearchText { get; init; }
        public DateTime StartedAt { get; init; } = DateTime.Now;
        public SearchStates State { get; set; }
        public int Token { get; init; }
    }
}