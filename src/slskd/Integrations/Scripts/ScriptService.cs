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
            DefaultCommandPrefix = "/c";
        }
        else
        {
            DefaultExecutable = Environment.GetEnvironmentVariable("SHELL");
            DefaultCommandPrefix = "-c";

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
    private string DefaultCommandPrefix { get; }

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

                // there are three 'modes' that we can use to execute a script, which are detailed below.
                // the mode is negotiated based on the script config, and we rely on the validation in Options
                // to enforce mututal exclusivity and prevent a user from supplying an invalid config
                try
                {
                    var run = script.Value.Run;
                    var executable = run.Executable ?? DefaultExecutable;

                    if (string.IsNullOrEmpty(executable))
                    {
                        Log.Warning("Script '{Script}' will not be run: unable to determine script executable. Update the script configuration, or set your operating system's SHELL envar.");
                        return;
                    }

                    if (!string.IsNullOrEmpty(run.Command))
                    {
                        // 'command' mode takes precedence over 'executable' mode
                        // run the system shell and ensure the specified command is prefixed with the correct flag
                        // this is designed to be the 'pit of success' for this feature that will work for most users,
                        // who most likely have not read the docs and expect this to work like a command line.
                        // users are on the hook for properly (and safely) quoting arguments
                        Log.Debug("Running script '{Script}' in 'command mode'", script.Key);

                        process = new Process()
                        {
                            StartInfo = new ProcessStartInfo(fileName: executable)
                            {
                                WorkingDirectory = Program.ScriptDirectory,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                Arguments = run.Command.StartsWith(DefaultCommandPrefix) ? run.Command : $"{DefaultCommandPrefix} \"{run.Command}\"",
                            },
                        };
                    }
                    else if (run.Arglist is not null)
                    {
                        // 'args list' mode takes precedence over 'args' mode
                        // the supplied list of args will be passed to the constructor of ProcessStartInfo, which is
                        // curiously the only way to pass a list of args (instead of a string). if no executable has been
                        // specified, the system shell is used. this mode is for maximalists who know what they are doing
                        // and want granular control
                        Log.Debug("Running script '{Script}' in 'args list mode'", script.Key);

                        process = new Process()
                        {
                            StartInfo = new ProcessStartInfo(
                                fileName: executable,
                                arguments: run.Arglist)
                            {
                                WorkingDirectory = Program.ScriptDirectory,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                            },
                        };
                    }
                    else
                    {
                        // 'args' mode is the default mode
                        // run the specified executable, or if not specified, the system shell. pass the args string
                        // (which may be empty or null) to ProcessStartInfo
                        Log.Debug("Running script '{Script}' in 'args mode'", script.Key);
                        process = new Process()
                        {
                            StartInfo = new ProcessStartInfo(fileName: executable)
                            {
                                WorkingDirectory = Program.ScriptDirectory,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                Arguments = run.Args,
                            },
                        };
                    }

                    Log.Debug("Running script '{Script}': \"{Executable}\" {Args} (id: {ProcessId})", script.Key, executable, run.Args ?? string.Join(' ', run.Arglist ?? []), processId);
                    var sw = Stopwatch.StartNew();

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
                    Log.Warning("Failed to run script '{Script}' for event type {Event}: {Message} (id: {ProcessId})", script.Key, data.Type, ex.Message, processId);
                    Log.Debug(ex, "Failed to run script '{Script}' for event type {Event}: {Message} (id: {ProcessId})", script.Key, data.Type, ex.Message, processId);
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