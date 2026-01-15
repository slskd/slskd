using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace slskd.Tests.Unit.Common.Middleware;

public class UrlEncodingModelBinderTests
{
    [Theory]
    [InlineData("api/v{version:apiVersion}/users/{username}/directory", "username", "http", "example.com", "/api/v0/users/test%20user/directory", "", "test user")]
    [InlineData("api/v{version:apiVersion}/users/{username}/directory", "username", "https", "example.com", "/api/v0/users/user%2Fwith%2Fslash/directory", "", "user/with/slash")]
    [InlineData("api/v{version:apiVersion}/users/{username}/directory", "username", "https", "example.com", "/api/v0/users/user%40special/directory", "", "user@special")]
    public async Task BindModelAsync_WithoutPathBase_ExtractsCorrectValue(
        string routeTemplate,
        string modelName,
        string scheme,
        string host,
        string rawUrl,
        string pathBase,
        string expectedValue)
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateModelBindingContext(routeTemplate, modelName, scheme, host, rawUrl, pathBase);

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(expectedValue, context.Result.Model);
    }

    [Theory]
    [InlineData("api/v{version:apiVersion}/users/{username}/directory", "username", "https", "media.example.com", "/slskd/api/v0/users/username/directory", "/slskd", "username")]
    [InlineData("api/v{version:apiVersion}/users/{username}/directory", "username", "https", "example.com", "/myapp/api/v0/users/test%20user/directory", "/myapp", "test user")]
    [InlineData("api/v{version:apiVersion}/users/{username}/directory", "username", "https", "example.com", "/base/api/v0/users/user%2Fwith%2Fslash/directory", "/base", "user/with/slash")]
    [InlineData("api/v{version:apiVersion}/users/{username}/info", "username", "https", "example.com", "/prefix/api/v0/users/special%40char/info", "/prefix", "special@char")]
    public async Task BindModelAsync_WithPathBase_ExtractsCorrectValue(
        string routeTemplate,
        string modelName,
        string scheme,
        string host,
        string rawUrl,
        string pathBase,
        string expectedValue)
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateModelBindingContext(routeTemplate, modelName, scheme, host, rawUrl, pathBase);

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal(expectedValue, context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_WithQueryString_DiscardsQueryString()
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateModelBindingContext(
            "api/v{version:apiVersion}/users/{username}/directory",
            "username",
            "https",
            "example.com",
            "/api/v0/users/testuser/directory?foo=bar&baz=qux",
            "");

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal("testuser", context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_WithPathBaseAndQueryString_ExtractsCorrectValue()
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateModelBindingContext(
            "api/v{version:apiVersion}/users/{username}/directory",
            "username",
            "https",
            "example.com",
            "/slskd/api/v0/users/testuser/directory?param=value",
            "/slskd");

        await binder.BindModelAsync(context);

        Assert.True(context.Result.IsModelSet);
        Assert.Equal("testuser", context.Result.Model);
    }

    [Fact]
    public async Task BindModelAsync_WithInvalidIndex_AddsModelError()
    {
        var binder = new UrlEncodingModelBinder();
        var context = CreateModelBindingContext(
            "api/v{version:apiVersion}/users/{username}/directory",
            "username",
            "https",
            "example.com",
            "/api/v0",
            "");

        await binder.BindModelAsync(context);

        Assert.False(context.Result.IsModelSet);
        Assert.True(context.ModelState.ContainsKey("username"));
        Assert.Single(context.ModelState["username"].Errors);
    }

    private ModelBindingContext CreateModelBindingContext(
        string routeTemplate,
        string modelName,
        string scheme,
        string host,
        string rawUrl,
        string pathBase)
    {
        var requestFeature = new HttpRequestFeature
        {
            Scheme = scheme,
            Path = rawUrl.Split('?')[0],
            PathBase = pathBase,
            QueryString = rawUrl.Contains('?') ? rawUrl.Substring(rawUrl.IndexOf('?')) : string.Empty,
            RawTarget = rawUrl,
            Headers = new HeaderDictionary { { "Host", host } }
        };

        var featureCollection = new FeatureCollection();
        featureCollection.Set<IHttpRequestFeature>(requestFeature);

        var httpContext = new DefaultHttpContext(featureCollection);
        httpContext.Request.Host = new HostString(host);

        var actionDescriptor = new ActionDescriptor();
        actionDescriptor.AttributeRouteInfo = new Microsoft.AspNetCore.Mvc.Routing.AttributeRouteInfo
        {
            Template = routeTemplate
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor);

        var modelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(string));

        var valueProvider = new SimpleValueProvider();

        var bindingContext = new DefaultModelBindingContext
        {
            ActionContext = actionContext,
            ModelName = modelName,
            ModelMetadata = modelMetadata,
            ValueProvider = valueProvider,
            ModelState = new ModelStateDictionary()
        };

        return bindingContext;
    }

    private class SimpleValueProvider : IValueProvider
    {
        public bool ContainsPrefix(string prefix) => false;
        public ValueProviderResult GetValue(string key) => ValueProviderResult.None;
    }
}
