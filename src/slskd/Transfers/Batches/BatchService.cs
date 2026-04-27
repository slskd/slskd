// <copyright file="BatchService.cs" company="JP Dillingham">
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

namespace slskd.Transfers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;

    /// <summary>
    ///     Manages transfer batches.
    /// </summary>
    public interface IBatchService
    {
        /// <summary>
        ///     Creates a new batch record.
        /// </summary>
        /// <param name="batch">The batch to create.</param>
        /// <returns>The created batch.</returns>
        Batch Create(Batch batch);

        /// <summary>
        ///     Finds a single batch matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match batches.</param>
        /// <returns>The found batch, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when an expression is not supplied.</exception>
        Task<Batch> FindAsync(Expression<Func<Batch, bool>> expression);

        /// <summary>
        ///     Returns a list of all batches matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match batches.</param>
        /// <returns>The list of batches matching the specified expression, or all batches if no expression is specified.</returns>
        Task<List<Batch>> ListAsync(Expression<Func<Batch, bool>> expression = null);

        /// <summary>
        ///     Marks all transfers associated with the specified batch as removed.
        /// </summary>
        /// <param name="id">The unique identifier of the batch.</param>
        /// <exception cref="NotFoundException">Thrown when the batch does not exist.</exception>
        void Remove(Guid id);
    }

    /// <summary>
    ///     Manages transfer batches.
    /// </summary>
    public class BatchService : IBatchService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="BatchService"/> class.
        /// </summary>
        /// <param name="contextFactory">The database context factory to use.</param>
        public BatchService(IDbContextFactory<TransfersDbContext> contextFactory)
        {
            ContextFactory = contextFactory;
        }

        private IDbContextFactory<TransfersDbContext> ContextFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<BatchService>();

        /// <summary>
        ///     Creates a new batch record.
        /// </summary>
        /// <param name="batch">The batch to create.</param>
        /// <returns>The created batch.</returns>
        public Batch Create(Batch batch)
        {
            if (batch == default)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            using var context = ContextFactory.CreateDbContext();
            context.Batches.Add(batch);
            context.SaveChanges();

            Log.Debug("Created batch {Id}", batch.Id);

            return batch;
        }

        /// <summary>
        ///     Finds a single batch matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match batches.</param>
        /// <returns>The found batch, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when an expression is not supplied.</exception>
        public Task<Batch> FindAsync(Expression<Func<Batch, bool>> expression)
        {
            if (expression == default)
            {
                throw new ArgumentException("An expression must be supplied.", nameof(expression));
            }

            using var context = ContextFactory.CreateDbContext();

            return context.Batches
                .AsNoTracking()
                .Where(expression)
                .Include(b => b.Transfers)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        ///     Returns a list of all batches matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match batches.</param>
        /// <returns>The list of batches matching the specified expression, or all batches if no expression is specified.</returns>
        public Task<List<Batch>> ListAsync(Expression<Func<Batch, bool>> expression = null)
        {
            expression ??= b => true;

            using var context = ContextFactory.CreateDbContext();

            return context.Batches
                .AsNoTracking()
                .Where(expression)
                .Include(b => b.Transfers)
                .ToListAsync();
        }

        /// <summary>
        ///     Marks all transfers associated with the specified batch as removed.
        /// </summary>
        /// <param name="id">The unique identifier of the batch.</param>
        /// <exception cref="NotFoundException">Thrown when the batch does not exist.</exception>
        public void Remove(Guid id)
        {
            using var context = ContextFactory.CreateDbContext();

            var exists = context.Batches.Any(b => b.Id == id);

            if (!exists)
            {
                throw new NotFoundException($"No batch with id {id}");
            }

            context.Transfers
                .Where(t => t.BatchId == id)
                .ExecuteUpdate(s => s.SetProperty(t => t.Removed, true));

            Log.Debug("Marked transfers for batch {Id} as removed", id);
        }
    }
}
