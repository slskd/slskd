namespace slskd.Tests.Unit.Search;

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Search;
using slskd.Search.API;
using Soulseek;
using Xunit;

public class SearchServicePaginationTests : IDisposable
{
    private readonly SqliteConnection _anchor;
    private readonly DbContextOptions<SearchDbContext> _contextOptions;
    private readonly SearchService _service;

    public SearchServicePaginationTests()
    {
        var connectionString = $"Data Source=search_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _anchor = new SqliteConnection(connectionString);
        _anchor.Open();
        _contextOptions = new DbContextOptionsBuilder<SearchDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var context = new SearchDbContext(_contextOptions);
        context.Database.EnsureCreated();

        var contextFactory = new Mock<IDbContextFactory<SearchDbContext>>();
        contextFactory
            .Setup(factory => factory.CreateDbContext())
            .Returns(() => new SearchDbContext(_contextOptions));

        _service = new SearchService(
            new Mock<IHubContext<SearchHub>>().Object,
            new Mock<IOptionsMonitor<slskd.Options>>().Object,
            new Mock<ISoulseekClient>().Object,
            contextFactory.Object);
    }

    public void Dispose()
    {
        _anchor.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ListAsync_Returns_A_Deterministic_Page()
    {
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var oldest = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tiedFirst = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var tiedSecond = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var newest = Guid.Parse("00000000-0000-0000-0000-000000000004");

        Insert(oldest, timestamp.AddMinutes(-1));
        Insert(tiedSecond, timestamp);
        Insert(tiedFirst, timestamp);
        Insert(newest, timestamp.AddMinutes(1));

        var page = await _service.ListAsync(offset: 1, limit: 2);

        Assert.Equal(new[] { tiedFirst, tiedSecond }, page.Select(search => search.Id));
    }

    [Fact]
    public async Task ListAsync_Allows_Offset_Without_Limit()
    {
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Insert(Guid.Parse("00000000-0000-0000-0000-000000000001"), timestamp);
        Insert(Guid.Parse("00000000-0000-0000-0000-000000000002"), timestamp.AddMinutes(1));
        Insert(Guid.Parse("00000000-0000-0000-0000-000000000003"), timestamp.AddMinutes(2));

        var page = await _service.ListAsync(offset: 1, limit: null);

        Assert.Equal(2, page.Count);
    }

    [Fact]
    public async Task CountAsync_Applies_The_Filter()
    {
        var timestamp = DateTime.UtcNow;
        Insert(Guid.NewGuid(), timestamp, "keep one");
        Insert(Guid.NewGuid(), timestamp, "keep two");
        Insert(Guid.NewGuid(), timestamp, "omit");

        var count = await _service.CountAsync(search => search.SearchText.StartsWith("keep"));

        Assert.Equal(2, count);
    }

    private void Insert(Guid id, DateTime startedAt, string searchText = "search")
    {
        using var context = new SearchDbContext(_contextOptions);
        context.Searches.Add(new slskd.Search.Search
        {
            Id = id,
            SearchText = searchText,
            StartedAt = startedAt,
        });
        context.SaveChanges();
    }
}

public class SearchesControllerPaginationTests
{
    [Fact]
    public async Task GetAll_Without_Pagination_Uses_The_Existing_List_And_Adds_Count()
    {
        var searches = new[]
        {
            new slskd.Search.Search { Id = Guid.NewGuid(), SearchText = "one" },
            new slskd.Search.Search { Id = Guid.NewGuid(), SearchText = "two" },
        }.ToList();
        var service = new Mock<ISearchService>();
        service
            .Setup(searchService => searchService.ListAsync(It.IsAny<Expression<Func<slskd.Search.Search, bool>>>()))
            .ReturnsAsync(searches);
        var controller = CreateController(service.Object);

        var result = Assert.IsType<OkObjectResult>(await controller.GetAll());

        Assert.Same(searches, result.Value);
        Assert.Equal("2", controller.Response.Headers["X-Total-Count"]);
        service.Verify(searchService => searchService.CountAsync(It.IsAny<Expression<Func<slskd.Search.Search, bool>>>()), Times.Never);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, 0)]
    public async Task GetAll_Rejects_Invalid_Pagination(int? offset, int? limit)
    {
        var controller = CreateController(new Mock<ISearchService>().Object);

        var result = await controller.GetAll(offset, limit);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static SearchesController CreateController(ISearchService service)
    {
        return new SearchesController(service, new Mock<IOptionsSnapshot<slskd.Options>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }
}
