using System;
using System.Linq;
using System.Text.Json.Nodes;
using slskd.Integrations.MusicBrainz;
using Xunit;

namespace slskd.Tests.Unit.Integrations;

public class MusicBrainzParserTests
{
    // trimmed from a real /ws/2/release/{id}?inc=recordings+artist-credits+labels+release-groups response
    private const string Release = """
        {
          "id": "b1c1a5a2-4b0e-4c56-9d1c-0f4a4a2f8f01",
          "title": "Selected Ambient Works 85-92",
          "date": "1992-11-09",
          "country": "BE",
          "status": "Official",
          "barcode": "",
          "disambiguation": "",
          "cover-art-archive": { "front": true },
          "artist-credit": [
            { "name": "Aphex Twin", "joinphrase": "", "artist": { "id": "f22942a1-6f70-4f48-866e-238cb2308fbd", "name": "Aphex Twin" } }
          ],
          "release-group": { "id": "a6a1e2d0-9e0c-3a2c-a6f2-2b0f0d3d3b3b", "primary-type": "Album" },
          "label-info": [ { "catalog-number": "AMB 3922", "label": { "name": "Apollo" } } ],
          "media": [
            {
              "position": 1,
              "format": "CD",
              "title": "",
              "track-count": 2,
              "tracks": [
                { "id": "0b8e2c9e-1d5b-4d5b-9a9e-1f7d8b0c0a01", "position": 1, "number": "1", "title": "Xtal", "length": 294000,
                  "recording": { "id": "5b2b2e5e-3f1a-4c3e-8c56-7d5e0b6d1a01", "title": "Xtal", "length": 293000 } },
                { "id": "0b8e2c9e-1d5b-4d5b-9a9e-1f7d8b0c0a02", "position": 2, "number": "2", "title": "Tha", "length": null,
                  "artist-credit": [
                    { "name": "Aphex Twin", "joinphrase": " feat. " },
                    { "name": "Someone", "joinphrase": "" }
                  ],
                  "recording": { "id": "5b2b2e5e-3f1a-4c3e-8c56-7d5e0b6d1a02", "title": "Tha", "length": 545000 } }
              ]
            },
            {
              "position": 2,
              "format": "CD",
              "tracks": [
                { "id": "0b8e2c9e-1d5b-4d5b-9a9e-1f7d8b0c0a03", "number": "1", "title": "Pulsewidth" }
              ]
            }
          ]
        }
        """;

    private const string Search = """
        {
          "count": 57,
          "offset": 25,
          "releases": [
            {
              "id": "b1c1a5a2-4b0e-4c56-9d1c-0f4a4a2f8f01",
              "score": 100,
              "title": "Selected Ambient Works 85-92",
              "track-count": 13,
              "artist-credit": [ { "name": "Aphex Twin", "artist": { "id": "f22942a1-6f70-4f48-866e-238cb2308fbd" } } ],
              "media": [ { "format": "Digital Media", "track-count": 13 } ]
            },
            { "id": "not a guid", "title": "Broken" },
            {
              "id": "c2c1a5a2-4b0e-4c56-9d1c-0f4a4a2f8f02",
              "title": "Unknown",
              "media": [ { "track-count": 4 }, { "track-count": 5 } ]
            }
          ]
        }
        """;

    [Fact]
    public void Parses_Release_Details()
    {
        var release = MusicBrainzParser.ParseRelease(JsonNode.Parse(Release));

        Assert.Equal(Guid.Parse("b1c1a5a2-4b0e-4c56-9d1c-0f4a4a2f8f01"), release.Id);
        Assert.Equal("Selected Ambient Works 85-92", release.Title);
        Assert.Equal("Aphex Twin", release.Artist);
        Assert.Equal([Guid.Parse("f22942a1-6f70-4f48-866e-238cb2308fbd")], release.ArtistIds);
        Assert.Equal("1992-11-09", release.Date);
        Assert.Equal("Album", release.Type);
        Assert.Equal("Apollo", release.Label);
        Assert.Equal("AMB 3922", release.CatalogNumber);
        Assert.Equal("2×CD", release.Format);
        Assert.True(release.HasFrontCover);
        Assert.Equal(3, release.TrackCount);
    }

    [Fact]
    public void Treats_Empty_Strings_As_Missing()
    {
        var release = MusicBrainzParser.ParseRelease(JsonNode.Parse(Release));

        Assert.Null(release.Barcode);
        Assert.Null(release.Disambiguation);
        Assert.Null(release.Media[0].Title);
    }

    [Fact]
    public void Parses_Tracks_With_Fallbacks()
    {
        var release = MusicBrainzParser.ParseRelease(JsonNode.Parse(Release));
        var tracks = release.Media.SelectMany(medium => medium.Tracks).ToList();

        Assert.Equal(294000, tracks[0].Length);
        Assert.Equal("Aphex Twin", tracks[0].Artist);

        // a null track length falls back to the recording's
        Assert.Equal(545000, tracks[1].Length);
        Assert.Equal("Aphex Twin feat. Someone", tracks[1].Artist);

        // a track without a position gets its index, and no length at all stays null
        Assert.Equal(2, release.Media[1].Position);
        Assert.Equal(1, tracks[2].Position);
        Assert.Null(tracks[2].Length);
        Assert.Null(tracks[2].RecordingId);
    }

    [Fact]
    public void Parses_Search_Results_And_Skips_Invalid_Releases()
    {
        var result = MusicBrainzParser.ParseReleaseSearch(JsonNode.Parse(Search));

        Assert.Equal(57, result.Count);
        Assert.Equal(25, result.Offset);
        Assert.Equal(2, result.Releases.Count);
        Assert.Equal(100, result.Releases[0].Score);
        Assert.Equal(13, result.Releases[0].TrackCount);
        Assert.Equal("Digital Media", result.Releases[0].Format);
        Assert.Null(result.Releases[0].HasFrontCover);

        // no track-count on the release; summed from the media
        Assert.Equal(9, result.Releases[1].TrackCount);
        Assert.Null(result.Releases[1].Format);
    }

    [Fact]
    public void Returns_Null_For_A_Release_Without_An_Id()
    {
        Assert.Null(MusicBrainzParser.ParseRelease(JsonNode.Parse("""{ "error": "Not Found" }""")));
    }

    [Theory]
    [InlineData("""[{ "format": "CD" }, { "format": "DVD-Video" }]""", "CD + DVD-Video")]
    [InlineData("""[{ "format": "12\" Vinyl" }, { "format": "12\" Vinyl" }, { "format": "CD" }]""", "2×12\" Vinyl + CD")]
    [InlineData("""[]""", null)]
    public void Summarises_Formats(string media, string expected)
    {
        Assert.Equal(expected, MusicBrainzParser.FormatSummary(JsonNode.Parse(media)));
    }
}
