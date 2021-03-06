﻿// <copyright file="IFTPService.cs" company="slskd Team">
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

namespace slskd.Integrations.FTP
{
    using System.Threading.Tasks;

    /// <summary>
    ///     FTP Integration service.
    /// </summary>
    public interface IFTPService
    {
        /// <summary>
        ///     Uploads the specified <paramref name="filename"/> to the configured FTP server.
        /// </summary>
        /// <param name="filename">The fully qualified name of the file to upload.</param>
        /// <returns>The operation context.</returns>
        Task UploadAsync(string filename);
    }
}
