// <copyright file="OptionsOverlay.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
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
    ///         selectively applies the patch overlay only to information that is explicitly supplied.
    ///     </para>
    /// </remarks>
    public record OptionsOverlay
    {
        /// <summary>
        ///     Gets options for the Soulseek client.
        /// </summary>
        [Validate]
        public SoulseekOptionsPatch Soulseek { get; init; } = null;

        /// <summary>
        ///     Soulseek client options.
        /// </summary>
        public record SoulseekOptionsPatch
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