// <copyright file="SqlitePragmaInterceptor.cs" company="slskd Team">
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

namespace slskd
{
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore.Diagnostics;

    /// <summary>
    ///     Intercepts SQLite database connections to set PRAGMAs immediately after opening.
    /// </summary>
    public class SqliteConnectionOpenedInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (connection is SqliteConnection)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA synchronous=1;";
                cmd.ExecuteNonQuery();
            }

            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (connection is SqliteConnection)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA synchronous=1;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
