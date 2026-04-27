// <copyright file="Batch.cs" company="JP Dillingham">
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

namespace slskd.Transfers;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using Soulseek;

public record Batch
{
    public Guid? SearchId { get; init; } = null;

    [Key]
    public Guid Id { get; init; }
    public string Username { get; init; }
    public TransferDirection Direction { get; init; } = TransferDirection.Download;
    public string Destination { get; init; }
    public int Files { get; init; }
    public long Size { get; init; }

    [NotMapped]
    public TransferStates State { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; } // todo: set when the last file is completed, for performance reasons

    /// <summary>
    ///     The transfers associated with the batch.
    /// </summary>
    /// <remarks>
    ///     Lazy loaded.  If not loaded with the batch record, the collection and all of the statistics will be null.
    /// </remarks>
    public ICollection<Transfer> Transfers { get; init; }

    [NotMapped]
    public long BytesTransferred { get; init; }

    [NotMapped]
    public long BytesRemaining { get; init; }

    [NotMapped]
    public TimeSpan ElapsedTime => (EndedAt ?? DateTime.UtcNow) - CreatedAt;

    [NotMapped]
    public double PercentComplete { get; init; }

    [NotMapped]
    public double AverageSpeed { get; init; }

    [JsonIgnore]
    public bool Removed { get; init; }

    private TransferStates ComputeState()
    {
        // if there's a transfer in progress, the batch is in progress
        if (Transfers.Any(t => TransferStateCategories.InProgress.Contains(t.State)))
        {
            return TransferStates.InProgress;
        }

        // if no transfers are in progress but at least one is queued (doesn't matter locally or remotely),
        // the batch is queued.  it doesn't matter if one or more are errored, they might be retried
        if (Transfers.Any(t => TransferStateCategories.Queued.Contains(t.State)))
        {
            return TransferStates.Queued;
        }

        // if all transfers completed successfully, the batch did too
        if (Transfers.All(t => TransferStateCategories.Successful.Contains(t.State)))
        {
            return TransferStates.Completed | TransferStates.Succeeded;
        }

        // if one or more transfers failed and there are no more enqueued files,
        // the batch errored and it will not recover on its own
        if (Transfers.Any(t => TransferStateCategories.Failed.Contains(t.State)))
        {
            return TransferStates.Completed | TransferStates.Errored;
        }

        // unclear how we would get here
        return TransferStates.None;
    }
}