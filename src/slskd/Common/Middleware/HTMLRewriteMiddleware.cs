// <copyright file="HTMLRewriteMiddleware.cs" company="JP Dillingham">
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
        ///     Executes this middleware, returning the contents of the requested HTML file with the specified replacements made.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // accept types can have some metadata after the type, separated by a semicolon. example:
            //   text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8
            context.Request.Headers.TryGetValue("accept", out var accept);

            // split by comma, then drop the semicolon and everything after it (if present) for that type
            var requestedTypes = accept.ToString().Split(',').Select(type => type.Split(';')[0].Trim());
            var injectableTypes = new[] { "text/html", "application/xhtml+xml", "application/xml" };
            var isRewriteableType = requestedTypes
                .Intersect(injectableTypes, StringComparer.InvariantCultureIgnoreCase)
                .Any();

            var isApiRoute = context.Request.Path.ToString().StartsWith("/api");
            var isGET = context.Request.Method == "GET";

            if (!isApiRoute && isGET && isRewriteableType)
            {
                var originalStream = context.Response.Body;

                // swap the response body out with a memory stream so we can manipulate it later
                // the downstream middleware will write the response to it
                var stream = new MemoryStream();
                context.Response.Body = stream;

                try
                {
                    await Next.Invoke(context);

                    if (context.Response.StatusCode == 200)
                    {
                        // something downstream responded with a 200, meaning there's data in the body
                        // we need to read it, so we can reset then play it back with the modified HTML
                        context.Response.Body.Seek(0, SeekOrigin.Begin);
                        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

                        var headers = context.Response.Headers
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                            .Where(kvp => !string.Equals(kvp.Key, "Content-Length", StringComparison.InvariantCultureIgnoreCase));

                        context.Response.Clear();

                        foreach (var header in headers)
                        {
                            context.Response.Headers.Add(header);
                        }

                        // check the response content type to make sure it's still rewriteable. the server might return something other
                        // than what was requested. if it's no longer rewriteable, don't rewrite.
                        // drop the semicolon and everything after it (e.g. '; charset=utf-8') before comparing
                        var responseType = context.Response.ContentType?.Split(';')[0].Trim();
                        var isRewriteableResponseType = injectableTypes.Contains(responseType, StringComparer.InvariantCultureIgnoreCase);
                        if (!isRewriteableResponseType)
                        {
                            await context.Response.WriteAsync(body);
                        }
                        else
                        {
                            await context.Response.WriteAsync(Regex.Replace(body, Pattern, Replacement));
                        }
                    }

                    // rewind the stream we injected to the beginning, then replay the data to the
                    // original stream
                    stream.Seek(0, SeekOrigin.Begin);
                    await stream.CopyToAsync(originalStream);
                }
                finally
                {
                    // swap the original stream back in, even if something downstream threw, so that
                    // upstream middleware (e.g. the exception handler) can write to the response
                    context.Response.Body = originalStream;
                }
            }
            else
            {
                await Next.Invoke(context);
            }
        }
    }
}
