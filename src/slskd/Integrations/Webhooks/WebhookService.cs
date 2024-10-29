// <copyright file="WebhookService.cs" company="slskd Team">
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

namespace slskd.Integrations.Webhooks;

using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using slskd.Events;

public class WebhookService
{
    public WebhookService(EventBus eventBus, IOptionsMonitor<Options> optionsMonitor)
    {
        Events = eventBus;
        OptionsMonitor = optionsMonitor;

        Events.Subscribe<Event>(nameof(WebhookService), HandleEvent);

        Log.Debug("{Service} initialized", nameof(WebhookService));
        Log.Warning("Webhook config: {Config}", OptionsMonitor.CurrentValue.Integration.Webhooks.ToJson());
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<WebhookService>();
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private EventBus Events { get; }

    private async Task HandleEvent(Event data)
    {
        await Task.Yield();

        Log.Debug("Handling event {Event}", data);

        bool EqualsThisEvent(string type) => type.Equals(data.Type.ToString(), StringComparison.OrdinalIgnoreCase);
        bool EqualsLiterallyAnyEvent(string type) => type.Equals(EventType.Any.ToString(), StringComparison.OrdinalIgnoreCase);

        var options = OptionsMonitor.CurrentValue;
        var webhooksTriggeredByThisEventType = options.Integration.Webhooks
            .Where(kvp => kvp.Value.On.Any(EqualsThisEvent) || kvp.Value.On.Any(EqualsLiterallyAnyEvent));

        foreach (var webhook in webhooksTriggeredByThisEventType)
        {
            Log.Information("{Webhook}", webhook);
        }
    }
}