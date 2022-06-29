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

namespace slskd
{
    using System;
    using System.Net.Sockets;
    using Soulseek;

    /// <summary>
    ///     Core extensions; extensions for types specific to Soulseek or slskd.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        ///     Redacts this instance of Options, replacing properties marked with <see cref="SecretAttribute"/> with '*****'.
        /// </summary>
        /// <remarks>
        ///     Creates a deep clone before redacting.
        /// </remarks>
        /// <param name="options">The Options instance to redact.</param>
        /// <returns>A redacted instance.</returns>
        public static Options Redact(this Options options)
        {
            var redacted = options.ToJson().FromJson<Options>();
            Redactor.Redact(redacted, redactWith: "*****");
            return redacted;
        }

        /// <summary>
        ///     Creates a copy of this instance with the specified parameters changed.
        /// </summary>
        /// <param name="o">The options instance to copy.</param>
        /// <param name="readBufferSize">The read buffer size for underlying TCP connections.</param>
        /// <param name="writeBufferSize">The write buffer size for underlying TCP connections.</param>
        /// <param name="writeQueueSize">The size of the write queue for double buffered writes.</param>
        /// <param name="connectTimeout">The connection timeout, in milliseconds, for client and peer TCP connections.</param>
        /// <param name="inactivityTimeout">The inactivity timeout, in milliseconds, for peer TCP connections.</param>
        /// <param name="proxyOptions">Optional SOCKS 5 proxy configuration options.</param>
        /// <param name="configureSocketAction">
        ///     The delegate invoked during instantiation to configure the server Socket instance.
        /// </param>
        /// <returns>The new instance.</returns>
        public static ConnectionOptions With(
            this ConnectionOptions o,
            int? readBufferSize = null,
            int? writeBufferSize = null,
            int? writeQueueSize = null,
            int? connectTimeout = null,
            int? inactivityTimeout = null,
            ProxyOptions proxyOptions = null,
            Action<Socket> configureSocketAction = null) => new ConnectionOptions(
                readBufferSize: readBufferSize ?? o.ReadBufferSize,
                writeBufferSize: writeBufferSize ?? o.WriteBufferSize,
                writeQueueSize: writeQueueSize ?? o.WriteQueueSize,
                connectTimeout: connectTimeout ?? o.ConnectTimeout,
                inactivityTimeout: inactivityTimeout ?? o.InactivityTimeout,
                proxyOptions: proxyOptions ?? o.ProxyOptions,
                configureSocket: configureSocketAction ?? o.ConfigureSocket);

        /// <summary>
        ///     Creates a new instance of <see cref="UserStatisticsState"/> from this instance of <see cref="UserStatistics"/>.
        /// </summary>
        /// <param name="stats">The UserStatistics instance from which to copy data</param>
        /// <returns>The new instance.</returns>
        public static UserStatisticsState ToUserStatisticsState(this UserStatistics stats) => new()
        {
            AverageSpeed = stats.AverageSpeed,
            DirectoryCount = stats.DirectoryCount,
            FileCount = stats.FileCount,
            UploadCount = stats.UploadCount,
        };
    }
}
