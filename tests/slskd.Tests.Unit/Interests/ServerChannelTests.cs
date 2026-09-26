namespace slskd.Tests.Unit.Interests
{
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Interests;
    using Soulseek;
    using Xunit;

    public class ServerChannelTests
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // ServerChannel depends on these internals of Soulseek.NET; if an upgrade renames them, this fails before users do
        [Fact]
        public void Soulseek_Internals_Used_By_The_Channel_Exist()
        {
            var property = typeof(SoulseekClient).GetProperty("ServerConnection", InstanceMembers);
            Assert.NotNull(property);

            var connectionType = typeof(SoulseekClient).Assembly.GetType("Soulseek.Network.MessageConnection");
            Assert.NotNull(connectionType);
            Assert.True(property.PropertyType.IsAssignableFrom(connectionType));

            Assert.NotNull(connectionType.GetMethod("WriteAsync", InstanceMembers, new[] { typeof(byte[]), typeof(CancellationToken?) }));

            var messageRead = connectionType.GetEvent("MessageRead", InstanceMembers);
            Assert.NotNull(messageRead);

            // the handler takes EventArgs; the event's argument type must derive from it for the delegate to bind
            var handler = typeof(ServerChannel).GetMethod("Connection_MessageRead", InstanceMembers);
            Assert.NotNull(Delegate.CreateDelegate(messageRead.EventHandlerType, new ServerChannel(Mock.Of<ISoulseekClient>()), handler));

            var argsType = messageRead.EventHandlerType.GetGenericArguments()[0];
            Assert.Equal(typeof(byte[]), argsType.GetProperty("Message")?.PropertyType);
        }

        [Fact]
        public async Task Throws_When_Not_Logged_In()
        {
            var client = new Mock<ISoulseekClient>();
            client.Setup(c => c.State).Returns(SoulseekClientStates.Connected);

            var channel = new ServerChannel(client.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => channel.SendAsync(new byte[8]));
        }
    }
}
