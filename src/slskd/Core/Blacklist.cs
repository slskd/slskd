// <copyright file="Blacklist.cs" company="slskd Team">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NetTools;
using Serilog;

/// <summary>
///     Blacklist file formats.
/// </summary>
public enum BlacklistFormat
{
    /// <summary>
    ///     Automatically detect format based on contents.
    /// </summary>
    AutoDetect,

    /// <summary>
    ///     CIDR format. <see href="https://en.wikipedia.org/wiki/Classless_Inter-Domain_Routing"/>.
    /// </summary>
    /// <example>
    ///     <code>
    ///         1.2.4.0/24
    ///         1.2.8.0/24
    ///         1.9.96.105/32
    ///     </code>
    /// </example>
    CIDR,

    /// <summary>
    ///     P2P format. <see href="https://sourceforge.net/p/peerguardian/wiki/dev-blocklist-format-p2p/"/>.
    /// </summary>
    /// <example>
    ///     <code>
    ///         China Internet Information Center (CNNIC):1.2.4.0-1.2.4.255
    ///         China Internet Information Center (CNNIC):1.2.8.0-1.2.8.255
    ///         Botnet on Telekom Malaysia:1.9.96.105-1.9.96.105
    ///     </code>
    /// </example>
    P2P,

    /// <summary>
    ///     DAT format. <see href="https://sourceforge.net/p/peerguardian/wiki/dev-blocklist-format-dat/"/>.
    /// </summary>
    /// <example>
    ///     <code>
    ///         001.002.004.000 - 001.002.004.255 , 000 , China Internet Information Center (CNNIC)
    ///         001.002.008.000 - 001.002.008.255 , 000 , China Internet Information Center (CNNIC)
    ///         001.009.096.105 - 001.009.096.105 , 000 , Botnet on Telekom Malaysia
    ///     </code>
    /// </example>
    DAT,
}

/// <summary>
///     A managed blacklist for CIDRs.
/// </summary>
public class Blacklist
{
    /// <summary>
    ///     Gets the total number of loaded CIDRs.
    /// </summary>
    public long Count => Cache.Sum(kvp => kvp.Value.Length);

    private ILogger Log { get; set; } = Serilog.Log.ForContext<Blacklist>();
    private ConcurrentDictionary<int, (uint First, uint Last)[]> Cache { get; set; } = new();

    /// <summary>
    ///     Examines the contents of the specified <paramref name="filename"/> and attempts to determine the format.
    /// </summary>
    /// <param name="filename">The fully qualified path to the file to examine.</param>
    /// <returns>The detected format.</returns>
    /// <exception cref="IOException">Thrown if the specified filename can't be accessed.</exception>
    /// <exception cref="FormatException">Thrown if the format can't be determined.</exception>
    public static async Task<BlacklistFormat> DetectFormat(string filename)
    {
        using var reader = new StreamReader(filename, options: new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.Read,
        });

        string line = default;

        // read the first non-empty, non-commented line from the file
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            try
            {
                // CIDR format: 1.2.4.0/24
                if (IPAddressRange.TryParse(line, out _))
                {
                    return BlacklistFormat.CIDR;
                }
            }
            catch
            {
            }

            try
            {
                // P2P format: China Internet Information Center (CNNIC):1.2.4.0-1.2.4.255
                if (IPAddressRange.TryParse(line.Split(':')[1], out _))
                {
                    return BlacklistFormat.P2P;
                }
            }
            catch
            {
            }

