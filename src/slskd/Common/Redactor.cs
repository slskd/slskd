// <copyright file="Redactor.cs" company="slskd Team">
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
    using System.Collections;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    ///     Redacts secrets within objects.
    /// </summary>
    public static class Redactor
    {
        /// <summary>
        ///     Recursively scans the <paramref name="target"/> for properties marked with <see cref="SecretAttribute"/> and redacts the values by overwriting them.
        /// </summary>
        /// <remarks>
        ///     Only works on properties of type <see cref="string"/>.
        /// </remarks>
        /// <param name="target">The object to redact.</param>
        /// <param name="redactWith">The string with which to replace redacted values.</param>
        public static void Redact(object target, string redactWith = "*****")
        {
            var type = target.GetType();

            if (type.IsPrimitive || type.IsValueType || type == typeof(string) || type.IsAssignableTo(typeof(IEnumerable)))
            {
                return;
            }

            foreach (var prop in target.GetType().GetProperties())
            {
                var value = prop.GetValue(target);

                if (value == null)
                {
                    continue;
                }

                if (prop.GetCustomAttributes().Any(attr => attr.GetType() == typeof(SecretAttribute)) && prop.PropertyType == typeof(string))
                {
                    prop.SetValue(target, redactWith);
                }
                else
                {
                    if (value.GetType().IsAssignableTo(typeof(IEnumerable)))
                    {
                        foreach (var element in (IEnumerable)value)
                        {
                            Redact(element);
                        }
                    }
                    else
                    {
                        Redact(value);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Indicates that a property or field contains a secret, allowing it to be redacted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SecretAttribute : Attribute
    {
    }
}
