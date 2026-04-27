// <copyright file="TransfersDbContext.cs" company="JP Dillingham">
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
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

    public class TransfersDbContext : DbContext
    {
        public TransfersDbContext(DbContextOptions<TransfersDbContext> options)
            : base(options)
        {
        }

        public DbSet<Batch> Batches { get; set; }
        public DbSet<Transfer> Transfers { get; set; }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            // this is absolutely NOT IDEAL and will accellerate the move away from EF
            foreach (var entry in ChangeTracker.Entries<Transfer>())
            {
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.StateDescription = entry.Entity.State.ToString();
                }
            }

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureTransfers(modelBuilder);
            ConfigureBatches(modelBuilder);
        }

        private void ConfigureTransfers(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<Transfer>()
                .Property(e => e.StartedAt)
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);

            modelBuilder
                .Entity<Transfer>()
                .Property(e => e.EndedAt)
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);

            modelBuilder
                .Entity<Transfer>()
                .Property(d => d.Direction)
                .HasConversion(new EnumToStringConverter<Soulseek.TransferDirection>());

            modelBuilder
                .Entity<Transfer>()
                .HasIndex(t => t.Direction)
                .HasDatabaseName("IDX_Transfers_Direction");

            modelBuilder
                .Entity<Transfer>()
                .HasIndex(t => t.State)
                .HasDatabaseName("IDX_Transfers_State");

            modelBuilder
                .Entity<Transfer>()
                .HasIndex(t => t.Removed)
                .HasDatabaseName("IDX_Transfers_Removed");

            modelBuilder
                .Entity<Transfer>()
                .Property(e => e.Attempts)
                .HasDefaultValue(0); // force EF to match the migration

            modelBuilder
                .Entity<Transfer>()
                .HasIndex(t => t.BatchId)
                .HasDatabaseName("IDX_Transfers_BatchId");

            // covers the check for existing records when enqueueing uploads and downloads
            modelBuilder
                .Entity<Transfer>()
                .HasIndex(t => new { t.Username, t.Filename })
                .HasDatabaseName("IDX_Transfers_UsernameFilename");

            // covers the GetUserStatistics method that backs limit checks
            // check every so often with EXPLAIN to make sure it's being shown as a covering index
            modelBuilder
                .Entity<Transfer>()
                .HasIndex(e => new { e.Username, e.Direction, e.EndedAt, e.StartedAt, e.State, e.Size })
                .HasDatabaseName("IDX_Transfers_UserUploadStatistics");
        }

        private void ConfigureBatches(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<Batch>()
                .Property(e => e.CreatedAt)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            modelBuilder
                .Entity<Batch>()
                .HasMany(b => b.Transfers)
                .WithOne()
                .HasForeignKey(t => t.BatchId)
                .IsRequired(false);
        }
    }
}
