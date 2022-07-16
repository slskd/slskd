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
    using Soulseek;

    public class Transfer
    {
        [Key]
        public Guid Id { get; init; }
        public string Username { get; init; }
        public TransferDirection Direction { get; init; }
        public string Filename { get; init; }
        public long Size { get; set; }
        public long StartOffset { get; init; }

        public TransferStates State { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? EnqueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public long BytesTransferred { get; set; }
        public double AverageSpeed { get; set; }

        public Exception Exception { get; set; }

        [NotMapped]
        public long BytesRemaining => Size - BytesTransferred : null;
        [NotMapped]
        public TimeSpan? ElapsedTime => StartedAt == null ? null : (EndedAt ?? DateTime.UtcNow) - StartedAt.Value;
        [NotMapped]
        public double PercentComplete => Size == 0 ? 0 : (BytesTransferred / (double)Size) * 100;
        [NotMapped]
        public TimeSpan? RemainingTime => AverageSpeed == 0 ? null : TimeSpan.FromSeconds(BytesRemaining / AverageSpeed);
    }
}