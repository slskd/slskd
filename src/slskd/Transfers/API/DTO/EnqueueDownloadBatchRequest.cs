// <copyright file="EnqueueDownloadBatchRequest.cs" company="slskd Team">
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

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace slskd.Transfers.API;

public record EnqueueDownloadBatchRequest
{
    public Guid? BatchId { get; init; }
    public Guid? SearchId { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Username { get; init; }
    public List<EnqueueDownloadBatchItem> Files { get; init; } = [];
}

public record EnqueueDownloadBatchItem
{
    [Required]
    public string Filename { get; set; }

    [Range(0, int.MaxValue)]
    public long Size { get; set; }
}