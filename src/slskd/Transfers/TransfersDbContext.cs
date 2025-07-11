﻿// <copyright file="TransfersDbContext.cs" company="slskd Team">
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
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

    public class TransfersDbContext : DbContext
    {
        public TransfersDbContext(DbContextOptions<TransfersDbContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }

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
        }
    }
}
