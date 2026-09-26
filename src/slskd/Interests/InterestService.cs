// <copyright file="InterestService.cs" company="JP Dillingham">
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

using Microsoft.Extensions.Options;

namespace slskd.Interests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using Soulseek;
    using static slskd.Interests.InterestMessages;

    /// <summary>
    ///     Shares the configured interests with the server and finds users and interests through them.
    /// </summary>
    /// <remarks>
    ///     The server forgets interests when the client disconnects, so they're sent again after each login.
    /// </remarks>
    public class InterestService : IInterestService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="InterestService"/> class.
        /// </summary>
        /// <param name="soulseekClient"></param>
        /// <param name="serverChannel"></param>
        /// <param name="optionsMonitor"></param>
        public InterestService(ISoulseekClient soulseekClient, IServerChannel serverChannel, IOptionsMonitor<Options> optionsMonitor)
        {
            Client = soulseekClient;
            Channel = serverChannel;
            OptionsMonitor = optionsMonitor;

            Client.LoggedIn += (sender, args) => _ = SyncAsync(resend: true);
            OptionsMonitor.OnChange(options => _ = SyncAsync());
        }

        private ISoulseekClient Client { get; }
        private IServerChannel Channel { get; }
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private SemaphoreSlim SyncLock { get; } = new SemaphoreSlim(1, 1);
        private HashSet<string> SentLiked { get; } = new HashSet<string>();
        private HashSet<string> SentHated { get; } = new HashSet<string>();
        private ILogger Log { get; } = Serilog.Log.ForContext<InterestService>();

        /// <summary>
        ///     Normalizes an interest the way the server and other clients store it.
        /// </summary>
        /// <param name="item">The interest.</param>
        /// <returns>The normalized interest, or an empty string if there's nothing left.</returns>
        public static string Normalize(string item) => (item ?? string.Empty).Trim().ToLowerInvariant();

        /// <inheritdoc/>
        public async Task<UserInterests> GetUserInterestsAsync(string username, CancellationToken cancellationToken = default)
        {
            var payload = await Channel.RequestAsync(
                Build(GetUserInterests, username),
                responseCode: GetUserInterests,
                key: username,
                keySelector: ReadKey,
                cancellationToken: cancellationToken);

            return ReadUserInterests(payload);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetItemSimilarUsersAsync(string item, CancellationToken cancellationToken = default)
        {
            item = Normalize(item);

            var payload = await Channel.RequestAsync(
                Build(GetItemSimilarUsers, item),
                responseCode: GetItemSimilarUsers,
                key: item,
                keySelector: ReadKey,
                cancellationToken: cancellationToken);

            return ReadItemSimilarUsers(payload);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SimilarUser>> GetSimilarUsersAsync(CancellationToken cancellationToken = default)
        {
            var payload = await Channel.RequestAsync(Build(GetSimilarUsers), GetSimilarUsers, cancellationToken: cancellationToken);
            return ReadSimilarUsers(payload);
        }

        /// <inheritdoc/>
        public async Task<Recommendations> GetItemRecommendationsAsync(string item, CancellationToken cancellationToken = default)
        {
            item = Normalize(item);

            var payload = await Channel.RequestAsync(
                Build(GetItemRecommendations, item),
                responseCode: GetItemRecommendations,
                key: item,
                keySelector: ReadKey,
                cancellationToken: cancellationToken);

            return ReadRecommendations(payload, hasItem: true);
        }

        /// <inheritdoc/>
        public async Task<Recommendations> GetRecommendationsAsync(CancellationToken cancellationToken = default)
        {
            var payload = await Channel.RequestAsync(Build(GetRecommendations), GetRecommendations, cancellationToken: cancellationToken);
            return ReadRecommendations(payload);
        }

        /// <inheritdoc/>
        public async Task<Recommendations> GetGlobalRecommendationsAsync(CancellationToken cancellationToken = default)
        {
            var payload = await Channel.RequestAsync(Build(GetGlobalRecommendations), GetGlobalRecommendations, cancellationToken: cancellationToken);
            return ReadRecommendations(payload);
        }

        /// <inheritdoc/>
        public async Task SyncAsync(bool resend = false)
        {
            await SyncLock.WaitAsync();

            try
            {
                if (resend)
                {
                    SentLiked.Clear();
                    SentHated.Clear();
                }

                if (!Client.State.HasFlag(SoulseekClientStates.Connected | SoulseekClientStates.LoggedIn))
                {
                    return;
                }

                var interests = OptionsMonitor.CurrentValue.Soulseek.Interests;
                var liked = NormalizeAll(interests?.Liked);
                var hated = NormalizeAll(interests?.Hated);

                // removals first, so an interest moved from one list to the other ends up in the right one
                var removed = await RemoveAsync(SentLiked, liked, InterestRemove)
                    + await RemoveAsync(SentHated, hated, HatedInterestRemove);

                var added = await AddAsync(SentLiked, liked, InterestAdd)
                    + await AddAsync(SentHated, hated, HatedInterestAdd);

                if (added + removed > 0)
                {
                    Log.Information("Updated interests on the server: {Added} added, {Removed} removed; sharing {Liked} liked and {Hated} disliked", added, removed, SentLiked.Count, SentHated.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to update interests on the server: {Message}", ex.Message);
            }
            finally
            {
                SyncLock.Release();
            }
        }

        private static HashSet<string> NormalizeAll(IEnumerable<string> items) =>
            (items ?? Enumerable.Empty<string>())
                .Select(Normalize)
                .Where(item => item.Length > 0)
                .ToHashSet();

        private async Task<int> RemoveAsync(HashSet<string> sent, HashSet<string> desired, int code)
        {
            var removals = sent.Except(desired).ToList();

            foreach (var item in removals)
            {
                await Channel.SendAsync(Build(code, item));
                sent.Remove(item);
            }

            return removals.Count;
        }

        private async Task<int> AddAsync(HashSet<string> sent, HashSet<string> desired, int code)
        {
            var additions = desired.Except(sent).ToList();

            foreach (var item in additions)
            {
                await Channel.SendAsync(Build(code, item));
                sent.Add(item);
            }

            return additions.Count;
        }
    }
}
