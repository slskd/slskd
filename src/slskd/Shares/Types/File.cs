// <copyright file="File.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System.Collections.Generic;
    using Soulseek;

    public class File : Soulseek.File
    {
        public File(int code, string filename, long size, string extension, IEnumerable<FileAttribute> attributeList = null, string originalFilename = null)
            : base(code, filename, size, extension, attributeList)
        {
            OriginalFilename = originalFilename;
        }

        public string OriginalFilename { get; set; }
    }
}
