// <copyright file="MessagingDbContext.cs" company="slskd Team">
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

namespace slskd.Messaging
{
    using System;
    using Microsoft.EntityFrameworkCore;

    public class MessagingDbContext : DbContext
    {
        public MessagingDbContext(DbContextOptions<MessagingDbContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }

        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<RoomMessage> RoomMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Conversation>().HasKey(e => e.Username);
            modelBuilder.Entity<Conversation>().Ignore(e => e.Messages);
            modelBuilder.Entity<Conversation>().Ignore(e => e.HasUnacknowledgedMessages);

            modelBuilder
                .Entity<PrivateMessage>()
                .Property(e => e.Timestamp)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            modelBuilder.Entity<PrivateMessage>().HasNoKey();
            modelBuilder.Entity<PrivateMessage>().HasIndex(e => e.Username);

            modelBuilder
                .Entity<RoomMessage>()
                .Property(e => e.Timestamp)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            modelBuilder.Entity<RoomMessage>().HasNoKey();
            modelBuilder.Entity<RoomMessage>().HasIndex(e => e.RoomName);
        }
    }
}
