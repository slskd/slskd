// <copyright file="Types.cs" company="JP Dillingham">
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

    /// <summary>
    ///     A release, as listed in search results.
    /// </summary>
    public record MusicBrainzReleaseSummary
    {
        /// <summary>
        ///     Gets the release MBID.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        ///     Gets the release title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        ///     Gets the credited artist, as displayed.
        /// </summary>
        public string Artist { get; init; }

        /// <summary>
        ///     Gets the MBIDs of the credited artists.
        /// </summary>
        public IReadOnlyList<Guid> ArtistIds { get; init; } = [];

        /// <summary>
        ///     Gets the text MusicBrainz uses to tell otherwise identical releases apart.
        /// </summary>
        public string Disambiguation { get; init; }

        /// <summary>
        ///     Gets the release date; a year, a year and month, or a full date.
        /// </summary>
        public string Date { get; init; }

        /// <summary>
        ///     Gets the release country code.
        /// </summary>
        public string Country { get; init; }

        /// <summary>
        ///     Gets the release status, such as Official or Bootleg.
        /// </summary>
        public string Status { get; init; }

        /// <summary>
        ///     Gets the primary type of the release group, such as Album or EP.
        /// </summary>
        public string Type { get; init; }

        /// <summary>
        ///     Gets the release group MBID.
        /// </summary>
        public Guid? ReleaseGroupId { get; init; }

        /// <summary>
        ///     Gets the media, summarised, such as "2×CD" or "CD + DVD-Video".
        /// </summary>
        public string Format { get; init; }

        /// <summary>
        ///     Gets the total number of tracks.
        /// </summary>
        public int TrackCount { get; init; }

        /// <summary>
        ///     Gets the first label.
        /// </summary>
        public string Label { get; init; }

        /// <summary>
        ///     Gets the first catalog number.
        /// </summary>
        public string CatalogNumber { get; init; }

        /// <summary>
        ///     Gets the barcode.
        /// </summary>
        public string Barcode { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the Cover Art Archive has a front cover, or null if unknown.
        /// </summary>
        public bool? HasFrontCover { get; init; }

        /// <summary>
        ///     Gets the search score, from 0 to 100, for releases from search results.
        /// </summary>
        public int? Score { get; init; }
    }

    /// <summary>
    ///     A release with its track list.
    /// </summary>
    public record MusicBrainzRelease : MusicBrainzReleaseSummary
    {
        /// <summary>
        ///     Gets the media (discs, sides, and so on) and their tracks.
        /// </summary>
        public IReadOnlyList<MusicBrainzMedium> Media { get; init; } = [];
    }

    /// <summary>
    ///     A medium of a release, such as one disc.
    /// </summary>
    public record MusicBrainzMedium
    {
        /// <summary>
        ///     Gets the position of the medium within the release, starting at 1.
        /// </summary>
        public int Position { get; init; }

        /// <summary>
        ///     Gets the format, such as CD or Digital Media.
        /// </summary>
        public string Format { get; init; }

        /// <summary>
        ///     Gets the medium title, if it has one.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        ///     Gets the tracks.
        /// </summary>
        public IReadOnlyList<MusicBrainzTrack> Tracks { get; init; } = [];
    }

    /// <summary>
    ///     A track on a medium.
    /// </summary>
    public record MusicBrainzTrack
    {
        /// <summary>
        ///     Gets the track MBID.
        /// </summary>
        public Guid Id { get; init; }

        /// <summary>
        ///     Gets the recording MBID.
        /// </summary>
        public Guid? RecordingId { get; init; }

        /// <summary>
        ///     Gets the position of the track on its medium, starting at 1.
        /// </summary>
        public int Position { get; init; }

        /// <summary>
        ///     Gets the track number as printed, such as "3" or "A3".
        /// </summary>
        public string Number { get; init; }

        /// <summary>
        ///     Gets the track title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        ///     Gets the credited artist, as displayed.
        /// </summary>
        public string Artist { get; init; }

        /// <summary>
        ///     Gets the length in milliseconds, if known.
        /// </summary>
        public int? Length { get; init; }
    }

    /// <summary>
    ///     A page of release search results.
    /// </summary>
    public record MusicBrainzReleaseSearchResult
    {
        /// <summary>
        ///     Gets the total number of matching releases.
        /// </summary>
        public int Count { get; init; }

        /// <summary>
        ///     Gets the offset of this page.
        /// </summary>
        public int Offset { get; init; }

        /// <summary>
        ///     Gets the releases on this page.
        /// </summary>
        public IReadOnlyList<MusicBrainzReleaseSummary> Releases { get; init; } = [];
    }

    /// <summary>
    ///     A cover image.
    /// </summary>
    /// <param name="Data">The image bytes.</param>
    /// <param name="ContentType">The image content type.</param>
    /// <param name="Source">Where the image came from.</param>
    public record CoverImage(byte[] Data, string ContentType, string Source);

    /// <summary>
    ///     A release that was searched for on Soulseek.
    /// </summary>
    public record SavedRelease
    {
        /// <summary>
        ///     Gets the release.
        /// </summary>
        public MusicBrainzReleaseSummary Release { get; init; }

        /// <summary>
        ///     Gets the ID of the latest Soulseek search for the release.
        /// </summary>
        public Guid? SearchId { get; init; }

        /// <summary>
        ///     Gets the text of the latest Soulseek search for the release.
        /// </summary>
        public string Query { get; init; }

        /// <summary>
        ///     Gets when the release was last searched for.
        /// </summary>
        public DateTime SavedAt { get; init; }
    }
}
