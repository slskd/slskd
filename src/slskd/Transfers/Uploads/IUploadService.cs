// <copyright file="IUploadService.cs" company="slskd Team">
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

namespace slskd.Transfers.Uploads
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    /// <summary>
    ///     Manages uploads.
    /// </summary>
    public interface IUploadService
    {
        /// <summary>
        ///     Gets the upload governor.
        /// </summary>
        IUploadGovernor Governor { get; }

        /// <summary>
        ///     Gets the upload queue.
        /// </summary>
        IUploadQueue Queue { get; }

        /// <summary>
        ///     Enqueues the requested file.
        /// </summary>
        /// <param name="username">The username of the requesting user.</param>
        /// <param name="filename">The local filename of the requested file.</param>
        /// <returns>The operation context.</returns>
        Task EnqueueAsync(string username, string filename);

        /// <summary>
        ///     Returns a value indicating whether an upload matching the specified <paramref name="expression"/> exists.
        /// </summary>
        /// <param name="expression">The expression used to match uploads.</param>
        /// <returns>A value indicating whether an upload matching the specified expression exists.</returns>
        Task<bool> ExistsAsync(Expression<Func<Transfer, bool>> expression);

        /// <summary>
        ///     Finds a single upload matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match uploads.</param>
        /// <returns>The found transfer, or default if not found.</returns>
        Task<Transfer> FindAsync(Expression<Func<Transfer, bool>> expression);

        /// <summary>
        ///     Returns a list of all uploads matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match uploads.</param>
        /// <param name="includeRemoved">Optionally include uploads that have been removed previously.</param>
        /// <returns>The list of uploads matching the specified expression, or all uploads if no expression is specified.</returns>
        Task<List<Transfer>> ListAsync(Expression<Func<Transfer, bool>> expression = null, bool includeRemoved = false);

        /// <summary>
        ///     Removes the upload matching the specified <paramref name="id"/>.
        /// </summary>
        /// <remarks>This is a soft delete; the record is retained for historical retrieval.</remarks>
        /// <param name="id">The unique identifier of the upload.</param>
        /// <returns></returns>
        Task RemoveAsync(Guid id);

        /// <summary>
        ///     Cancels the upload matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the upload.</param>
        /// <returns>A value indicating whether the upload was successfully cancelled.</returns>
        bool TryCancel(Guid id);

        /// <summary>
        ///     Updates the specified <paramref name="transfer"/>.
        /// </summary>
        /// <param name="transfer">The transfer to update.</param>
        /// <returns>The operation context.</returns>
        Task UpdateAsync(Transfer transfer);
    }
}