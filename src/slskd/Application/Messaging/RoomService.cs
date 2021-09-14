namespace slskd.Messaging
{
    using System;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    public interface IRoomService
    {
        Task<RoomData> JoinAsync(string name);
    }

    public class RoomService : IRoomService
    {
        public RoomService(
            ISoulseekClient soulseekClient)
        {
            SoulseekClient = soulseekClient;
        }

        private ISoulseekClient SoulseekClient { get; }
        private ILogger Logger { get; set; } = Log.ForContext<RoomService>();

        public async Task<RoomData> JoinAsync(string name)
        {
            Logger.Debug("Joining room {Room}", name);

            try
            {
                var data = await SoulseekClient.JoinRoomAsync(name);
                Logger.Information("Joined room {Room}", name);
                Logger.Debug("Room data for {Room}: {Info}", name, data.ToJson());
                return data;
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to join room {Room}: {Message}", name, ex.Message);
                throw;
            }
        }
    }
}
