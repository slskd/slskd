// <copyright file="QueueDownloadBatchRequest.cs" company="JP Dillingham">
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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using slskd.Validation;

namespace slskd.Transfers.API;

/// <summary>
///     Enqueue a batch of downloads.
/// </summary>
public record QueueDownloadBatchRequest
{
    /// <summary>
    ///     The ID of the Batch.
    /// </summary>
    /// <remarks>
    ///     If not supplied, one will be randomly generated.
    /// </remarks>
    [Guid]
    public string Id { get; init; }

    /// <summary>
    ///     The ID of the associated Search, if applicable.
    /// </summary>
    [Guid]
    public string SearchId { get; init; }

    /// <summary>
    ///     The username of the user from which to download.
    /// </summary>
    [Required]
    [String(AllowNull = false, AllowEmpty = false, AllowWhiteSpace = false, MinimumLength = 1, MaximumLength = 500)]
    public string Username { get; init; }

    /// <summary>
    ///     The list of files to download.
    /// </summary>
    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<EnqueueDownloadBatchItem> Files { get; init; } = [];

    /// <summary>
    ///     Options for the Batch.
    /// </summary>
    public EnqueueDownloadBatchOptions Options { get; init; } = new();
}

/// <summary>
///     An item in a download batch.
/// </summary>
public record EnqueueDownloadBatchItem
{
    /// <summary>
    ///     The name of the file.
    /// </summary>
    [Required]
    [NotNullOrWhiteSpace]
    [NonTraversingPath]
    public string Filename { get; init; }

    /// <summary>
    ///     The file size.
    /// </summary>
    [Required]
    [Range(0, long.MaxValue)]
    public long? Size { get; init; }
}

/// <summary>
///     Download batch options.
/// </summary>
public record EnqueueDownloadBatchOptions
{
    /// <summary>
    ///     The destination directory for the files, relative to the configured download directory.
    /// </summary>
    [RelativePath(OperatingSystem.All)]
    [NonTraversingPath]
    [String(AllowNull = true, AllowEmpty = false, AllowWhiteSpace = false, MinimumLength = 1)]
    public string Destination { get; init; }

    /// <summary>
    ///     An optional external ID for the batch.
    /// </summary>
    public string ExternalId { get; init; }
}