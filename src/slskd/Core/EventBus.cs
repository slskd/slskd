// <copyright file="EventBus.cs" company="slskd Team">
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

using System.Threading.Tasks;

namespace slskd;

public enum EventType
{
    NOOP,
    DOWNLOAD_FILE_COMPLETE,
    DOWNLOAD_DIRECTORY_COMPLETE,
}

public class EventData
{
}

/// <summary>
///     The application event bus.
/// </summary>
public class EventBus
{
    public async Task RaiseAsync<T>(EventType type, T data)
    {

    }
}