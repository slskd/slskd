// <copyright file="TransferStateCategories.cs" company="slskd Team">
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

namespace slskd.Transfers;

using System.Collections.Generic;
using Soulseek;

/// <summary>
///     A collection of transfer state integers, collected into arrays that represent common states.
/// </summary>
/// <remarks>
///     SQLite can't take advantage of indexes when using bitwise operations, which is what Entity Franework
///     turns HasFlag() expressions into.  These arrays allow us to use IN expressions instead of bitwise.
/// </remarks>
public static class TransferStateCategories
{
    /// <summary>
    ///     All states representing a successful transfer:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Succeeded"/></item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<int> Successful = [
        (int)(TransferStates.Completed | TransferStates.Succeeded)
    ];

    /// <summary>
    ///     All states representing a failed transfer:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Cancelled"/></item>
    ///         <item><see cref="TransferStates.TimedOut"/></item>
    ///         <item><see cref="TransferStates.Errored"/></item>
    ///         <item><see cref="TransferStates.Rejected"/></item>
    ///         <item><see cref="TransferStates.Aborted"/></item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<int> Failed = [
        (int)(TransferStates.Completed | TransferStates.Cancelled),
        (int)(TransferStates.Completed | TransferStates.TimedOut),
        (int)(TransferStates.Completed | TransferStates.Errored),
        (int)(TransferStates.Completed | TransferStates.Rejected),
        (int)(TransferStates.Completed | TransferStates.Aborted),
    ];

    /// <summary>
    ///     All states containing the <see cref="TransferStates.Completed"/> flag.
    /// </summary>
    public static readonly HashSet<int> Completed = [
        (int)TransferStates.Completed, // in case some sort of a malfunction or regression
        .. Successful,
        .. Failed
    ];

    /// <summary>
    ///     All states representing a queued transfer:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Locally"/></item>
    ///         <item><see cref="TransferStates.Remotely"/></item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<int> Queued = [
        (int)TransferStates.Queued,
        (int)(TransferStates.Queued | TransferStates.Locally),
        (int)(TransferStates.Queued | TransferStates.Remotely),
    ];

    /// <summary>
    ///     All states representing a transfer that is in progress:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Initializing"/></item>
    ///         <item><see cref="TransferStates.InProgress"/></item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<int> InProgress = [
        (int)TransferStates.Initializing,
        (int)TransferStates.InProgress,
    ];
}