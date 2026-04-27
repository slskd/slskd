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
using Soulseek;

public class Batch
{
    public Guid? SearchId { get; init; } = null;

    [Key]
    public Guid Id { get; init; }
    public string Username { get; init; }
    public TransferDirection Direction { get; } = TransferDirection.Download;

    [NotMapped]
    public TransferStates State => ComputeState();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [NotMapped]
    public DateTime? EndedAt => Transfers.Max(t => t.EndedAt);

    public ICollection<Transfer> Transfers { get; set; } = [];

    public string Destination { get; init; }

    [NotMapped]
    public long BytesTransferred => Transfers.Sum(t => t.BytesTransferred);
    [NotMapped]
    public int Files => Transfers.Count;
    [NotMapped]
    public long Size => Transfers.Sum(t => t.Size);
    [NotMapped]
    public long BytesRemaining => Size - BytesTransferred;
    [NotMapped]
    public TimeSpan ElapsedTime => DateTime.UtcNow - CreatedAt;
    [NotMapped]
    public double PercentComplete => Size == 0 ? 0 : (BytesTransferred / (double)Size) * 100;

    [NotMapped]
    public double AverageSpeed => Transfers
        .Where(t => TransferStateCategories.Successful.Contains(t.State))
        .Select(t => t.AverageSpeed)
        .DefaultIfEmpty(0)
        .Average();

    [NotMapped]
    public bool Removed => Transfers.All(t => t.Removed);

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