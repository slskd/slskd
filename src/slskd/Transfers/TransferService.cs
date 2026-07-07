// <copyright file="TransferService.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Transfers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using slskd.Transfers.Downloads;
using slskd.Transfers.Uploads;

/// <summary>
///     Manages transfers.
/// </summary>
public class TransferService
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TransferService"/> class.
    /// </summary>
    public TransferService(
        IDbContextFactory<TransfersDbContext> contextFactory,
        IUploadService uploadService = null,
        IDownloadService downloadService = null)
    {
        ContextFactory = contextFactory;

        Uploads = uploadService;
        Downloads = downloadService;
    }

    /// <summary>
    ///     Gets the upload service.
    /// </summary>
    public virtual IUploadService Uploads { get; init; }

    /// <summary>
    ///     Gets the download service.
    /// </summary>
    public virtual IDownloadService Downloads { get; init; }

    private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
    private ILogger Log { get; } = Serilog.Log.ForContext<TransferService>();

    /// <summary>
    ///     Finds a single transfer matching the specified <paramref name="expression"/>.
    /// </summary>
    /// <remarks>
    ///     Use this only to avoid needing to call this method on both the Upload and Download services. If the expression
    ///     contains a direction, this method is being used in error.
    /// </remarks>
    /// <param name="expression">The expression to use to match transfers.</param>
    /// <returns>The found transfer, or default if not found.</returns>
    public virtual Transfer Find(Expression<Func<Transfer, bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        try
        {
            using var context = ContextFactory.CreateDbContext();

            return context.Transfers
                .AsNoTracking()
                .Where(expression)
                .SingleOrDefault();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to list transfers: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    ///     Returns a list of all transfers matching the optional <paramref name="expression"/>.
    /// </summary>
    /// <remarks>
    ///     Use this only to avoid needing to call this method on both the Upload and Download services. If the expression
    ///     contains a direction, this method is being used in error.
    /// </remarks>
    /// <param name="expression">An optional expression used to match transfers.</param>
    /// <param name="includeRemoved">A value indicating whether to include transfers that have been removed previously.</param>
    /// <returns>The list of transfers matching the specified expression, or all transfers if no expression is specified.</returns>
    public virtual List<Transfer> List(Expression<Func<Transfer, bool>> expression, bool includeRemoved)
    {
        expression ??= t => true;

        try
        {
            using var context = ContextFactory.CreateDbContext();

            return context.Transfers
                .AsNoTracking()
                .Where(t => !t.Removed || includeRemoved)
                .Where(expression)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to list uploads: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    ///     Query the Transfers database arbitrarily. Only use if there's no better way.
    /// </summary>
    /// <typeparam name="TResult">The result Type.</typeparam>
    /// <param name="query">The query function.</param>
    /// <returns>The result.</returns>
    public virtual TResult Query<TResult>(Func<IQueryable<Transfer>, TResult> query)
    {
        try
        {
            using var context = ContextFactory.CreateDbContext();
            return query(context.Transfers.AsNoTracking());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to query transfers: {Message}", ex.Message);
            throw;
        }
    }
}