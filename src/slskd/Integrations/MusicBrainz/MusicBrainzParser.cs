// <copyright file="MusicBrainzParser.cs" company="JP Dillingham">
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
    using System.Linq;
    using System.Text;
    using System.Text.Json.Nodes;

    /// <summary>
    ///     Converts MusicBrainz web service JSON into slskd's types.
    /// </summary>
    public static class MusicBrainzParser
    {
        /// <summary>
        ///     Parses the response to a release search.
        /// </summary>
        /// <param name="node">The response.</param>
        /// <returns>The search result.</returns>
        public static MusicBrainzReleaseSearchResult ParseReleaseSearch(JsonNode node)
        {
            return new MusicBrainzReleaseSearchResult
            {
                Count = Integer(node?["count"]) ?? 0,
                Offset = Integer(node?["offset"]) ?? 0,
                Releases = Items(node?["releases"])
                    .Select(release => Summarize(release))
                    .Where(release => release is not null)
                    .ToList(),
            };
        }

        /// <summary>
        ///     Parses the response to a release lookup that included recordings and artist credits.
        /// </summary>
        /// <param name="node">The response.</param>
        /// <returns>The release, or null if the response did not describe one.</returns>
        public static MusicBrainzRelease ParseRelease(JsonNode node)
        {
            var summary = Summarize(node);

            if (summary is null)
            {
                return null;
            }

            var media = Items(node["media"])
                .Select((medium, index) => new MusicBrainzMedium
                {
                    Position = Integer(medium["position"]) ?? index + 1,
                    Format = Text(medium["format"]),
                    Title = NullIfEmpty(Text(medium["title"])),
                    Tracks = Items(medium["tracks"])
                        .Select((track, trackIndex) => ParseTrack(track, trackIndex, summary.Artist))
                        .Where(track => track is not null)
                        .ToList(),
                })
                .ToList();

            return new MusicBrainzRelease
            {
                Id = summary.Id,
                Title = summary.Title,
                Artist = summary.Artist,
                ArtistIds = summary.ArtistIds,
                Disambiguation = summary.Disambiguation,
                Date = summary.Date,
                Country = summary.Country,
                Status = summary.Status,
                Type = summary.Type,
                ReleaseGroupId = summary.ReleaseGroupId,
                Format = summary.Format,
                TrackCount = media.Sum(medium => medium.Tracks.Count) is var count && count > 0 ? count : summary.TrackCount,
                Label = summary.Label,
                CatalogNumber = summary.CatalogNumber,
                Barcode = summary.Barcode,
                HasFrontCover = summary.HasFrontCover,
                Score = summary.Score,
                Media = media,
            };
        }

        /// <summary>
        ///     Joins an artist credit into the name as it is displayed, such as "Artist feat. Other".
        /// </summary>
        /// <param name="credit">The artist-credit array.</param>
        /// <returns>The display name, or null if there is no credit.</returns>
        public static string ArtistCredit(JsonNode credit)
        {
            var builder = new StringBuilder();

            foreach (var part in Items(credit))
            {
                builder.Append(Text(part["name"]) ?? Text(part["artist"]?["name"]));
                builder.Append(Text(part["joinphrase"]));
            }

            return NullIfEmpty(builder.ToString().Trim());
        }

        /// <summary>
        ///     Summarises media formats, such as "2×CD" or "CD + DVD-Video".
        /// </summary>
        /// <param name="media">The media array.</param>
        /// <returns>The summary, or null if no medium has a format.</returns>
        public static string FormatSummary(JsonNode media)
        {
            var groups = Items(media)
                .Select(medium => Text(medium["format"]) ?? "(unknown)")
                .GroupBy(format => format)
                .Select(group => group.Count() > 1 ? $"{group.Count()}×{group.Key}" : group.Key)
                .ToList();

            return groups.Count == 0 || groups.All(group => group.EndsWith("(unknown)", StringComparison.Ordinal))
                ? null
                : string.Join(" + ", groups);
        }

        private static MusicBrainzReleaseSummary Summarize(JsonNode node)
        {
            if (node is null || !Guid.TryParse(Text(node["id"]), out var id))
            {
                return null;
            }

            var labelInfo = Items(node["label-info"]).FirstOrDefault();
            var releaseGroup = node["release-group"];

            var trackCount = Integer(node["track-count"])
                ?? Items(node["media"]).Sum(medium => Integer(medium["track-count"]) ?? Items(medium["tracks"]).Count());

            return new MusicBrainzReleaseSummary
            {
                Id = id,
                Title = Text(node["title"]),
                Artist = ArtistCredit(node["artist-credit"]),
                ArtistIds = Items(node["artist-credit"])
                    .Select(part => Guid.TryParse(Text(part["artist"]?["id"]), out var artistId) ? artistId : Guid.Empty)
                    .Where(artistId => artistId != Guid.Empty)
                    .ToList(),
                Disambiguation = NullIfEmpty(Text(node["disambiguation"])),
                Date = NullIfEmpty(Text(node["date"])),
                Country = NullIfEmpty(Text(node["country"])),
                Status = NullIfEmpty(Text(node["status"])),
                Type = NullIfEmpty(Text(releaseGroup?["primary-type"])),
                ReleaseGroupId = Guid.TryParse(Text(releaseGroup?["id"]), out var groupId) ? groupId : null,
                Format = FormatSummary(node["media"]),
                TrackCount = trackCount,
                Label = NullIfEmpty(Text(labelInfo?["label"]?["name"])),
                CatalogNumber = NullIfEmpty(Text(labelInfo?["catalog-number"])),
                Barcode = NullIfEmpty(Text(node["barcode"])),
                HasFrontCover = Flag(node["cover-art-archive"]?["front"]),
                Score = Integer(node["score"]),
            };
        }

        private static MusicBrainzTrack ParseTrack(JsonNode track, int index, string releaseArtist)
        {
            if (track is null)
            {
                return null;
            }

            var recording = track["recording"];

            return new MusicBrainzTrack
            {
                Id = Guid.TryParse(Text(track["id"]), out var id) ? id : Guid.Empty,
                RecordingId = Guid.TryParse(Text(recording?["id"]), out var recordingId) ? recordingId : null,
                Position = Integer(track["position"]) ?? index + 1,
                Number = Text(track["number"]) ?? (index + 1).ToString(),
                Title = Text(track["title"]) ?? Text(recording?["title"]),
                Artist = ArtistCredit(track["artist-credit"]) ?? ArtistCredit(recording?["artist-credit"]) ?? releaseArtist,
                Length = Integer(track["length"]) ?? Integer(recording?["length"]),
            };
        }

        private static IEnumerable<JsonNode> Items(JsonNode node)
            => node is JsonArray array ? array.Where(item => item is not null) : [];

        private static string Text(JsonNode node)
            => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

        private static int? Integer(JsonNode node)
        {
            if (node is not JsonValue value)
            {
                return null;
            }

            if (value.TryGetValue<int>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<long>(out var big))
            {
                return (int)Math.Clamp(big, int.MinValue, int.MaxValue);
            }

            if (value.TryGetValue<double>(out var real))
            {
                return (int)Math.Round(real);
            }

            return value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed) ? parsed : null;
        }

        private static bool? Flag(JsonNode node)
            => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

        private static string NullIfEmpty(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
