// <copyright file="UserUploadStatistics.cs" company="slskd Team">
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

namespace slskd.Transfers;

/// <summary>
///     Upload statistics, for use when evaluating incoming enqueue requests against configured limits.
/// </summary>
public record UserUploadStatistics
{
    public int QueuedFiles { get; set; }
    public long QueuedBytes { get; set; }

    public int WeeklyFailedFiles { get; set; }
    public int WeeklySucceededFiles { get; set; }
    public long WeeklySucceededBytes { get; set; }

    public int DailyFailedFiles { get; set; }
    public int DailySucceededFiles { get; set; }
    public long DailySucceededBytes { get; set; }
}