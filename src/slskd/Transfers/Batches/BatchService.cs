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
    using Soulseek;

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
        public async Task<Batch> FindAsync(Expression<Func<Batch, bool>> expression)
        {
            if (expression == default)
            {
                throw new ArgumentException("An expression must be supplied.", nameof(expression));
            }

            using var context = ContextFactory.CreateDbContext();

            var batch = await context.Batches
                .AsNoTracking()
                .Where(expression)
                .SingleOrDefaultAsync();

            if (batch is null)
            {
                return null;
            }

            var transfers = await context.Transfers
                .AsNoTracking()
                .Where(t => t.BatchId == batch.Id)
                .ToListAsync();

            var bytesTransferred = transfers.Sum(t => t.BytesTransferred);

            return batch with
            {
                Transfers = transfers,
                BytesTransferred = bytesTransferred,
                BytesRemaining = transfers.Sum(t => t.BytesRemaining),
                PercentComplete = batch.Size == 0 ? 0 : bytesTransferred / (double)batch.Size,
                AverageSpeed = transfers.Average(t => t.AverageSpeed),
                Removed = transfers.All(t => t.Removed),
            };
        }

        /// <summary>
        ///     Returns a list of all batches matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match batches.</param>
        /// <returns>The list of batches matching the specified expression, or all batches if no expression is specified.</returns>
        public async Task<List<Batch>> ListAsync(Expression<Func<Batch, bool>> expression = null)
        {
            expression ??= b => true;

            using var context = ContextFactory.CreateDbContext();

            var batches = await context.Batches
                .AsNoTracking()
                .Where(expression)
                .ToListAsync();

            if (batches.Count == 0)
            {
                return batches;
            }

            var batchIds = batches.Select(b => b.Id).ToList();

            var stats = (await context.Transfers
                .AsNoTracking()
                .Where(t => t.BatchId.HasValue && batchIds.Contains(t.BatchId.Value))
                .GroupBy(t => t.BatchId)
                .Select(g => new
                {
                    BatchId = g.Key,
                    BytesTransferred = g.Sum(t => t.BytesTransferred),
                    BytesRemaining = g.Sum(t => t.BytesRemaining),
                    AverageSpeed = g.Average(t => t.AverageSpeed),
                    Removed = g.All(t => t.Removed),
                    AnyInProgress = g.Any(t => TransferStateCategories.InProgress.Contains(t.State)),
                    AnyQueued = g.Any(t => TransferStateCategories.Queued.Contains(t.State)),
                    AllSuccessful = g.All(t => TransferStateCategories.Successful.Contains(t.State)),
                    AnyFailed = g.Any(t => TransferStateCategories.Failed.Contains(t.State)),
                })
                .ToListAsync())
                .ToDictionary(s => s.BatchId);

            return batches.Select(b =>
            {
                if (!stats.TryGetValue(b.Id, out var s))
                {
                    return b;
                }

                var state = s.AnyInProgress ? TransferStates.InProgress
                    : s.AnyQueued ? TransferStates.Queued
                    : s.AllSuccessful ? TransferStates.Completed | TransferStates.Succeeded
                    : s.AnyFailed ? TransferStates.Completed | TransferStates.Errored
                    : TransferStates.None;

                return b with
                {
                    BytesTransferred = s.BytesTransferred,
                    BytesRemaining = s.BytesRemaining,
                    PercentComplete = b.Size == 0 ? 0 : s.BytesTransferred / (double)b.Size,
                    AverageSpeed = s.AverageSpeed,
                    Removed = s.Removed,
                    State = state,
                };
            }).ToList();
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
