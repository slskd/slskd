// <copyright file="MetricsHub.cs" company="JP Dillingham">
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

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Serilog;

namespace slskd.Telemetry;

public static class MetricsHubMethods
{
    public static readonly string Transfers = nameof(Transfers);
}

public static class MetricsHubExtensions
{
    public static Task BroadcastTransferMetrics(this IHubContext<MetricsHub> hub)
    {
        return hub.Clients.All.SendAsync(MetricsHubMethods.Transfers, MetricsHub.GetMetrics());
    }
}

public class MetricsHub : Hub
{
    public MetricsHub()
    {
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(MetricsHubMethods.Transfers, GetMetrics());
    }

    private static ILogger Log { get; } = Serilog.Log.ForContext<MetricsHub>();

    public static object GetMetrics()
    {
        var metrics = new
        {
            Downloads = new
            {
                InProgress = new
                {
                    Files = Metrics.Transfers.Downloads.InProgress.Files.Value,
                    AverageSpeed = Metrics.Transfers.Downloads.InProgress.CurrentAverageSpeed.Value,
                    TotalSpeed = Metrics.Transfers.Downloads.InProgress.CurrentTotalSpeed.Value,
                },
                Queued = new
                {
                    Files = Metrics.Transfers.Downloads.Queued.Files.Value,
                    Users = Metrics.Transfers.Downloads.Queued.Users.Value,
                    Bytes = Metrics.Transfers.Downloads.Queued.Bytes.Value,
                },
            },
            Uploads = new
            {
                InProgress = new
                {
                    Files = Metrics.Transfers.Uploads.InProgress.Files.Value,
                    AverageSpeed = Metrics.Transfers.Uploads.InProgress.CurrentAverageSpeed.Value,
                    TotalSpeed = Metrics.Transfers.Uploads.InProgress.CurrentTotalSpeed.Value,
                },
                Queued = new
                {
                    Files = Metrics.Transfers.Uploads.Queued.Files.Value,
                    Users = Metrics.Transfers.Uploads.Queued.Users.Value,
                    Bytes = Metrics.Transfers.Uploads.Queued.Bytes.Value,
                },
            },
        };

        Log.Warning("{Metrics}", metrics);
        return metrics;
    }
}