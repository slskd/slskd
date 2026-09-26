namespace slskd.Tests.Unit.Interests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Interests;
    using Soulseek;
    using Xunit;
    using static slskd.Interests.InterestMessages;

    public class InterestServiceTests
    {
        [Fact]
        public async Task SyncAsync_Sends_Nothing_When_Not_Logged_In()
        {
            var (service, mocks) = GetFixture(liked: new[] { "jazz" }, loggedIn: false);

            await service.SyncAsync(resend: true);

            Assert.Empty(mocks.Sent);
        }

        [Fact]
        public async Task SyncAsync_Sends_Normalized_Interests()
        {
            var (service, mocks) = GetFixture(liked: new[] { " Jazz ", "jazz", "", "Soul" }, hated: new[] { "NOISE" });

            await service.SyncAsync(resend: true);

            Assert.Equal(
                new[] { (InterestAdd, "jazz"), (InterestAdd, "soul"), (HatedInterestAdd, "noise") },
                mocks.Sent);
        }

        [Fact]
        public async Task SyncAsync_Sends_Only_Changes()
        {
            var (service, mocks) = GetFixture(liked: new[] { "jazz", "soul" });
            await service.SyncAsync(resend: true);
            mocks.Sent.Clear();

            mocks.OptionsMonitor.Set(BuildOptions(liked: new[] { "jazz", "funk" }));
            await service.SyncAsync();

            Assert.Equal(new[] { (InterestRemove, "soul"), (InterestAdd, "funk") }, mocks.Sent);
        }

        [Fact]
        public async Task SyncAsync_Removes_Before_Adding_When_Interest_Moves_Lists()
        {
            var (service, mocks) = GetFixture(liked: new[] { "jazz" });
            await service.SyncAsync(resend: true);
            mocks.Sent.Clear();

            mocks.OptionsMonitor.Set(BuildOptions(hated: new[] { "jazz" }));
            await service.SyncAsync();

            Assert.Equal(new[] { (InterestRemove, "jazz"), (HatedInterestAdd, "jazz") }, mocks.Sent);
        }

        [Fact]
        public async Task SyncAsync_Resends_Everything_After_Login()
        {
            var (service, mocks) = GetFixture(liked: new[] { "jazz" });
            await service.SyncAsync(resend: true);
            mocks.Sent.Clear();

            await service.SyncAsync(resend: true);

            Assert.Equal(new[] { (InterestAdd, "jazz") }, mocks.Sent);
        }

        [Fact]
        public async Task SyncAsync_Swallows_Send_Failures()
        {
            var (service, mocks) = GetFixture(liked: new[] { "jazz" });
            mocks.Channel.Setup(c => c.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("disconnected"));

            await service.SyncAsync(resend: true);
        }

        [Fact]
        public async Task GetUserInterestsAsync_Requests_By_Username()
        {
            var (service, mocks) = GetFixture();
            var payload = Concat(Str("Alice"), Int(1), Str("jazz"), Int(0));

            mocks.Channel.Setup(c => c.RequestAsync(It.IsAny<byte[]>(), GetUserInterests, "Alice", It.IsAny<Func<byte[], string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(payload);

            var interests = await service.GetUserInterestsAsync("Alice");

            Assert.Equal("Alice", interests.Username);
            Assert.Equal(new[] { "jazz" }, interests.Liked);
            Assert.Empty(interests.Hated);
        }

        [Fact]
        public async Task GetItemSimilarUsersAsync_Normalizes_Item()
        {
            var (service, mocks) = GetFixture();
            var payload = Concat(Str("jazz"), Int(1), Str("alice"));

            mocks.Channel.Setup(c => c.RequestAsync(It.IsAny<byte[]>(), GetItemSimilarUsers, "jazz", It.IsAny<Func<byte[], string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(payload);

            var users = await service.GetItemSimilarUsersAsync("  JAZZ ");

            Assert.Equal(new[] { "alice" }, users);
        }

        private static Options BuildOptions(string[] liked = null, string[] hated = null) => new Options
        {
            Soulseek = new Options.SoulseekOptions
            {
                Interests = new Options.SoulseekOptions.InterestsOptions
                {
                    Liked = liked ?? Array.Empty<string>(),
                    Hated = hated ?? Array.Empty<string>(),
                },
            },
        };

        private static byte[] Int(int value) => BitConverter.GetBytes(value);

        private static byte[] Str(string value) => Concat(Int(Encoding.UTF8.GetByteCount(value)), Encoding.UTF8.GetBytes(value));

        private static byte[] Concat(params byte[][] parts)
        {
            var list = new List<byte>();

            foreach (var part in parts)
            {
                list.AddRange(part);
            }

            return list.ToArray();
        }

        private static (InterestService Service, Mocks Mocks) GetFixture(string[] liked = null, string[] hated = null, bool loggedIn = true)
        {
            var mocks = new Mocks(BuildOptions(liked, hated), loggedIn);
            var service = new InterestService(mocks.SoulseekClient.Object, mocks.Channel.Object, mocks.OptionsMonitor);

            return (service, mocks);
        }

        private class Mocks
        {
            public Mocks(Options options, bool loggedIn)
            {
                OptionsMonitor = new TestOptionsMonitor<Options>(options);

                SoulseekClient.Setup(c => c.State)
                    .Returns(loggedIn ? SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn : SoulseekClientStates.Disconnected);

                // record each message as (code, first string)
                Channel.Setup(c => c.SendAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                    .Callback<byte[], CancellationToken>((message, _) => Sent.Add((BitConverter.ToInt32(message, 4), ReadKey(message[8..]))))
                    .Returns(Task.CompletedTask);
            }

            public Mock<ISoulseekClient> SoulseekClient { get; } = new Mock<ISoulseekClient>();
            public Mock<IServerChannel> Channel { get; } = new Mock<IServerChannel>();
            public TestOptionsMonitor<Options> OptionsMonitor { get; }
            public List<(int Code, string Item)> Sent { get; } = new List<(int Code, string Item)>();
        }
    }
}
