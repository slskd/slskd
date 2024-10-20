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
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
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
        await Task.Yield();

        Log.Debug("Handling event {Event}", data);

        bool EqualsThisEvent(string type) => type.Equals(data.Type.ToString(), StringComparison.OrdinalIgnoreCase);
        bool EqualsLiterallyAnyEvent(string type) => type.Equals(EventType.Any.ToString(), StringComparison.OrdinalIgnoreCase);

        var options = OptionsMonitor.CurrentValue;
        var scriptsTriggeredByThisEventType = options.Integration.Scripts
            .Where(kvp => kvp.Value.On.Any(EqualsThisEvent) || kvp.Value.On.Any(EqualsLiterallyAnyEvent));

        foreach (var script in scriptsTriggeredByThisEventType)
        {
            _ = Task.Run(() =>
            {
                Process process = default;

                try
                {
                    var run = script.Value.Run;
                    var executable = run.Split(" ", 2)[0];
                    var args = run.Split(" ", 2)[1].Replace("$DATA", data.ToJson());

                    Log.Debug("Running script '{Script}': {Run}", script.Key, run);
                    var sw = Stopwatch.StartNew();

                    process = new Process()
                    {
                        StartInfo = new ProcessStartInfo(fileName: executable)
                        {
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            RedirectStandardInput = true,
                        },
                    };

                    process.Start();
                    process.StandardInput.WriteLine($"{args}");
                    process.StandardInput.WriteLine("exit");

                    process.WaitForExit();
                    sw.Stop();

                    var error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new Exception($"STDERR: {Regex.Replace(error, @"\r\n?|\n", " ", RegexOptions.Compiled)}");
                    }

                    var result = process.StandardOutput.ReadToEnd();
                    var resultAsLines = result.Split(["\r\n", "\r", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    Log.Debug("Script '{Script}' ran successfully in {Duration}ms; output: {Output}", script.Key, sw.ElapsedMilliseconds, resultAsLines);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to run script '{Script}': {Message}", script.Key, ex.Message);
                }
                finally
                {
                    try
                    {
                        process?.Close();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to clean up process started from script '{Script}': {Message}", script.Key, ex.Message);
                    }
                }
            });
        }
    }
}