namespace slskd.API.DTO
{
    using Soulseek;

    public class RoomInfoResponse
    {
        /// <summary>
        ///     Gets the room name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        ///     Gets the number of users in the room.
        /// </summary>
        public int UserCount { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the room is private.
        /// </summary>
        public bool IsPrivate { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the room is owned by the currently logged in user.
        /// </summary>
        public bool IsOwned { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the room is moderated by the currently logged in user.
        /// </summary>
        public bool IsModerated { get; set; }

        public static RoomInfoResponse FromRoomInfo(RoomInfo info, bool isPrivate = false, bool isOwned = false)
        {
            return new RoomInfoResponse()
            {
                Name = info.Name,
                UserCount = info.UserCount,
                IsPrivate = isPrivate,
                IsOwned = isOwned
            };
        }
    }
}
