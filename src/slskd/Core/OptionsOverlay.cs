// <copyright file="OptionsOverlay.cs" company="slskd Team">
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
    ///     Volatile run-time overlay for application <see cref="Options"/>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The values specified in this overlay are applied at run-time only, are volatile (lost when the application restarts),
    ///         and take precedence over all other options, regardless of which method was used to define them.
    ///     </para>
    ///     <para>
    ///         Only options that can be applied while the application is running can be overlaid, given the nature of how
    ///         an overlay is applied.
    ///     </para>
    ///     <para>
    ///         Every property in this class must be nullable, and must have a null default value; the application
    ///         selectively applies the patch overlay only information explicitly supplied.
    ///     </para>
    /// </remarks>
    public class OptionsOverlay
    {
        /// <summary>
        ///     Gets options for the Soulseek client.
        /// </summary>
        [Validate]
        public SoulseekOptionsPatch Soulseek { get; init; } = null;

        /// <summary>
        ///     Soulseek client options.
        /// </summary>
        public class SoulseekOptionsPatch
        {
            /// <summary>
            ///     Gets the local IP address on which to listen for incoming connections.
            /// </summary>
            [IPAddress]
            public string ListenIpAddress { get; init; } = null;

            /// <summary>
            ///     Gets the port on which to listen for incoming connections.
            /// </summary>
            [Range(1024, 65535)]
            public int? ListenPort { get; init; } = null;
        }
    }
}