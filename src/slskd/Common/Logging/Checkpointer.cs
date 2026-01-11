// <copyright file="Checkpointer.cs" company="slskd Team">
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

namespace slskd;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Serilog;

public static class CheckpointerExtensions
{
    public static Checkpointer Checkpointer(this ILogger logger, string context = null, [CallerMemberName] string memberName = "")
    {
        return new Checkpointer(context, memberName, logger);
    }
}

public sealed class Checkpointer : IDisposable
{
    public Checkpointer(
        string context = null,
        [CallerMemberName] string memberName = "",
        ILogger logger = null)
    {
        Stopwatch = Stopwatch.StartNew();
        Context = $"{context ?? memberName}";
        Logger = logger ?? Log.ForContext<Checkpointer>().ForContext("CheckpointerContext", Context);
        Checkpoints = new List<(string, long)>();
        LastCheckpointMs = 0;

        Logger.Debug("[{Context}] Started", Context);
    }

    private Stopwatch Stopwatch { get; }
    private string Context { get; }
    private ILogger Logger { get; }
    private List<(string Name, long ElapsedMs)> Checkpoints { get; }
    private long LastCheckpointMs { get; set; }

    public void Capture(
        string name,
        [CallerLineNumber] int lineNumber = 0)
    {
        var currentMs = Stopwatch.ElapsedMilliseconds;
        var deltaMs = currentMs - LastCheckpointMs;

        Checkpoints.Add((name, currentMs));

        Logger.Warning(
            "📌[{Context}] [+{Delta}ms] {Checkpoint} ({Total}ms)",
            Context,
            currentMs.ToString().PadLeft(5),
            name,
            deltaMs);

        LastCheckpointMs = currentMs;
    }

    public void Summary()
    {
        Stopwatch.Stop();
        var totalMs = Stopwatch.ElapsedMilliseconds;

        Logger.Information(
            "[{Context}] Completed in {Total}ms with {Count} checkpoints",
            Context,
            totalMs,
            Checkpoints.Count);
    }

    public void Dispose()
    {
        if (Stopwatch.IsRunning)
        {
            Summary();
        }
    }
}