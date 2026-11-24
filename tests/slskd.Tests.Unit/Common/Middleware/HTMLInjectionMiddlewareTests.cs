using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace slskd.Tests.Unit.Common.Middleware;

public class HTMLInjectionMiddlewareTests
{
    private const string Html = "<html><body>hello</body></html>";
    private const string Injection = "<script>window.urlBase=\"/slskd\"</script>";
    private static readonly string[] ExcludedRoutes = ["/api", "/swagger"];

    [Theory]
    [InlineData("text/html")]
    [InlineData("TEXT/HTML")]
    [InlineData("application/xhtml+xml")]
    [InlineData("application/xml")]
    [InlineData("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")]
    [InlineData("application/xml;q=0.9")]
    [InlineData("application/json, text/html")]
    public async Task InvokeAsync_Injects_When_Accept_Is_Injectable(string accept)
    {
        var (context, body) = CreateContext(path: "/", accept: accept);
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html + Injection, Read(body));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("*/*")]
    [InlineData("")]
    public async Task InvokeAsync_Does_Not_Inject_When_Accept_Is_Not_Injectable(string accept)
    {
        var (context, body) = CreateContext(path: "/", accept: accept);
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html, Read(body));
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("text/html; charset=utf-8")]
    [InlineData("TEXT/HTML;charset=UTF-8")]
    [InlineData("application/xhtml+xml")]
    public async Task InvokeAsync_Injects_When_Response_Content_Type_Is_Injectable(string contentType)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, contentType));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html + Injection, Read(body));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain; charset=utf-8")]
    [InlineData(null)]
    public async Task InvokeAsync_Does_Not_Inject_When_Response_Content_Type_Is_Not_Injectable(string contentType)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(Respond("{\"foo\":\"bar\"}", contentType));

        await middleware.InvokeAsync(context);

        Assert.Equal("{\"foo\":\"bar\"}", Read(body));
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v0/application")]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    public async Task InvokeAsync_Does_Not_Inject_Excluded_Routes(string path)
    {
        var (context, body) = CreateContext(path: path, accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html, Read(body));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/searches")]
    [InlineData("/system/info")]
    public async Task InvokeAsync_Injects_Non_Excluded_Routes(string path)
    {
        var (context, body) = CreateContext(path: path, accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html + Injection, Read(body));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("HEAD")]
    public async Task InvokeAsync_Does_Not_Inject_Non_GET_Requests(string method)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html", method: method);
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html, Read(body));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task InvokeAsync_Does_Not_Inject_Non_200_Responses(int statusCode)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, "text/html", statusCode));

        await middleware.InvokeAsync(context);

        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.Equal(Html, Read(body));
    }

    [Fact]
    public async Task InvokeAsync_Removes_Content_Length_And_Retains_Other_Headers()
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(async ctx =>
        {
            ctx.Response.Headers["X-Test"] = "value";
            ctx.Response.ContentLength = Html.Length;
            await Respond(Html, "text/html")(ctx);
        });

        await middleware.InvokeAsync(context);

        Assert.Null(context.Response.ContentLength);
        Assert.Equal("value", context.Response.Headers["X-Test"]);
        Assert.Equal("text/html", context.Response.ContentType);
        Assert.Equal(Html + Injection, Read(body));
    }

    [Fact]
    public async Task InvokeAsync_Restores_Original_Body_Stream_When_Downstream_Throws()
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));

        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Same(body, context.Response.Body);
    }

    [Fact]
    public async Task InvokeAsync_Restores_Original_Body_Stream_When_Successful()
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Same(body, context.Response.Body);
    }

    private static HTMLInjectionMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, Injection, ExcludedRoutes);

    private static RequestDelegate Respond(string content, string contentType, int statusCode = 200)
        => async ctx =>
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = contentType;
            await ctx.Response.WriteAsync(content);
        };

    private static (HttpContext Context, MemoryStream Body) CreateContext(string path, string accept, string method = "GET")
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();

        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.Accept = accept;
        context.Response.Body = body;

        return (context, body);
    }

    private static string Read(MemoryStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        return new StreamReader(stream).ReadToEnd();
    }
}
