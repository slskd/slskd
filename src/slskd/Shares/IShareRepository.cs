// <copyright file="IShareRepository.cs" company="slskd Team">
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

namespace slskd.Shares
{
    using System;
    using Soulseek;

    /// <summary>
    ///     Writeable persistent storage of shared files and metadata.
    /// </summary>
    public interface IShareRepository : IReadOnlyShareRepository
    {
        /// <summary>
        ///     Creates a new database.
        /// </summary>
        /// <remarks>
        ///     Creates tables using 'IF NOT EXISTS', so this is idempotent unless 'discardExisting` is specified, in which case
        ///     tables are explicitly dropped prior to creation.
        /// </remarks>
        /// <param name="discardExisting">An optional value that determines whether the existing database should be discarded.</param>
        void Create(bool discardExisting = false);

        /// <summary>
        ///     Inserts a directory.
        /// </summary>
        /// <param name="name">The fully qualified local name of the directory.</param>
        /// <param name="timestamp">The timestamp to assign to the record.</param>
        void InsertDirectory(string name, long timestamp);

        /// <summary>
        ///     Inserts a file.
        /// </summary>
        /// <param name="maskedFilename">The fully qualified remote path of the file.</param>
        /// <param name="originalFilename">The fully qualified local path of the file.</param>
        /// <param name="touchedAt">The timestamp at which the file was last modified, according to the host OS.</param>
        /// <param name="file">The Soulseek.File instance representing the file.</param>
        /// <param name="timestamp">The timestamp to assign to the record.</param>
        void InsertFile(string maskedFilename, string originalFilename, DateTime touchedAt, File file, long timestamp);

        /// <summary>
        ///     Inserts a scan record at the specified <paramref name="timestamp"/>.
        /// </summary>
        /// <param name="timestamp">The timestamp associated with the scan.</param>
        /// <param name="options">The options snapshot at the start of the scan.</param>
        void InsertScan(long timestamp, Options.SharesOptions options);

        /// <summary>
        ///     Backs the current database up to the database at the specified <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">The connection string of the destination database.</param>
        void BackupTo(string connectionString);

        /// <summary>
        ///     Restores the current database from the database at the specified <paramref name="connectionString"/>.
        /// </summary>
        /// <param name="connectionString">The connection string of the source database.</param>
        void RestoreFrom(string connectionString);

        /// <summary>
        ///     Deletes directory records with a timestamp prior to the specified <paramref name="olderThanTimestamp"/>.
        /// </summary>
        /// <param name="olderThanTimestamp">The timestamp before which to delete directories.</param>
        /// <returns>The number of records deleted.</returns>
        long PruneDirectories(long olderThanTimestamp);

        /// <summary>
        ///     Deletes file records with a timestamp prior to the specified <paramref name="olderThanTimestamp"/>.
        /// </summary>
        /// <param name="olderThanTimestamp">The timestamp before which to delete files.</param>
        /// <returns>The number of records deleted.</returns>
        long PruneFiles(long olderThanTimestamp);

        /// <summary>
        ///     Updates the scan started at the specified <paramref name="timestamp"/> to set the <paramref name="end"/>.
        /// </summary>
        /// <param name="timestamp">The timestamp associated with the scan.</param>
        /// <param name="end">The timestamp at the conclusion of the scan.</param>
        void UpdateScan(long timestamp, long end);
    }
}