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

namespace slskd.Users;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

/// <summary>
///     A managed blacklist for CIDRs.
/// </summary>
public class Blacklist
{
    private Dictionary<int, (uint First, uint Last)[]> Cache { get; }

    public void Load(string filename)
    {
        // todo:
        // _stream_ contents of file into a ConcurrentDictionary<int, List<>>
        // load that dictionary into the Cache
        // enjoy
    }

    public bool Contains(IPAddress ip)
    {
        int first = ip.GetAddressBytes()[0];

        if (!Cache.TryGetValue(first, out (uint, uint)[] cidrs))
        {
            return false;
        }

        var ipAsUint32 = ToUint32(ip);

        foreach (var cidr in cidrs)
        {
            if (ipAsUint32 >= cidr.Item1 && ipAsUint32 <= cidr.Item2)
            {
                return true;
            }
        }

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