// <copyright file="OptionsPatch.cs" company="slskd Team">
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
    using System.ComponentModel.DataAnnotations;
    using slskd.Validation;

    /// <summary>
    ///     Run-time patch for application <see cref="Options"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The values specified in this patch are applied at run-time only, are volatile (lost when the application restarts),
    ///         and take precedence over all other options, regardless of which method was used to define them.
    ///     </para>
    ///     <para>
    ///         Only options that can be applied while the application is running can be patched, given the nature of how
    ///         a patch is applied.
    ///     </para>
    ///     <para>
    ///         Every property in this class must be nullable, and must have a null default value; the application
    ///         selectively applies the patch using only information explicitly supplied.
    ///     </para>
    /// </remarks>
    public class OptionsPatch
    {
        public static OptionsPatch Current { get; private set; } = new();

        [Validate]
        public SoulseekOptionsPatch Soulseek { get; init; } = null;

        public static void SetCurrent(OptionsPatch patch) => Current = patch;

        /// <summary>
        ///     Soulseek client options.
        /// </summary>
        public class SoulseekOptionsPatch
        {
            /// <summary>
            ///     Gets the local IP address on which to listen for incoming connections.
            /// </summary>
            [IPAddress]
#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
            public string? ListenIpAddress { get; init; } = null;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

            /// <summary>
            ///     Gets the port on which to listen for incoming connections.
            /// </summary>
            [Range(1024, 65535)]
            public int? ListenPort { get; init; } = null;
        }
    }
}