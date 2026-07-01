namespace slskd.Tests.Unit.Users.API.Controllers
{
    using System.Threading.Tasks;
    using System.Net;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Users;
    using slskd.Users.API;
    using Soulseek;
    using Xunit;

    public class UsersControllerBrowseIndexTests
    {
        [Fact]
        public async Task BrowseIndex_Returns_NotFound_Given_Blacklisted_User()
        {
            var fixture = GetFixture();
            fixture.Users.Setup(users => users.IsBlacklisted("bad-user", It.IsAny<IPAddress>(), It.IsAny<bool>())).Returns(true);

            var result = await fixture.Controller.BrowseIndex("bad-user");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task BrowseIndex_Returns_NotFound_Given_Offline_User()
        {
            var fixture = GetFixture();
            fixture.Client
                .Setup(client => client.BrowseAsync("offline-user"))
                .ThrowsAsync(new UserOfflineException("offline-user"));

            var result = await fixture.Controller.BrowseIndex("offline-user");

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("offline-user", notFound.Value);
        }

        [Fact]
        public async Task BrowseIndex_Returns_Index_Response_Given_Browse_Response()
        {
            var fixture = GetFixture();
            fixture.Client
                .Setup(client => client.BrowseAsync("good-user"))
                .ReturnsAsync(new BrowseResponse(
                    [new Directory("Music", [new File(1, "song.mp3", 123, "mp3", [])])]));

            var result = await fixture.Controller.BrowseIndex("good-user");

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<BrowseIndexResponse>(ok.Value);
            Assert.Equal(1, response.Info.Directories);
            Assert.Equal(1, response.Info.Files);
            Assert.Equal("Music", Assert.Single(response.Directories).Name);
        }

        private static Fixture GetFixture()
        {
            var client = new Mock<ISoulseekClient>();
            var browseTracker = new Mock<IBrowseTracker>();
            var users = new Mock<IUserService>();
            var options = new Mock<IOptionsSnapshot<slskd.Options>>();

            users.Setup(service => service.IsBlacklisted(It.IsAny<string>(), It.IsAny<IPAddress>(), It.IsAny<bool>())).Returns(false);

            return new Fixture(
                client,
                browseTracker,
                users,
                new UsersController(client.Object, browseTracker.Object, users.Object, options.Object));
        }

        private record Fixture(
            Mock<ISoulseekClient> Client,
            Mock<IBrowseTracker> BrowseTracker,
            Mock<IUserService> Users,
            UsersController Controller);
    }
}
