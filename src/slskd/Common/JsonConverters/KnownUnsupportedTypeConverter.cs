// <copyright file="KnownUnsupportedTypeConverter.cs" company="slskd Team">
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
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Convert all of the Types System.Text.Json refuses to serialize to a simple string containing
///     the name of the type.
/// </summary>
public class KnownUnsupportedTypeConverter : JsonConverter<object>
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(Type).IsAssignableFrom(typeToConvert)
            || typeof(Assembly).IsAssignableFrom(typeToConvert)
            || typeof(MemberInfo).IsAssignableFrom(typeToConvert) // covers MethodBase, MethodInfo, ConstructorInfo, FieldInfo, PropertyInfo, EventInfo
            || typeof(Module).IsAssignableFrom(typeToConvert)
            || typeof(Delegate).IsAssignableFrom(typeToConvert) // covers Action, Func, EventHandler, MulticastDelegate
            || typeof(SerializationInfo).IsAssignableFrom(typeToConvert)
            || typeToConvert == typeof(IntPtr)
            || typeToConvert == typeof(UIntPtr);
    }

    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Skip();
        return default;
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value?.GetType().FullName ?? "null");
    }
}
