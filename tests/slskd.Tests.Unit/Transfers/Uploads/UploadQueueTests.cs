using Moq;
using slskd.Transfers;
using slskd.Users;

namespace slskd.Tests.Unit.Transfers.Uploads
{
    public class UploadQueueTests
    {
        private static (UploadQueue queue, Mocks mocks) GetFixture(Options options = null)
        {
            var mocks = new Mocks(options);
            var queue = new UploadQueue(
                mocks.UserService.Object,
                mocks.OptionsMonitor);

            return (queue, mocks);
        }

        private class Mocks
        {
            public Mocks(Options options = null)
            {
                OptionsMonitor = new TestOptionsMonitor<Options>(options ?? new Options());
            }

            public Mock<IUserService> UserService { get; } = new Mock<IUserService>();
            public TestOptionsMonitor<Options> OptionsMonitor { get; init; }
        }
    }
}
