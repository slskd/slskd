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

namespace slskd.Transfers
{
    using slskd.Transfers.Downloads;
    using slskd.Transfers.Uploads;

    /// <summary>
    ///     Manages transfers.
    /// </summary>
    public interface ITransferService
    {
        /// <summary>
        ///     Gets the upload service.
        /// </summary>
        IUploadService Uploads { get; }

        /// <summary>
        ///     Gets the download service.
        /// </summary>
        IDownloadService Downloads { get; }
    }

    /// <summary>
    ///     Manages transfers.
    /// </summary>
    public class TransferService : ITransferService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="TransferService"/> class.
        /// </summary>
        public TransferService(
            IUploadService uploadService = null,
            IDownloadService downloadService = null)
        {
            Uploads = uploadService;
            Downloads = downloadService;
        }

        /// <summary>
        ///     Gets the upload service.
        /// </summary>
        public IUploadService Uploads { get; init; }

        /// <summary>
        ///     Gets the download service.
        /// </summary>
        public IDownloadService Downloads { get; init; }
    }
}