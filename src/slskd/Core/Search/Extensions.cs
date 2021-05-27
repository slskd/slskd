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

namespace slskd.Search
{
    using System;
    using Soulseek;

    public static class Extensions
    {
        public static SearchOptions WithActions(
            this SearchOptions options,
            Action<SearchStateChangedEventArgs> stateChanged = null,
            Action<SearchResponseReceivedEventArgs> responseReceived = null)
        {
            stateChanged ??= (args) => { };
            responseReceived ??= (args) => { };

            return new SearchOptions(
                options.SearchTimeout,
                options.ResponseLimit,
                options.FilterResponses,
                options.MinimumResponseFileCount,
                options.MinimumPeerFreeUploadSlots,
                options.MaximumPeerQueueLength,
                options.MinimumPeerUploadSpeed,
                options.FileLimit,
                options.RemoveSingleCharacterSearchTerms,
                options.ResponseFilter,
                options.FileFilter,
                stateChanged: (args) =>
                {
                    if (options.StateChanged != null)
                    {
                        options.StateChanged(args);
                    }

                    stateChanged(args);
                },
                responseReceived: (args) =>
                {
                    if (options.ResponseReceived != null)
                    {
                        options.ResponseReceived(args);
                    }

                    responseReceived(args);
                });
        }
    }
}
