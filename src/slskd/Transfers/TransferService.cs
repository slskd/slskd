// <copyright file="TransferService.cs" company="slskd Team">
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

using Microsoft.Extensions.Options;

namespace slskd.Transfers
{
    using slskd.Transfers.Uploads;
    using slskd.Users;

    /// <summary>
    ///     Manages transfers.
    /// </summary>
    public interface ITransferService
    {
        /// <summary>
        ///     Gets the upload service.
        /// </summary>
        IUploadService Uploads { get; }
    }

    /// <summary>
    ///     Manages transfers.
    /// </summary>
    public class TransferService : ITransferService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransferService"/> class.
        /// </summary>
        /// <param name="userService">The UserService instance to use.</param>
        /// <param name="optionsMonitor">The OptionsMonitor instance to use.</param>
        /// <param name="uploadService">The optional UploadService instance to use.</param>
        public TransferService(
            IUserService userService,
            IOptionsMonitor<Options> optionsMonitor,
            IUploadService uploadService = null)
        {
            Users = userService;
            OptionsMonitor = optionsMonitor;

            Uploads = uploadService ?? new UploadService()
            {
                Governor = new UploadGovernor(userService: Users, optionsMonitor: OptionsMonitor),
                Queue = new UploadQueue(userService: Users, optionsMonitor: OptionsMonitor),
            };
        }

        /// <summary>
        ///     Gets the upload service.
        /// </summary>
        public IUploadService Uploads { get; init; }

        private IOptionsMonitor<Options> OptionsMonitor { get; init; }
        private IUserService Users { get; init; }
    }
}