            try
            {
                // DAT format: 001.002.004.000 - 001.002.004.255 , 000 , China Internet Information Center (CNNIC)
                if (IPAddressRange.TryParse(line.Split(",")[0], out _))
                {
                    return BlacklistFormat.DAT;
                }
            }
            catch
            {
            }
        }

        throw new FormatException($"Failed to detect blacklist format. Only CIDR, P2P and DAT formats are supported");
    }

    /// <summary>
    ///     Clears the contents of the Blacklist.
    /// </summary>
    public virtual void Clear() => Cache.Clear();

    /// <summary>
    ///     Loads the contents of the specified <paramref name="filename"/> into the Blacklist.
    /// </summary>
    /// <remarks>
    ///     The existing Blacklist contents are replaced atomically.
    /// </remarks>
    /// <param name="filename">The fully qualified path to the file to load.</param>
    /// <param name="format">The optional blacklist file format.</param>
    /// <returns>The operation context.</returns>
    /// <exception cref="IOException">Thrown if the specified filename can't be accessed.</exception>
    /// <exception cref="FormatException">
    ///     Thrown if any of the lines in the file do not match the specified or auto-detected <paramref name="format"/>.
    /// </exception>
    /// <exception cref="FormatException">
    ///     Thrown if the specified <paramref name="format"/> is <see cref="BlacklistFormat.AutoDetect"/> and the format could not be detected.
    /// </exception>
    public virtual async Task Load(string filename, BlacklistFormat format = BlacklistFormat.AutoDetect)
    {
        if (format == BlacklistFormat.AutoDetect)
        {
            Log.Debug("Attempting to auto-detect blacklist fomat from contents...");
            format = await DetectFormat(filename); // FormatException if unsuccessful
            Log.Debug("Detected blacklist format {Format}", format);
        }

        using var reader = new StreamReader(filename, options: new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.Read,
            BufferSize = 262144, // 256kib, assumes most blacklists will be > 1mb
        });

        var dict = new ConcurrentDictionary<int, List<(uint First, uint Last)>>();
        string line = default;
        int lineNumber = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            IPAddressRange cidr = default;

            /*
                parse CIDR from string using specified or detected format
                note: we should never get this far without format being one of CIDR, P2P, DAT
            */

            try
            {
                // CIDR format: 1.2.4.0/24
                if (format == BlacklistFormat.CIDR)
                {
                    cidr = IPAddressRange.Parse(line);
                }

                // P2P format: China Internet Information Center (CNNIC):1.2.4.0-1.2.4.255
                else if (format == BlacklistFormat.P2P)
                {
                    cidr = IPAddressRange.Parse(line.Split(':').Last());
                }

                // DAT format: 001.002.004.000 - 001.002.004.255 , 000 , China Internet Information Center (CNNIC)
                else if (format == BlacklistFormat.DAT)
                {
                    // the IPAddressRange library doesn't like leading zeros, so we have to remove them
                    static string TrimLeadingZerosFromEachOctet(string ip)
                        => string.Join('.', ip.Split('.').Select(octet => octet == "000" ? "0" : octet.TrimStart('0')));

                    // 001.002.004.000-001.002.004.255
                    var range = line.Split(",").First().Replace(" ", string.Empty);

                    // [1.2.4.0, 1.2.4.255]
                    var ips = range.Split("-")
                        .Select(TrimLeadingZerosFromEachOctet);

                    cidr = IPAddressRange.Parse(string.Join('-', ips));
                }
            }
            catch (Exception ex)
            {
                throw new FormatException($"Failed to parse {format} blacklist line {lineNumber} '{line}': {ex.Message}");
            }

            // grab the first octet of the first and last addresses in the range
            var first = int.Parse(cidr.Begin.ToString().Split('.')[0]);
            var last = int.Parse(cidr.End.ToString().Split('.')[0]);

            var entry = (ToUint32(cidr.Begin), ToUint32(cidr.End));

            // CIDRs with a mask of /7 and lower span multiple octets, so we must add these CIDRs to the list for each octet
            for (int i = 0; i <= last - first; i++)
            {
                dict.AddOrUpdate(
                    key: first + i, // first octet
                    addValueFactory: (_) => new List<(uint, uint)> { entry }, // if we haven't seen this octet yet, initialize the list
                    updateValueFactory: (_, list) =>
                    {
                        list.Add(entry); // if we've seen it already, append
                        return list;
                    });
            }
        }

        // copy working dictionary to a temporary cache to:
        //   * deduplicate entries
        //   * sort arrays by first IP to enable binary search (future?)
        //   * convert List<> to array to increase access speed
        var tempCache = new ConcurrentDictionary<int, (uint First, uint Last)[]>();

        foreach (var key in dict.Keys)
        {
            tempCache[key] = dict[key]
                .Distinct()
                .OrderBy(x => x.First)
                .ToArray();
        }

        // swap the temporary cache for the "real" cache, enabling a bumpless update
        Cache = tempCache;
    }

    /// <summary>
    ///     Returns a value indicating whether the specified <paramref name="ip"/> is contained witin the blacklist.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>A value indicating whether the specified IP is contained within the blacklist.</returns>
    public virtual bool Contains(IPAddress ip)
    {
        // grab the first octet
        int first = ip.GetAddressBytes()[0];

        // check to see if *any* CIDRs covering this offset are in the blacklist
        // best case scenario for performance if not, roughly O(1)
        if (!Cache.TryGetValue(first, out (uint, uint)[] cidrs))
        {
            return false;
        }

        // conver the entire IP to a uint
        var ipAsUint32 = ToUint32(ip);

        // check the list for this octet sequentially until the first match
        foreach (var cidr in cidrs)
        {
            if (ipAsUint32 >= cidr.Item1 && ipAsUint32 <= cidr.Item2)
            {
                return true;
            }
        }

        // not in the list
        // worst case scenario for performance, O(n) where n = list length
        return false;
    }

    private uint ToUint32(IPAddress ip)
    {
        byte[] bytes = ip.GetAddressBytes();

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt32(bytes, 0);
    }
}