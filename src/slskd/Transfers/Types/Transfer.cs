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

namespace slskd.Transfers;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Soulseek;

/// <summary>
///     A file Transfer between network peers.
/// </summary>
public class Transfer
{
    /// <summary>
    ///     Gets the associated <see cref="Batch.Id"/>, if one was specified when the Transfer was created.
    /// </summary>
    public Guid? BatchId { get; init; } = null;

    /// <summary>
    ///     Gets the unique identifier for the Transfer.
    /// </summary>
    [Key]
    public Guid Id { get; init; }

    /// <summary>
    ///     Gets the remote username associated with the Transfer.
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    ///     Gets the Transfer direction (Upload, Download).
    /// </summary>
    public TransferDirection Direction { get; init; }

    /// <summary>
    ///     Gets the remote filename of the file.
    /// </summary>
    /// <remarks>
    ///     Exactly as it appeared in the originating Search, Browse, or file directory contents response.
    /// </remarks>
    public string Filename { get; init; }

    /// <summary>
    ///     Gets or sets the remote size of the file.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    ///     Gets or sets the state of the Transfer.
    /// </summary>
    public TransferStates State { get; set; } = TransferStates.None;

    /// <summary>
    ///     Gets or sets the string representation of the transfer <see cref="State"/>.
    /// </summary>
    /// <remarks>
    ///     This is a hack to get the string into the database. *DO NOT* use this property in code
    ///     and especially **DO NOT** set the value. The getter and setter can't be protected because
    ///     EF Core needs them to be public.
    /// </remarks>
    [Obsolete("Use State isntead; this is a hack for EF")]
    [JsonIgnore]
    public string StateDescription { get; set; }

    /// <summary>
    ///     Gets or sets the time at which the Transfer was requested from the remote peer.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    ///     Gets or sets the time at which the Transfer was enqueued, or null if the Transfer has not yet been enqueued.
    /// </summary>
    /// <remarks>
    ///     For downloads, this is the time at which the remote peer responded to our enqueue request. For uploads this
    ///     is the time at which the transfer was made available for scheduling by the upload queue, after we have
    ///     responded to the remote peer's request to enqueue.
    /// </remarks>
    public DateTime? EnqueuedAt { get; set; }

    /// <summary>
    ///     Gets or sets the time at which the Transfer connection was established and data transfer began, or null if the
    ///     Transfer has not yet been started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     Gets or sets the time at which the Transfer ended, or null if the Transfer has not yet started or is still in progress.
    /// </summary>
    /// <remarks>
    ///     Guaranteed to be set for transfers in a terminal state.
    /// </remarks>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    ///     Gets or sets the number of bytes transferred.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    ///     Gets or sets the average speed of the Transfer over the duration.
    /// </summary>
    public double AverageSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the current place in line in the remote peer's queue, if place has been requested.
    /// </summary>
    /// <remarks>
    ///     May be wildly innacurate to the point of uselessness.
    /// </remarks>
    public int? PlaceInQueue { get; set; }

    /// <summary>
    ///     Gets or sets the associated Exception, if the Transfer did not end successfully, or null if it did.
    /// </summary>
    public string Exception { get; set; }

    /// <summary>
    ///     Gets or sets the number of attempts.  Incremented each time the Transfer is retried.
    /// </summary>
    /// <remarks>
    ///     Applicable to downloads only.
    /// </remarks>
    public int Attempts { get; set; } = 0;

    /// <summary>
    ///     Gets or sets the time at which the next retry attempt will be made, if applicable.
    /// </summary>
    public DateTime? NextAttemptAt { get; set; } = null;

    /// <summary>
    ///     Gets or sets a value indicating whether the Transfer has been removed from the UI.
    /// </summary>
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