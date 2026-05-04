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
using Soulseek;

/// <summary>
///     <para>
///         A Transfer Batch is a lightweight association between a number of related Transfer
///         records and optionally the associated Search the Transfers are intended to fulfil.
///     </para>
///     <para>
///         Transfers may be enqueued in a Batch, and a caller that enqueued a Batch may use the
///         available API(s) to determine whether all of the Transfers within the Batch are complete,
///         but otherwise a Batch serves no functional purpose within the application.
///     </para>
/// </summary>
public record Batch
{
    /// <summary>
    ///     Gets the associated <see cref="slskd.Search.Search.Id"/>, if one was specified when the
    ///     Batch was enqueued.
    /// </summary>
    public Guid? SearchId { get; init; } = null;

    /// <summary>
    ///     Gets the unique identifier for the Batch.
    /// </summary>
    [Key]
    public Guid Id { get; init; }

    /// <summary>
    ///     Gets the username associated with the Batch.
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    ///     Gets the Batch direction (Upload, Download).
    /// </summary>
    /// <remarks>
    ///     We don't have, and likely will never be able to implement, upload batches,
    ///     nor are they of any particular use.
    /// </remarks>
    public TransferDirection Direction { get; init; } = TransferDirection.Download;

    /// <summary>
    ///     Gets the time at which the Batch was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the Transfer records associated with the batch.
    /// </summary>
    public ICollection<Transfer> Transfers { get; init; } = null;

    /// <summary>
    ///     Gets the options for the batch.
    /// </summary>
    public BatchOptions Options { get; init; }

    /*
        future: [NotMapped] properties that aggregate values from the associated Transfer records.
        there's no use for these now, but if we ever decide to leverage Batch records for anything
        but determining whether a Search has been fulfilled, allowing a user to override a download directory,
        or to handle an event when a batch is completed, we can add them and figure out how persistence should work.

        file count and size are also not included because of the transfer deduplication that takes place during
        the enqueue; if a batch is created and some or all of the files within are already in progress, they are
        dropped and no new transfer records are created. this would create data inconsistency, and for no reason (for now)

        loading Transfers and doing aggregation in memory (if fetching 1 batch) or querying and aggregating Transfer
        records in SQL and then updating a list of Batch records in memory (if fetching a list) technically works,
        but is unlikely to perform well, especially as data grows, and ESPECIALLY when excliding 'Removed' Batches
        from results (as this must be aggregated on the fly for each batch.. forever)

        my thinking (and reason for punting on this for now); add the interesting properties (Removed, BytesTransferred,
        AverageSpeed, etc) to this class as nullable, and don't set a value until all of the associated Transfer
        records have been finalized, then update the columns.  when fetching batches we can deduce that any Batch
        record missing those properties is still in progress, and we can aggregate the data on the fly. for historical
        records that have completed, the data is all in the database and is quick to query.

        this can sit until/if we have further use for Batch records, i want to get this out and don't want to sit
        and go back and forth trying to speculate on how this might need to work in the future.
    */
}