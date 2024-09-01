// <copyright file="ShellService.cs" company="slskd Team">
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

namespace slskd.Integrations.Shell;

using System;
using System.Threading.Tasks;
using Serilog;
using slskd.Events;

public class ShellService
{
    public ShellService(EventBus eventBus)
    {
        Events = eventBus;

        Events.Subscribe<DownloadFileCompleteEvent>(nameof(ShellService), HandleDownloadFileCompleteEvent);
        Events.Subscribe<DownloadDirectoryCompleteEvent>(nameof(ShellService), HandleDownloadDirectoryCompleteEvent);

        Log.Debug("Shell service initialized");
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<ShellService>();
    private EventBus Events { get; }

    private async Task HandleDownloadFileCompleteEvent(DownloadFileCompleteEvent data)
    {
        Console.WriteLine("here is where we would call an external application!");
    }

    private async Task HandleDownloadDirectoryCompleteEvent(DownloadDirectoryCompleteEvent data)
    {
        Console.WriteLine("wow we downloaded a whole directory");
    }
}