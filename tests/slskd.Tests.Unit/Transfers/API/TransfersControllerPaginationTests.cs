namespace slskd.Tests.Unit.Transfers.API;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Transfers;
using slskd.Transfers.API;
using slskd.Transfers.Downloads;
using slskd.Transfers.Uploads;
using slskd.Users;
using Soulseek;
using Xunit;
using Transfer = slskd.Transfers.Transfer;

public class TransfersControllerPaginationTests : IDisposable
{
    private readonly SqliteConnection _anchor;
    private readonly DbContextOptions<TransfersDbContext> _contextOptions;
    private readonly Mock<IDownloadService> _downloads = new();
    private readonly TransfersController _controller;

    public TransfersControllerPaginationTests()
    {
        var connectionString = $"Data Source=transfers_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _anchor = new SqliteConnection(connectionString);
        _anchor.Open();
        _contextOptions = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var context = new TransfersDbContext(_contextOptions);
        context.Database.EnsureCreated();

        var contextFactory = new Mock<IDbContextFactory<TransfersDbContext>>();
        contextFactory
            .Setup(factory => factory.CreateDbContext())
            .Returns(() => new TransfersDbContext(_contextOptions));
        var transferService = new TransferService(
            contextFactory.Object,
            new Mock<IUploadService>().Object,
            _downloads.Object);

        _controller = new TransfersController(
            transferService,
            new Mock<IUserService>().Object,
            new Mock<IOptionsSnapshot<slskd.Options>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    public void Dispose()
    {
        _anchor.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetDownloads_Pages_Complete_User_Groups_By_Newest_Activity()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 101; i++)
        {
            Insert(TransferDirection.Download, "alice", now, $"alice\\album\\{i}.mp3");
        }

        Insert(TransferDirection.Download, "bob", now.AddMinutes(-1), "bob\\album\\file.mp3");
        Insert(TransferDirection.Download, "removed", now.AddMinutes(1), "removed\\file.mp3", removed: true);

        var result = Assert.IsType<OkObjectResult>(_controller.GetDownloadsAsync(includeRemoved: false, offset: 0, limit: 1));
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(result.Value).ToList();
        var files = users.Single().Directories.SelectMany(directory => directory.Files).ToList();

        Assert.Equal("alice", users.Single().Username);
        Assert.Equal(101, files.Count);
        Assert.Equal("2", _controller.Response.Headers["X-Total-Count"]);
    }

    [Fact]
    public void GetUploads_Applies_IncludeRemoved_Before_Counting()
    {
        var now = DateTime.UtcNow;
        Insert(TransferDirection.Upload, "alice", now, "alice\\file.mp3");
        Insert(TransferDirection.Upload, "bob", now.AddMinutes(1), "bob\\file.mp3", removed: true);

        var withoutRemoved = Assert.IsType<OkObjectResult>(_controller.GetUploads(includeRemoved: false, offset: 0, limit: 100));
        var visibleUsers = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(withoutRemoved.Value).ToList();

        Assert.Single(visibleUsers);
        Assert.Equal("1", _controller.Response.Headers["X-Total-Count"]);

        _controller.Response.Headers.Clear();
        var withRemoved = Assert.IsType<OkObjectResult>(_controller.GetUploads(includeRemoved: true, offset: 0, limit: 100));
        var allUsers = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(withRemoved.Value).ToList();

        Assert.Equal(2, allUsers.Count);
        Assert.Equal("2", _controller.Response.Headers["X-Total-Count"]);
    }

    [Fact]
    public void GetDownloads_Returns_An_Empty_Out_Of_Range_Page_With_The_Total_Count()
    {
        Insert(TransferDirection.Download, "alice", DateTime.UtcNow, "file.mp3");

        var result = Assert.IsType<OkObjectResult>(_controller.GetDownloadsAsync(offset: 10, limit: 100));
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(result.Value).ToList();

        Assert.Empty(users);
        Assert.Equal("1", _controller.Response.Headers["X-Total-Count"]);
    }

    [Fact]
    public void GetDownloads_Without_Pagination_Uses_The_Existing_List()
    {
        var transfers = new List<Transfer>
        {
            CreateTransfer(TransferDirection.Download, "alice", DateTime.UtcNow, "file.mp3"),
        };
        _downloads
            .Setup(service => service.List(It.IsAny<Expression<Func<Transfer, bool>>>(), false))
            .Returns(transfers);

        var result = Assert.IsType<OkObjectResult>(_controller.GetDownloadsAsync());
        var users = Assert.IsAssignableFrom<IEnumerable<UserResponse>>(result.Value).ToList();

        Assert.Equal("alice", users.Single().Username);
        Assert.Equal("1", _controller.Response.Headers["X-Total-Count"]);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, 0)]
    public void GetDownloads_Rejects_Invalid_Pagination(int? offset, int? limit)
    {
        var result = _controller.GetDownloadsAsync(offset: offset, limit: limit);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private void Insert(TransferDirection direction, string username, DateTime requestedAt, string filename, bool removed = false)
    {
        using var context = new TransfersDbContext(_contextOptions);
        context.Transfers.Add(CreateTransfer(direction, username, requestedAt, filename, removed));
        context.SaveChanges();
    }

    private static Transfer CreateTransfer(TransferDirection direction, string username, DateTime requestedAt, string filename, bool removed = false)
    {
        return new Transfer
        {
            Id = Guid.NewGuid(),
            Direction = direction,
            Username = username,
            Filename = filename,
            RequestedAt = requestedAt,
            Removed = removed,
            Size = 1,
        };
    }
}
