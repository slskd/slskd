using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Integrations.MusicBrainz;
using Xunit;

namespace slskd.Tests.Unit.Integrations;

public sealed class SavedReleaseServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"slskd-saved-releases-{Guid.NewGuid()}");

    private string Filename => Path.Combine(directory, "releases.json");

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private SavedReleaseService Create() => new(NullLogger<SavedReleaseService>.Instance, Filename);

    private static SavedRelease Release(string title, Guid? searchId = null) => new()
    {
        Release = new MusicBrainzReleaseSummary { Id = Guid.NewGuid(), Title = title, Artist = "Artist" },
        Query = title.ToLowerInvariant(),
        SearchId = searchId,
    };

    [Fact]
    public async Task Saves_Newest_First_And_Keeps_Them_Across_Restarts()
    {
        using (var service = Create())
        {
            await service.SaveAsync(Release("First"));
            await service.SaveAsync(Release("Second"));
        }

        using var reloaded = Create();
        Assert.Equal(["Second", "First"], reloaded.List().Select(saved => saved.Release.Title));
    }

    [Fact]
    public async Task Replaces_An_Earlier_Save_Of_The_Same_Release()
    {
        using var service = Create();
        var release = Release("Album");
        await service.SaveAsync(release);
        await service.SaveAsync(Release("Other"));

        var searchId = Guid.NewGuid();
        await service.SaveAsync(release with { SearchId = searchId });

        Assert.Equal(2, service.List().Count);
        Assert.Equal(searchId, service.List()[0].SearchId);
        Assert.Equal(searchId, service.Find(release.Release.Id).SearchId);
    }

    [Fact]
    public async Task Removes_Some_Or_All()
    {
        using var service = Create();
        var keep = Release("Keep");
        var drop = Release("Drop");
        await service.SaveAsync(keep);
        await service.SaveAsync(drop);

        var removed = await service.RemoveAsync([drop.Release.Id]);
        Assert.Equal("Drop", Assert.Single(removed).Release.Title);
        Assert.Null(service.Find(drop.Release.Id));

        await service.RemoveAsync();
        Assert.Empty(service.List());
        Assert.Empty(Create().List());
    }

    [Fact]
    public async Task Rejects_A_Release_Without_An_Id()
    {
        using var service = Create();
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new SavedRelease { Release = new MusicBrainzReleaseSummary() }));
    }

    [Fact]
    public void Starts_Empty_When_The_File_Is_Corrupt()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Filename, "{ not json");

        using var service = Create();
        Assert.Empty(service.List());
    }
}
