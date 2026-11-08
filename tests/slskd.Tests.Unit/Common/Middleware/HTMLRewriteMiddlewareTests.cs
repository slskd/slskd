using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace slskd.Tests.Unit.Common.Middleware;

public class HTMLRewriteMiddlewareTests
{
    // the same patterns and replacements used in Program.cs, with a url base of /slskd
    private const string StaticPattern = "((\\.)?\\/static)";
    private const string StaticReplacement = "/slskd/static";
    private const string Html = "<html><script src=\"./static/js/main.js\"></script><link href=\"/static/css/main.css\"></html>";
    private const string RewrittenHtml = "<html><script src=\"/slskd/static/js/main.js\"></script><link href=\"/slskd/static/css/main.css\"></html>";

    [Theory]
    [InlineData("text/html")]
    [InlineData("TEXT/HTML")]
    [InlineData("application/xhtml+xml")]
    [InlineData("application/xml")]
    [InlineData("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")]
    [InlineData("application/xml;q=0.9")]
    [InlineData("application/json, text/html")]
    public async Task InvokeAsync_Rewrites_When_Accept_Is_Rewriteable(string accept)
    {
        var (context, body) = CreateContext(path: "/", accept: accept);
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(RewrittenHtml, Read(body));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("*/*")]
    [InlineData("")]
    public async Task InvokeAsync_Does_Not_Rewrite_When_Accept_Is_Not_Rewriteable(string accept)
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
    public async Task InvokeAsync_Rewrites_When_Response_Content_Type_Is_Rewriteable(string contentType)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, contentType));

        await middleware.InvokeAsync(context);

        Assert.Equal(RewrittenHtml, Read(body));
    }

    [Theory]
    [InlineData("application/javascript")]
    [InlineData("text/css; charset=utf-8")]
    [InlineData(null)]
    public async Task InvokeAsync_Does_Not_Rewrite_When_Response_Content_Type_Is_Not_Rewriteable(string contentType)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, contentType));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html, Read(body));
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v0/application")]
    public async Task InvokeAsync_Does_Not_Rewrite_API_Routes(string path)
    {
        var (context, body) = CreateContext(path: path, accept: "text/html");
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html, Read(body));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("HEAD")]
    public async Task InvokeAsync_Does_Not_Rewrite_Non_GET_Requests(string method)
    {
        var (context, body) = CreateContext(path: "/", accept: "text/html", method: method);
        var middleware = CreateMiddleware(Respond(Html, "text/html"));

        await middleware.InvokeAsync(context);

        Assert.Equal(Html, Read(body));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task InvokeAsync_Does_Not_Rewrite_Non_200_Responses(int statusCode)
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
        Assert.Equal(RewrittenHtml, Read(body));
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

    private static HTMLRewriteMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, StaticPattern, StaticReplacement);

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
