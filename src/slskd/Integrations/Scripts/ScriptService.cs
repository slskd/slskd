// <copyright file="ScriptService.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd.Integrations.Scripts;

using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using slskd.Events;

/// <summary>
///     Handles the invocation of shell scripts.
/// </summary>
public class ScriptService
{
    public ScriptService(EventBus eventBus, IOptionsMonitor<Options> optionsMonitor)
    {
        Events = eventBus;
        OptionsMonitor = optionsMonitor;

        Events.Subscribe<Event>(nameof(ScriptService), HandleEvent);

        Log.Debug("{Service} initialized", nameof(ScriptService));
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<ScriptService>();
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private EventBus Events { get; }

    private async Task HandleEvent(Event data)
    {
        Log.Debug("Handling event {Event}", data);

        bool EqualsThisEvent(string type) => type.Equals(data.Type.ToString(), StringComparison.OrdinalIgnoreCase);
        bool EqualsLiterallyAnyEvent(string type) => type.Equals(EventType.Any.ToString(), StringComparison.OrdinalIgnoreCase);

        var options = OptionsMonitor.CurrentValue;
        var scriptsTriggeredByThisEventType = options.Integration.Scripts
            .Where(kvp => kvp.Value.On.Any(EqualsThisEvent) || kvp.Value.On.Any(EqualsLiterallyAnyEvent));

        foreach (var script in scriptsTriggeredByThisEventType)
        {
            Console.WriteLine($"--------------------{script.Key}");
        }
    }
}