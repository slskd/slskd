namespace slskd.Messaging
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;

    public interface IRoomService
    {
        Task<RoomData> JoinAsync(string name);
        Task LeaveAsync(string name);
    }

    public class RoomService : IRoomService
    {
        public RoomService(
            ISoulseekClient soulseekClient,
            IStateMonitor<State> stateMonitor,
            IStateMutator<State> stateMutator)
        {
            Client = soulseekClient;

            Monitor = stateMonitor;
            Mutator = stateMutator;

            Client.RoomJoined += Client_RoomJoined;
            Client.RoomLeft += Client_RoomLeft;
        }

        private ISoulseekClient Client { get; }
        private IStateMonitor<State> Monitor { get; }
        private IStateMutator<State> Mutator { get; }
        private ILogger Logger { get; set; } = Log.ForContext<RoomService>();

        public async Task<RoomData> JoinAsync(string name)
        {
            Logger.Debug("Joining room {Room}", name);

            try
            {
                var data = await Client.JoinRoomAsync(name);

                Mutator.SetValue(state => state with { Rooms = state.Rooms.Concat(new[] { name }).ToArray() });

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

        public async Task LeaveAsync(string name)
        {
            Logger.Debug("Leaving room {Room}", name);
            try
            {
                await Client.LeaveRoomAsync(name);

                Mutator.SetValue(state => state with { Rooms = state.Rooms.Where(room => room != name).ToArray() });
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to leave room {Room}: {Message}", name, ex.Message);
                throw;
            }
        }

        private void Client_RoomJoined(object sender, RoomJoinedEventArgs args)
        {
        }

        private void Client_RoomLeft(object sender, RoomLeftEventArgs args)
        {
        }
    }
}
