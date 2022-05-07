// <copyright file="StrictUrlBaseMiddleware.cs" company="slskd Team">
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
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Serilog;

    /// <summary>
    ///     Adds the <see cref="StrictUrlBaseMiddleware"/>.
    /// </summary>
    public static class StrictUrlBaseMiddlewareExtensions
    {
        /// <summary>
        ///     Ensures that routes are prefixed with <paramref name="urlBase"/>.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="urlBase"></param>
        /// <returns></returns>
        public static IApplicationBuilder EnforceUrlBase(
            this IApplicationBuilder builder, string urlBase)
        {
            return builder.UseMiddleware<StrictUrlBaseMiddleware>(urlBase);
        }
    }

    /// <summary>
    ///     Ensures that routes are prefixed with UrlBase.
    /// </summary>
    public class StrictUrlBaseMiddleware
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="StrictUrlBaseMiddleware"/> class.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="urlBase"></param>
        public StrictUrlBaseMiddleware(RequestDelegate next, string urlBase)
        {
            Next = next;
            UrlBase = urlBase;
        }

        private ILogger Log { get; } = Serilog.Log.ForContext<StrictUrlBaseMiddleware>();
        private RequestDelegate Next { get; }
        private string UrlBase { get; }

        /// <summary>
        ///     Executes this middleware, returning a 404 response if the path is not prefixed with UrlBase.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.ToString().StartsWith(UrlBase))
            {
                Log.Debug("Request to {Path} rejected with 404; URL Base {UrlBase} is strictly enforced", context.Request.Path.ToString(), UrlBase);
                context.Response.StatusCode = 404;
                return;
            }

            await Next(context);
        }
    }
}