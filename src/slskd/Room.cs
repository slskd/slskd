namespace slskd
{
    using Soulseek;
    using System.Collections.Generic;
    using System.Linq;
    using slskd.Entities;

    public class Room
    {
        /// <summary>
        ///     The room name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     A value indicating whether the room is private.
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        ///     The number of operators in the room, if private.
        /// </summary>
        public int? OperatorCount { get; set; }

        /// <summary>
        ///     The operators in the room, if private.
        /// </summary>
        public IList<string> Operators { get; set; }

        /// <summary>
        ///     The owner of the room, if private.
        /// </summary>
        public string Owner { get; set; }

        /// <summary>
        ///     The list of users in the room.
        /// </summary>
        public IList<UserData> Users { get; set; } = new List<UserData>();

        /// <summary>
        ///     The list of messages.
        /// </summary>
        public IList<RoomMessage> Messages { get; set; } = new List<RoomMessage>();

        public static Room FromRoomData(RoomData roomData)
        {
            return new Room()
            {
                Name = roomData.Name,
                IsPrivate = roomData.IsPrivate,
                OperatorCount = roomData.OperatorCount,
                Operators = roomData.Operators.ToList(),
                Owner = roomData.Owner,
                Users = roomData.Users.ToList(),
                Messages = new List<RoomMessage>()
            };
        }
    }
}
