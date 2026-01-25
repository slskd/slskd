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

    public class OptionsPatch
    {
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
            public string? ListenIpAddress { get; init; } = "0.0.0.0";

            /// <summary>
            ///     Gets the port on which to listen for incoming connections.
            /// </summary>
            [Range(1024, 65535)]
            public int? ListenPort { get; init; } = 50300;
        }
    }
}