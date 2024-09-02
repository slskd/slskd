// <copyright file="EventRecord.cs" company="slskd Team">
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

namespace slskd.Events;

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

[Index(nameof(Timestamp))]
[Index(nameof(Type))]
public record EventRecord
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Type { get; private set; }
    public string Data { get; init; }
    [Key]
    public Guid Id { get; private set; }

    private static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    /// <summary>
    ///     Converts the specified event <paramref name="e"/> into an instance of <see cref="EventRecord"/>
    ///     for the purpose of database storage.
    /// </summary>
    /// <remarks>
    ///     The <see cref="Data"/> property contains the event-specific data serialized as json, applying
    ///     standard formatting such as camel cased property names and null value omission. This is important so that
    ///     the values stored in the database will match the values exposed through other means. Maybe not *that* important,
    ///     but standardizing json is a good practice.
    /// </remarks>
    /// <param name="e">The Event to convert.</param>
    /// <typeparam name="T">The specific type of the Event.</typeparam>
    /// <returns>The converted EventRecord.</returns>
    public static EventRecord From<T>(Event e)
        where T : Event
    {
        // this will be 'slskd.NameOfEvent'; we want to chop off 'slskd.' and 'Event' = 'NameOf'
        var type = e.GetType().Name.Split('.').TakeLast(1).First();
        type = type.Substring(0, type.Length - nameof(Event).Length);

        // construct the data for the record by serializing the event and removing redundant properties
        var json = JsonSerializer.Serialize(e as T, JsonSerializerOptions);
        var data = JsonNode.Parse(json).AsObject();
        data.Remove(nameof(Timestamp).ToLower());
        data.Remove(nameof(Type).ToLower());
        data.Remove(nameof(Id).ToLower());
        data.Remove(nameof(Data).ToLower());

        return new EventRecord
        {
            Timestamp = e.Timestamp,
            Type = type,
            Id = e.Id,
            Data = data.ToJsonString(JsonSerializerOptions),
        };
    }
}