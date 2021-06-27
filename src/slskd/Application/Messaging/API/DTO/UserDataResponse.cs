// <copyright file="UserDataResponse.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Messaging.API
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
        ///     The number of uploads tracked by the server for the user.
        /// </summary>
        public long UploadCount { get; set; }

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
                UploadCount = userData.UploadCount,
                FileCount = userData.FileCount,
                SlotsFree = userData.SlotsFree,
                Status = userData.Status,
                Username = userData.Username,
                Self = self ? self : (bool?)null,
            };
        }
    }
}