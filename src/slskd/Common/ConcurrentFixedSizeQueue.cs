// <copyright file="ConcurrentFixedSizeQueue.cs" company="slskd Team">
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
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class ConcurrentFixedSizeQueue<T> : IReadOnlyCollection<T>
    {
        public ConcurrentFixedSizeQueue(int size)
        {
            Size = size;
        }

        public int Count => Queue.Count;

        private ConcurrentQueue<T> Queue { get; } = new ConcurrentQueue<T>();
        private int Size { get; }

        public void Enqueue(T item)
        {
            Queue.Enqueue(item);

            if (Queue.Count > Size)
            {
                Queue.TryDequeue(out _);
            }
        }

        public IEnumerator<T> GetEnumerator() => Queue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Queue.GetEnumerator();
    }
}