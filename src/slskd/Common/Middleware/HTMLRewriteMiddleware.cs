// <copyright file="HTMLRewriteMiddleware.cs" company="slskd Team">
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
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;

    /// <summary>
    ///     Adds the <see cref="HTMLRewriteMiddleware"/>.
    /// </summary>
    public static class HTMLRewriteMiddlewareExtensions
    {
        /// <summary>
        ///     Replaces the specified <paramref name="pattern"/> with the specified <paramref name="replacement"/>.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="pattern"></param>
        /// <param name="replacement"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseHTMLRewrite(this IApplicationBuilder builder, string pattern, string replacement)
        {
            return builder.UseMiddleware<HTMLRewriteMiddleware>(pattern, replacement);
        }
    }

    /// <summary>
    ///     Replaces parts of HTML files.
    /// </summary>
    public class HTMLRewriteMiddleware
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="HTMLRewriteMiddleware"/> class.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="pattern"></param>
        /// <param name="replacement"></param>
        public HTMLRewriteMiddleware(RequestDelegate next, string pattern, string replacement)
        {
            Next = next;
            Pattern = pattern;
            Replacement = replacement;
        }

        private string Pattern { get; }
        private string Replacement { get; }
        private RequestDelegate Next { get; }

        /// <summary>
        ///     Executes this middleware, returning the contents of the requested HTML file with the specified replacesments made.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.Headers.TryGetValue("accept", out var accept);
            var requestedTypes = accept.ToString().Split(',');

            var rewriteableType = new[] { "text/html", "application/xhtml + xml", "application/xml" };

            var isApiRoute = context.Request.Path.ToString().StartsWith("/api");
            var isGET = context.Request.Method == "GET";

            var isRewriteableType = requestedTypes
                .Intersect(rewriteableType, StringComparer.InvariantCultureIgnoreCase)
                .Any();

            if (!isApiRoute && isGET && isRewriteableType)
            {
                var originalStream = context.Response.Body;

                // swap the response body out with a memory stream so we can manipulate it later
                // the downstream middleware will write the response to it
                var stream = new MemoryStream();
                context.Response.Body = stream;

                await Next.Invoke(context);

                if (context.Response.StatusCode == 200)
                {
                    // something downstream responded with a 200, meaning there's data in the body
                    // we need to read it, so we can reset then play it back with the appended HTML
                    context.Response.Body.Seek(0, SeekOrigin.Begin);
                    var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

                    context.Response.Clear();

                    await context.Response.WriteAsync(Regex.Replace(body, Pattern, Replacement));
                }

                // rewind the stream we injected to the beginning, then replay the data to the
                // original stream
                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(originalStream);

                // swap the original stream back in
                context.Response.Body = originalStream;
            }
            else
            {
                await Next.Invoke(context);
            }
        }
    }
}
