// <copyright file="Extensions.cs" company="slskd Team">
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
    public static class Extensions
    {
        public static Transfer WithSoulseekTransfer(this Transfer transfer, Soulseek.Transfer t)
        {
            return new Transfer()
            {
                Id = transfer.Id,
                Username = transfer.Username,
                Direction = transfer.Direction,
                Filename = transfer.Filename,
                Size = transfer.Size,
                StartOffset = transfer.StartOffset,
                State = t.State,
                RequestedAt = transfer.RequestedAt,
                EnqueuedAt = transfer.EnqueuedAt,
                StartedAt = t.StartTime,
                EndedAt = t.EndTime,
                BytesTransferred = t.BytesTransferred,
                AverageSpeed = t.AverageSpeed,
                Exception = t.Exception?.Message,
            };
        }
    }
}
