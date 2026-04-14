// <copyright file="StreamController.cs" company="JP Dillingham">
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

namespace slskd.Stream.API
{
    using System;
    using System.IO;
    using System.IO.Pipelines;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Net.Http.Headers;

    /// <summary>
    ///     Streaming audio from a Soulseek peer directly to the browser.
    ///
    ///     Request flow:
    ///
    ///       GET /api/v0/stream/{username}/{**filename}
    ///              │
    ///              ├─ Sets Content-Type from file extension
    ///              ├─ If iOS/WebKit sends Range: bytes=0- → responds 206 + Content-Range
    ///              │   so Safari's audio element doesn't stall
    ///              │
    ///              ├─ Creates a Pipe (64KB segments, 1MB backpressure window)
    ///              ├─ Fire-and-forgets write task (peer → PipeWriter)
    ///              └─ Awaits pipe.Reader.CopyToAsync(Response.Body)
    ///                   │
    ///                   └─ 30-second stall timeout cancels both sides
    /// </summary>
    [Route("api/v{version:apiVersion}/stream")]
    [ApiVersion("0")]
    public class StreamController : ControllerBase
    {
        public StreamController(IStreamService streamService)
        {
            Streams = streamService;
        }

        private IStreamService Streams { get; }

        /// <summary>
        ///     Stream a file from a Soulseek peer.
        /// </summary>
        /// <param name="username">The Soulseek username of the peer.</param>
        /// <param name="filename">The remote file path as reported by the peer's browse/search results.</param>
        /// <param name="cancellationToken">Cancelled when the browser closes the connection.</param>
        [HttpGet("{username}/{**filename}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task StreamFile(
            [FromRoute] string username,
            [FromRoute] string filename,
            CancellationToken cancellationToken)
        {
            var pipe = new Pipe(new PipeOptions(
                minimumSegmentSize: 65536,        // 64 KB segments — reduces allocations for audio payloads
                pauseWriterThreshold: 1048576,    // pause peer download when 1 MB is buffered
                resumeWriterThreshold: 524288));  // resume when buffer drains to 512 KB

            Response.ContentType = GuessContentType(filename);

            // iOS Safari (WebKit) sends Range: bytes=0- on the first request to any audio src.
            // Without a 206 + Content-Range response the audio element stalls immediately.
            // RFC 7233 requires last-byte-pos to be a digit — "bytes 0-/*" is invalid.
            // Use "bytes 0-1/2" as the minimal valid 206 that satisfies WebKit's range check;
            // the actual body is still the full streaming response.
            if (Request.Headers.ContainsKey(HeaderNames.Range))
            {
                Response.StatusCode = 206;
                Response.Headers["Content-Range"] = "bytes 0-1/2";
                Response.Headers["Accept-Ranges"] = "bytes";
            }

            // 30-second stall timeout: if the peer stops sending data (or the browser stops
            // consuming it) for 30 seconds we cancel both the read and write sides so the
            // connection closes rather than leaking indefinitely.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                // Fire-and-forget: write task feeds bytes from the peer into the pipe.
                // We deliberately do NOT await it here — if we used Task.WhenAll and both
                // tasks faulted, the second exception would be swallowed.  Instead the write
                // task completes the PipeWriter (with or without an exception) which causes
                // CopyToAsync on the reader side to return/throw naturally.
                _ = Streams.StreamToPipeAsync(username, filename, pipe.Writer, linkedCts.Token);

                await pipe.Reader.CopyToAsync(Response.Body, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Browser closed the tab / stall timeout fired — clean exit, no error to surface.
            }
            finally
            {
                // Always release the reader regardless of outcome to avoid pipe memory leaks.
                await pipe.Reader.CompleteAsync();
            }
        }

        /// <summary>
        ///     Infers an audio MIME type from the file extension.
        ///     Falls back to application/octet-stream for unknown types.
        /// </summary>
        public static string GuessContentType(string filename) =>
            Path.GetExtension(filename).ToLowerInvariant() switch
            {
                ".mp3"  => "audio/mpeg",
                ".flac" => "audio/flac",
                ".ogg"  => "audio/ogg",
                ".m4a"  => "audio/mp4",
                ".wav"  => "audio/wav",
                _       => "application/octet-stream",
            };
    }
}
