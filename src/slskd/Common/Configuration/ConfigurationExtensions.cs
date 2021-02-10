// <copyright file="ConfigurationExtensions.cs" company="slskd Team">
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

namespace slskd.Configuration
{
    /// <summary>
    ///     Extension methods for the Configuration namespace.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        ///     Converts an <see cref="Option"/> mapping to a <see cref="CommandLineArgument"/> mapping.
        /// </summary>
        /// <param name="option">The option mapping to convert.</param>
        /// <returns>The converted mapping.</returns>
        public static CommandLineArgument ToCommandLineArgument(this Option option)
            => new(option.ShortName, option.LongName, option.Type, option.Key, option.Description);

        /// <summary>
        ///     Converts an <see cref="Option"/> mapping to a <see cref="EnvironmentVariable"/> mapping.
        /// </summary>
        /// <param name="option">The option mapping to convert.</param>
        /// <returns>The converted mapping.</returns>
        public static EnvironmentVariable ToEnvironmentVariable(this Option option)
            => new(option.EnvironmentVariable, option.Type, option.Key, option.Description);

        /// <summary>
        ///     Converts an <see cref="Option"/> mapping to a <see cref="DefaultValue"/> mapping.
        /// </summary>
        /// <param name="option">The option mapping to convert.</param>
        /// <returns>The converted mapping.</returns>
        public static DefaultValue ToDefaultValue(this Option option)
            => new(option.Key, option.Type, option.Default);
    }
}
