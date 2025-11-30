// <copyright file="UserSummary.cs" company="slskd Team">
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

namespace slskd.Telemetry;

using System.Collections.Generic;
using Soulseek;

public record UserSummary
{
    public UserTransferSummary Transfers { get; init; }
}

public record UserTransferSummary
{
    public UserTransferDirectionSummary Upload { get; init; }
    public UserTransferDirectionSummary Download { get; init; }
    public UserTransferRatios Ratios { get; init; }
    public Dictionary<TransferDirection, UserTransferErrorSummary> Errors { get; init; }
}

public record UserTransferRatios
{
    public double Count { get; init; }
    public double TotalBytes { get; init; }
}

public record UserTransferDirectionSummary
{
    public Dictionary<TransferStates, TransferSummary> Summary { get; init; }
    public List<UserTransferErrorSummary> Errors { get; init; }
    public UserTransferDirectionStatistics Statistics { get; init; }
}

public record UserTransferErrorSummary
{
    public string Exception { get; init; }
    public long Count { get; init; }
}

public record UserTransferDirectionStatistics
{
    public double SuccessRate { get; init; }
    public double ErrorRate { get; init; }
    public double CancelRate { get; init; }
}

public record UserMessageStatistics
{
    public int Sent { get; init; }
    public int Received { get; init; }
}