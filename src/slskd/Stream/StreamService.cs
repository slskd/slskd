// <copyright file="StreamService.cs" company="JP Dillingham">
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
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Stream
{
    using System;
    using System.IO.Pipelines;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Soulseek;

    /// <summary>
    ///     Bridges a Soulseek peer download directly to an HTTP response body via a Pipe.
    ///
    ///     Data flow:
    ///
    ///       Peer TCP ──► Soulseek.NET ──► PipeWriter.AsStream()
    ///                                           │
    ///                                     (backpressure)
    ///                                           │
    ///                                     PipeReader.CopyToAsync()
    ///                                           │
    ///                                     HTTP chunked response ──► browser
    ///
    ///     The write task is fire-and-forget; the controller awaits only the reader
    ///     side so that a faulted writer does not cause an unobserved exception on
    ///     the read side (both sides share the same linked CancellationToken).
    /// </summary>
    public interface IStreamService
    {
        /// <summary>
        ///     Begins downloading <paramref name="filename"/> from <paramref name="username"/>
        ///     and writing bytes into <paramref name="writer"/>.
        ///
        ///     On success: writer.CompleteAsync() is called with no exception.
        ///     On peer error: a <c>streamError</c> SignalR event is broadcast, then
        ///     writer.CompleteAsync(ex) is called so the reader faults and the HTTP
        ///     connection closes cleanly.
        ///     On cancellation: writer.CompleteAsync() is called with no exception.
        /// </summary>
        Task StreamToPipeAsync(string username, string filename, PipeWriter writer, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Manages peer-to-HTTP audio streaming.
    /// </summary>
    public class StreamService : IStreamService
    {
        public StreamService(ISoulseekClient soulseekClient, IHubContext<StreamHub> hubContext)
        {
            Client = soulseekClient;
            Hub = hubContext;
        }

        private ISoulseekClient Client { get; }
        private IHubContext<StreamHub> Hub { get; }

        /// <inheritdoc />
        public async Task StreamToPipeAsync(string username, string filename, PipeWriter writer, CancellationToken cancellationToken = default)
        {
            try
            {
                await Client.DownloadAsync(
                    username: username,
                    remoteFilename: filename,
                    outputStreamFactory: () => Task.FromResult(writer.AsStream()),
                    cancellationToken: cancellationToken);

                await writer.CompleteAsync();
            }
            catch (OperationCanceledException)
            {
                await writer.CompleteAsync();
            }
            catch (Exception ex)
            {
                await Hub.Clients.All.SendAsync(
                    StreamHubMethods.StreamError,
                    new { username, filename, reason = ex.Message },
                    CancellationToken.None);

                await writer.CompleteAsync(ex);
            }
        }
    }
}
