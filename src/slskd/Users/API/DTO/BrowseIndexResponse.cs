// <copyright file="BrowseIndexResponse.cs" company="JP Dillingham">
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

namespace slskd.Users.API
{
    using System.Collections.Generic;
    using System.Linq;
    using Soulseek;

    public record BrowseIndexInfo
    {
        public int Directories { get; init; }
        public int Files { get; init; }
        public int LockedDirectories { get; init; }
        public int LockedFiles { get; init; }
    }

    public record BrowseIndexDirectory
    {
        public string Name { get; init; }
        public int FileCount { get; init; }

        public static BrowseIndexDirectory FromSoulseek(Directory directory)
        {
            return new BrowseIndexDirectory
            {
                Name = directory.Name,
                FileCount = directory.FileCount,
            };
        }
    }

    public record BrowseIndexResponse
    {
        public BrowseIndexInfo Info { get; init; }
        public IReadOnlyCollection<BrowseIndexDirectory> Directories { get; init; }
        public IReadOnlyCollection<BrowseIndexDirectory> LockedDirectories { get; init; }

        public static BrowseIndexResponse FromSoulseek(BrowseResponse response)
        {
            var directories = (response?.Directories ?? Enumerable.Empty<Directory>()).ToList();

            return new BrowseIndexResponse
            {
                Info = new BrowseIndexInfo
                {
                    Directories = directories.Count,
                    Files = directories.Sum(directory => directory.FileCount),
                    LockedDirectories = 0,
                    LockedFiles = 0,
                },
                Directories = directories.Select(BrowseIndexDirectory.FromSoulseek).ToList(),
                LockedDirectories = new List<BrowseIndexDirectory>(),
            };
        }
    }
}
