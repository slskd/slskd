// <copyright file="Transfer.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
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
        public Guid? BatchId { get; init; } = null;
        public string DestinationDirectory { get; set; } = null;

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

        public TransferStates State { get; set; } = TransferStates.None;

        /// <summary>
        ///     Gets the string representation of the transfer <see cref="State"/>.
        /// </summary>
        /// <remarks>
        ///     This is a hack to get the string into the database. *DO NOT* use this property in code
        ///     and especially **DO NOT** set the value. The getter and setter can't be protected because
        ///     EF Core needs them to be public.
        /// </remarks>
        public string StateDescription { get; set; }
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

        public int Attempts { get; set; } = 0;

        public DateTime? NextAttemptAt { get; set; } = null;

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