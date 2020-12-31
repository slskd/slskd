namespace slskd.DTO
{
    using Soulseek;

    public class UserDataResponse
    {
        /// <summary>
        ///     The average upload speed of the user.
        /// </summary>
        public int AverageSpeed { get; set; }

        /// <summary>
        ///     The user's country code, if provided.
        /// </summary>
        public string CountryCode { get; set; }

        /// <summary>
        ///     The number of directories shared by the user.
        /// </summary>
        public int DirectoryCount { get; set; }

        /// <summary>
        ///     The number of active user downloads.
        /// </summary>
        public long DownloadCount { get; set; }

        /// <summary>
        ///     The number of files shared by the user.
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        ///     A value indicating whether this user data belongs to the currently logged in user.
        /// </summary>
        public bool? Self { get; set; }

        /// <summary>
        ///     The number of the user's free download slots, if provided.
        /// </summary>
        public int? SlotsFree { get; set; }

        /// <summary>
        ///     The status of the user (0 = offline, 1 = away, 2 = online).
        /// </summary>
        public UserPresence Status { get; set; }

        /// <summary>
        ///     The username of the user.
        /// </summary>
        public string Username { get; set; }

        public static UserDataResponse FromUserData(UserData userData, bool self = false)
        {
            return new UserDataResponse()
            {
                AverageSpeed = userData.AverageSpeed,
                CountryCode = userData.CountryCode,
                DirectoryCount = userData.DirectoryCount,
                DownloadCount = userData.DownloadCount,
                FileCount = userData.FileCount,
                SlotsFree = userData.SlotsFree,
                Status = userData.Status,
                Username = userData.Username,
                Self = self ? self : (bool?)null
            };
        }
    }
}