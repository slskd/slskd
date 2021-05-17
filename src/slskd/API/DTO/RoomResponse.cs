namespace slskd.API.DTO
{
    using slskd.Entities;
    using System.Collections.Generic;
    using System.Linq;

    public class RoomResponse
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
        public IEnumerable<UserDataResponse> Users { get; set; } = new List<UserDataResponse>();

        /// <summary>
        ///     The list of messages.
        /// </summary>
        public IEnumerable<RoomMessageResponse> Messages { get; set; } = new List<RoomMessageResponse>();

        public static RoomResponse FromRoom(Room room)
        {
            return new RoomResponse()
            {
                Name = room.Name,
                IsPrivate = room.IsPrivate,
                OperatorCount = room.OperatorCount,
                Operators = room.Operators?.ToList(),
                Owner = room.Owner,
                Users = new List<UserDataResponse>(),
                Messages = new List<RoomMessageResponse>(),
            };
        }
    }
}
