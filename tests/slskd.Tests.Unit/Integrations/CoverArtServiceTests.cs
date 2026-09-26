using slskd.Integrations.MusicBrainz;
using Xunit;

namespace slskd.Tests.Unit.Integrations;

public class CoverArtServiceTests
{
    [Theory]
    [InlineData("Björk", "bjork")]
    [InlineData("Selected Ambient Works 85–92", "selected ambient works 85 92")]
    [InlineData("OK Computer (Remastered)", "ok computer")]
    [InlineData("Simon & Garfunkel", "simon and garfunkel")]
    [InlineData("  ", "")]
    public void Simplifies_Names(string name, string expected)
    {
        Assert.Equal(expected, CoverArtService.Simplify(name));
    }

    [Theory]
    [InlineData("Radiohead", "OK Computer", "Radiohead", "OK Computer", true)]
    [InlineData("Radiohead", "OK Computer OKNOTOK 1997 2017", "Radiohead", "OK Computer", true)]
    [InlineData("Radiohead", "OK Computer [Deluxe Edition]", "Radiohead", "OK Computer", true)]
    [InlineData("Aphex Twin", "Selected Ambient Works 85-92", "Aphex Twin", "Selected Ambient Works 85–92", true)]
    [InlineData("Radiohead", "Kid A", "Radiohead", "OK Computer", false)]
    [InlineData("Radiohead Tribute Band", "OK Computer", "Radiohead", "OK Computer", true)]
    [InlineData("The Radiohead Tribute", "OK Computer", "Radiohead", "OK Computer", false)]
    [InlineData("Radiohead", "OK", "Radiohead", "OK Computer", true)]
    [InlineData("Radiohead", "OKC", "Radiohead", "OK Computer", false)]
    [InlineData("", "OK Computer", "Radiohead", "OK Computer", false)]
    public void Decides_Whether_An_Album_Is_The_Release(string foundArtist, string foundTitle, string artist, string title, bool expected)
    {
        Assert.Equal(expected, CoverArtService.IsSameAlbum(foundArtist, foundTitle, artist, title));
    }
}
