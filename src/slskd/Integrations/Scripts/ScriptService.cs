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

        if (OperatingSystem.IsWindows())
        {
            DefaultExecutable = "cmd.exe";
        }
        else
        {
            DefaultExecutable = Environment.GetEnvironmentVariable("SHELL");

            if (string.IsNullOrEmpty(DefaultExecutable))
            {
                Log.Warning("Unable to determine default script executable ($SHELL is missing or blank); any user-defined scripts that do not specify an executable will fail");
            }
        }

        Log.Information("Set default script executable to '{DefaultExecutable}'", DefaultExecutable);
        Log.Debug("{Service} initialized", nameof(ScriptService));
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<ScriptService>();
    private IOptionsMonitor<Options> OptionsMonitor { get; }
    private EventBus Events { get; }
    private string DefaultExecutable { get; }

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
                var processId = Guid.NewGuid();

                try
                {
                    var run = script.Value.Run;
                    string executable = default;
                    string args = default;

                    // determine whether the executable is wrapped in quotes, and if so, set the executable to the
                    // text contained within. the regex contains two capturing groups; the initial quoted string, and
                    // everything that comes after. if both groups capture something, we were given a quoted string to start
                    // otherwise no quotes were used, and we should use the first word as the executable
                    var matches = LeadingQuotedString.Matches(run);

                    if (matches.Count == 2)
                    {
                        executable = matches[0].Value;
                        args = matches[1].Value;
                    }
                    else
                    {
                        // didn't start with a quoted string, so just split the string into at most 2 parts, leaving
                        // part [0] the executable and part [1] the rest of the string (but possibly null)
                        var parts = run.Split(" ", count: 2, options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.RemoveEmptyEntries);

                        executable = parts[0];
                        args = parts.Length > 1 ? parts[1] : null;
                    }

                    Log.Debug("Running script '{Script}': \"{Executable}\" {Args} (id: {ProcessId})", script.Key, executable, args, processId);
                    var sw = Stopwatch.StartNew();

                    process = new Process()
                    {
                        StartInfo = new ProcessStartInfo(fileName: executable)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            Arguments = args,
                        },
                    };

                    process.StartInfo.Environment.Clear();
                    process.StartInfo.EnvironmentVariables["SLSKD_SCRIPT_DATA"] = data.ToJson();
                    process.Start();

                    process.WaitForExit();
                    sw.Stop();

                    var error = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new Exception($"STDERR: {Regex.Replace(error, @"\r\n?|\n", " ", RegexOptions.Compiled)}");
                    }

                    var result = process.StandardOutput.ReadToEnd();
                    var resultAsLines = result.Split(["\r\n", "\r", "\n"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    Log.Debug("Script '{Script}' ran successfully in {Duration}ms; output: {Output} (id: {ProcessId})", script.Key, sw.ElapsedMilliseconds, resultAsLines, processId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to run script '{Script}' for event type {Event}: {Message} (id: {ProcessId})", script.Key, data.Type, ex.Message, processId);
                }
                finally
                {
                    try
                    {
                        process?.Close();
                        process?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to clean up process started from script '{Script}' for event type {Event}: {Message} (id: {ProcessId})", script.Key, data.Type, ex.Message, processId);
                    }
                }
            });
        }
    }
}