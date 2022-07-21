// <copyright file="Dumper.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    /// <summary>
    ///     Dumps the contents of the application's memory to a .dmp file using dotnet-dump.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Monitor https://docs.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump for tool updates. Currently supported
    ///         runtime IDs:
    ///     </para>
    ///     <list type="bullet">
    ///         <listheader>Currently supported runtime IDs:</listheader>
    ///         <item>https://aka.ms/dotnet-dump/win-x86</item>
    ///         <item>https://aka.ms/dotnet-dump/win-x64</item>
    ///         <item>https://aka.ms/dotnet-dump/win-arm</item>
    ///         <item>https://aka.ms/dotnet-dump/win-arm64</item>
    ///         <item>https://aka.ms/dotnet-dump/osx-x64</item>
    ///         <item>https://aka.ms/dotnet-dump/linux-x64</item>
    ///         <item>https://aka.ms/dotnet-dump/linux-arm</item>
    ///         <item>https://aka.ms/dotnet-dump/linux-arm64</item>
    ///         <item>https://aka.ms/dotnet-dump/linux-musl-x64</item>
    ///         <item>https://aka.ms/dotnet-dump/linux-musl-arm64</item>
    ///         <item></item>
    ///     </list>
    /// </remarks>
    public class Dumper : IDisposable
    {
        private static readonly string URLTemplate = "https://aka.ms/dotnet-dump/$RID";

        private string BinFile { get; set; }
        private bool Disposed { get; set; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async Task<string> DumpAsync()
        {
            BinFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var outputFile = Path.Combine(Path.GetTempPath(), $"slskd_{Path.GetRandomFileName()}.dmp");

            var url = URLTemplate.Replace("$RID", GetRID());

            await Download(url, BinFile);

            await ExecAsync(BinFile, Environment.ProcessId, outputFile);

            return outputFile;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    TryDelete(BinFile);
                }

                Disposed = true;
            }
        }

        private async Task Download(string url, string destination)
        {
            using var http = new HttpClient();

            using var localStream = new FileStream(destination, FileMode.OpenOrCreate);
            using var remoteStream = await http.GetStreamAsync(url);

            await remoteStream.CopyToAsync(localStream);
        }

        private async Task ExecAsync(string bin, int pid, string output)
        {
            using var process = new Process();
            process.StartInfo.FileName = bin;
            process.StartInfo.Arguments = $"collect --process-id {pid} --type full --output {output}";
            process.Start();
            await process.WaitForExitAsync();
        }

        private string GetRID()
        {
            // one of: x86, x64, arm, arm64, wasm, s390x
            var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();

            // .RuntimeIdentifier returns a very specific (e.g. win10-x64) RID, and we need a generic one (e.g. win-x64). rather
            // than trying to hack this up to derive a generic RID, only inspect this to see if this build targeted musl libc (we
            // can't get this any other way)
            var isMusl = RuntimeInformation.RuntimeIdentifier.ToLower().Contains("musl");

            string os = default;

            // seems like there should be a way to just retrieve this, but there is not as of .NET 6
            if (OperatingSystem.IsLinux())
            {
                os = "linux";
            }
            else if (OperatingSystem.IsWindows())
            {
                os = "win";
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            {
                os = "osx";
            }

            if (os == default)
            {
                throw new PlatformNotSupportedException($"Unable to determine operating system. RID is {RuntimeInformation.RuntimeIdentifier}; did someone forget to update Dumper.cs to reflect .NET targeting changes?");
            }

            return $"{os}-{(isMusl ? "musl-" : string.Empty)}{arch}";
        }

        private bool TryDelete(string file)
        {
            try
            {
                File.Delete(file);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
    }
}