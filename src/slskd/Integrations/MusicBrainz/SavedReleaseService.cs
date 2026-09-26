// <copyright file="SavedReleaseService.cs" company="JP Dillingham">
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

namespace slskd.Integrations.MusicBrainz
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Keeps the releases that were searched for on Soulseek, like the search list keeps searches.
    /// </summary>
    public interface ISavedReleaseService
    {
        /// <summary>
        ///     Lists the saved releases, most recent first.
        /// </summary>
        /// <returns>The saved releases.</returns>
        IReadOnlyList<SavedRelease> List();

        /// <summary>
        ///     Finds a saved release.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <returns>The saved release, or null if it isn't saved.</returns>
        SavedRelease Find(Guid id);

        /// <summary>
        ///     Saves a release, replacing any earlier save of it.
        /// </summary>
        /// <param name="release">The release to save.</param>
        /// <returns>The operation context, including the saved release.</returns>
        Task<SavedRelease> SaveAsync(SavedRelease release);

        /// <summary>
        ///     Removes saved releases.
        /// </summary>
        /// <param name="ids">The release MBIDs, or null to remove every release.</param>
        /// <returns>The operation context, including the releases removed.</returns>
        Task<IReadOnlyList<SavedRelease>> RemoveAsync(IEnumerable<Guid> ids = null);
    }

    /// <summary>
    ///     Keeps the releases that were searched for on Soulseek in a JSON file in the data directory.
    /// </summary>
    public sealed class SavedReleaseService : ISavedReleaseService, IDisposable
    {
        private const int MaximumReleases = 500;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        /// <summary>
        ///     Initializes a new instance of the <see cref="SavedReleaseService"/> class.
        /// </summary>
        /// <param name="log">The logger.</param>
        /// <param name="filename">The file to keep releases in; defaults to releases.json in the data directory.</param>
        public SavedReleaseService(ILogger<SavedReleaseService> log, string filename = null)
        {
            Log = log;
            Filename = filename ?? Path.Combine(Program.DataDirectory, "releases.json");
            Releases = Load();
        }

        private string Filename { get; }
        private ILogger<SavedReleaseService> Log { get; }
        private List<SavedRelease> Releases { get; set; }
        private SemaphoreSlim WriteLock { get; } = new SemaphoreSlim(1, 1);

        /// <summary>
        ///     Lists the saved releases, most recent first.
        /// </summary>
        /// <returns>The saved releases.</returns>
        public IReadOnlyList<SavedRelease> List() => Releases;

        /// <summary>
        ///     Finds a saved release.
        /// </summary>
        /// <param name="id">The release MBID.</param>
        /// <returns>The saved release, or null if it isn't saved.</returns>
        public SavedRelease Find(Guid id) => Releases.FirstOrDefault(saved => saved.Release.Id == id);

        /// <summary>
        ///     Saves a release, replacing any earlier save of it.
        /// </summary>
        /// <param name="release">The release to save.</param>
        /// <returns>The operation context, including the saved release.</returns>
        public async Task<SavedRelease> SaveAsync(SavedRelease release)
        {
            if (release?.Release is null || release.Release.Id == Guid.Empty)
            {
                throw new ArgumentException("A release with an ID is required", nameof(release));
            }

            var saved = release with { SavedAt = DateTime.UtcNow };

            await WriteLock.WaitAsync();

            try
            {
                // lists are replaced, never changed, so List() never sees a list being modified
                Releases = Releases
                    .Where(existing => existing.Release.Id != saved.Release.Id)
                    .Prepend(saved)
                    .Take(MaximumReleases)
                    .ToList();

                await WriteAsync();
                return saved;
            }
            finally
            {
                WriteLock.Release();
            }
        }

        /// <summary>
        ///     Removes saved releases.
        /// </summary>
        /// <param name="ids">The release MBIDs, or null to remove every release.</param>
        /// <returns>The operation context, including the releases removed.</returns>
        public async Task<IReadOnlyList<SavedRelease>> RemoveAsync(IEnumerable<Guid> ids = null)
        {
            await WriteLock.WaitAsync();

            try
            {
                var remove = ids is null ? null : new HashSet<Guid>(ids);
                var removed = Releases.Where(saved => remove is null || remove.Contains(saved.Release.Id)).ToList();

                if (removed.Count > 0)
                {
                    Releases = Releases.Except(removed).ToList();
                    await WriteAsync();
                }

                return removed;
            }
            finally
            {
                WriteLock.Release();
            }
        }

        /// <summary>
        ///     Releases the write lock.
        /// </summary>
        public void Dispose()
        {
            WriteLock.Dispose();
        }

        private List<SavedRelease> Load()
        {
            try
            {
                if (!File.Exists(Filename))
                {
                    return [];
                }

                var releases = JsonSerializer.Deserialize<List<SavedRelease>>(File.ReadAllText(Filename), JsonOptions) ?? [];
                return releases.Where(saved => saved?.Release is not null && saved.Release.Id != Guid.Empty).ToList();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                Log.LogWarning("Failed to read saved releases from {Filename}; starting with none: {Message}", Filename, ex.Message);
                return [];
            }
        }

        private async Task WriteAsync()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Filename));

            // write a new file and swap it in, so a crash mid-write can't leave half a file
            var temporary = $"{Filename}.tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(Releases, JsonOptions));
            File.Move(temporary, Filename, overwrite: true);
        }
    }
}
