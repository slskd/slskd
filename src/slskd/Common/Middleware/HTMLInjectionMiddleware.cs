namespace slskd
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;

    public static class HTMLInjectionMiddlewareExtensions
    {
        public static IApplicationBuilder UseHTMLInjection(this IApplicationBuilder builder, string html)
        {
            return builder.UseMiddleware<HTMLInjectionMiddleware>(html);
        }
    }

    public class HTMLInjectionMiddleware
    {
        public HTMLInjectionMiddleware(RequestDelegate next, string html)
        {
            Next = next;
            HTML = html;
        }

        private string HTML { get; }
        private RequestDelegate Next { get; }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.Headers.TryGetValue("accept", out var accept);
            var requestedTypes = accept.ToString().Split(',');

            var injectableTypes = new[] { "text/html", "application/xhtml + xml", "application/xml" };

            var isApiRoute = context.Request.Path.ToString().StartsWith("/api");
            var isGET = context.Request.Method == "GET";

            var isInjectableType = requestedTypes
                .Intersect(injectableTypes, StringComparer.InvariantCultureIgnoreCase)
                .Any();

            if (!isApiRoute && isGET && isInjectableType)
            {
                context.Request.EnableBuffering();

                await Next.Invoke(context);

                if (context.Response.StatusCode == 200)
                {
                    await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(HTML));
                }
            }
            else
            {
                await Next.Invoke(context);
            }
        }
    }
}
