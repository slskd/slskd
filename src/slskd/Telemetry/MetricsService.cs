// <copyright file="MetricsService.cs" company="JP Dillingham">
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

namespace slskd.Telemetry;

using Serilog;

/// <summary>
///     Metrics.
/// </summary>
public class MetricsService
{
    private ILogger Log { get; } = Serilog.Log.ForContext<MetricsService>();

    public object Current()
    {
        var metrics = new
        {
            Downloads = new
            {
                InProgress = new
                {
                    Files = Metrics.Transfers.Downloads.InProgress.Files.Value,
                    Users = Metrics.Transfers.Downloads.InProgress.Users.Value,
                    AverageSpeed = Metrics.Transfers.Downloads.InProgress.CurrentAverageSpeed.Value,
                    TotalSpeed = Metrics.Transfers.Downloads.InProgress.CurrentTotalSpeed.Value,
                },
                Queued = new
                {
                    Files = Metrics.Transfers.Downloads.Queued.Files.Value,
                    Users = Metrics.Transfers.Downloads.Queued.Users.Value,
                    Bytes = Metrics.Transfers.Downloads.Queued.Bytes.Value,
                },
                Completed = new
                {
                    Succeeded = Metrics.Transfers.Downloads.Completed.Succeeded.Value,
                    Failed = Metrics.Transfers.Downloads.Completed.Failed.Value,
                    Bytes = Metrics.Transfers.Downloads.Completed.Bytes.Value,
                },
            },
            Uploads = new
            {
                InProgress = new
                {
                    Files = Metrics.Transfers.Uploads.InProgress.Files.Value,
                    Users = Metrics.Transfers.Uploads.InProgress.Users.Value,
                    AverageSpeed = Metrics.Transfers.Uploads.InProgress.CurrentAverageSpeed.Value,
                    TotalSpeed = Metrics.Transfers.Uploads.InProgress.CurrentTotalSpeed.Value,
                },
                Queued = new
                {
                    Files = Metrics.Transfers.Uploads.Queued.Files.Value,
                    Users = Metrics.Transfers.Uploads.Queued.Users.Value,
                    Bytes = Metrics.Transfers.Uploads.Queued.Bytes.Value,
                },
                Completed = new
                {
                    Succeeded = Metrics.Transfers.Uploads.Completed.Succeeded.Value,
                    Failed = Metrics.Transfers.Uploads.Completed.Failed.Value,
                    Bytes = Metrics.Transfers.Uploads.Completed.Bytes.Value,
                },
            },
        };

        return metrics;
    }
}