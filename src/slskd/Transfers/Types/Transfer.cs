// <copyright file="Transfer.cs" company="slskd Team">
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

namespace slskd.Transfers
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Text.Json.Serialization;
    using Soulseek;

    public class Transfer
    {
        [Key]
        public Guid Id { get; init; }
        public string Username { get; init; }
        public TransferDirection Direction { get; init; }

        /// <summary>
        ///     Gets the remote filename.
        /// </summary>
        public string Filename { get; init; }
        public long Size { get; set; }
        public long StartOffset { get; init; }

        public TransferStates State { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? EnqueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }

        /// <summary>
        ///     The time at which the transfer ended, or null if the transfer has not yet started or is in progress.
        /// </summary>
        /// <remarks>
        ///     Guaranteed to be set for transfers in a terminal state.
        /// </remarks>
        public DateTime? EndedAt { get; set; }
        public long BytesTransferred { get; set; }
        public double AverageSpeed { get; set; }

        public int? PlaceInQueue { get; set; }
        public string Exception { get; set; }

        [JsonIgnore]
        public bool Removed { get; set; }

        [NotMapped]
        public long BytesRemaining => Size - BytesTransferred;
        [NotMapped]
        public TimeSpan? ElapsedTime => StartedAt == null ? null : (EndedAt ?? DateTime.UtcNow) - StartedAt.Value;
        [NotMapped]
        public double PercentComplete => Size == 0 ? 0 : (BytesTransferred / (double)Size) * 100;
        [NotMapped]
        public TimeSpan? RemainingTime => AverageSpeed == 0 ? null : TimeSpan.FromSeconds(BytesRemaining / AverageSpeed);
    }
}