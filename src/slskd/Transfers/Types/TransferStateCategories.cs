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
///     A collection of transfer state integers, grouped into sets that represent common states.
/// </summary>
/// <remarks>
///     SQLite can't take advantage of indexes when using bitwise operations, which is what Entity Framework
///     turns HasFlag() expressions into.  These hash sets allow us to use IN expressions instead of bitwise.
/// </remarks>
public static class TransferStateCategories
{
    /// <summary>
    ///     All states representing a successful transfer:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Succeeded"/> (16 | 32 = 48)</item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<TransferStates> Successful = [
        TransferStates.Completed | TransferStates.Succeeded // 16 | 32 = 48
    ];

    /// <summary>
    ///     All states representing a failed transfer:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Cancelled"/> (16 | 64 = 80)</item>
    ///         <item><see cref="TransferStates.TimedOut"/> (16 | 128 = 144)</item>
    ///         <item><see cref="TransferStates.Errored"/> (16 | 256 = 272)</item>
    ///         <item><see cref="TransferStates.Rejected"/> (16 | 512 = 528)</item>
    ///         <item><see cref="TransferStates.Aborted"/> (16 | 1024 = 1040)</item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<TransferStates> Failed = [
        TransferStates.Completed | TransferStates.Cancelled, // 16 | 64 = 80
        TransferStates.Completed | TransferStates.TimedOut, // 16 | 128 = 144
        TransferStates.Completed | TransferStates.Errored, // 16 | 256 = 272
        TransferStates.Completed | TransferStates.Rejected, // 16 | 512 = 528
        TransferStates.Completed | TransferStates.Aborted, // 16 | 1024 = 1040
    ];

    /// <summary>
    ///     All states containing the <see cref="TransferStates.Completed"/> flag.
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Completed"/> (16)</item>
    ///     </list>
    ///
    ///     Successful:
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Succeeded"/> (16 | 32 = 48)</item>
    ///     </list>
    ///
    ///     Failed:
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Cancelled"/> (16 | 64 = 80)</item>
    ///         <item><see cref="TransferStates.TimedOut"/> (16 | 128 = 144)</item>
    ///         <item><see cref="TransferStates.Errored"/> (16 | 256 = 272)</item>
    ///         <item><see cref="TransferStates.Rejected"/> (16 | 512 = 528)</item>
    ///         <item><see cref="TransferStates.Aborted"/> (16 | 1024 = 1040)</item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<TransferStates> Completed = [
        TransferStates.Completed, // 16, in case of some sort of a malfunction or regression
        .. Successful,
        .. Failed
    ];

    /// <summary>
    ///     All states representing a queued transfer:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Queued"/> (2)</item>
    ///         <item><see cref="TransferStates.Locally"/> (2 | 2048 = 2050)</item>
    ///         <item><see cref="TransferStates.Remotely"/> (2 | 4096 = 4098)</item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<TransferStates> Queued = [
        TransferStates.Queued, // 2, in case of some sort of a malfunction or regression
        TransferStates.Queued | TransferStates.Locally, // 2 | 2048 = 2050
        TransferStates.Queued | TransferStates.Remotely, // 2 | 4096 = 4098
    ];

    /// <summary>
    ///     All states representing a transfer that is in progress:
    ///
    ///     <list type="bullet">
    ///         <item><see cref="TransferStates.Initializing"/> (4)</item>
    ///         <item><see cref="TransferStates.InProgress"/> (8)</item>
    ///     </list>
    /// </summary>
    public static readonly HashSet<TransferStates> InProgress = [
        TransferStates.Initializing, // 4
        TransferStates.InProgress, // 8
    ];
}