// <copyright file="UrlEncodingModelBinder.cs" company="slskd Team">
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

namespace slskd;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

/// <summary>
///     Model binder for URL encoded strings.
///
///     For this to work as intended, method parameters must:
///
///     <list>
///         <item>Use Path binding (e.g [FromRoute])</item>
///         <item>Have a backing type of string</item>
///         <item>Be decorated with the [UrlEncoded] attribute</item>
///     </list>
///
///     Most of this assumes the model binder is configured with <see cref="UrlEncodingModelBinderProvider"/>.
/// </summary>
public class UrlEncodingModelBinder : IModelBinder
{
    /// <inheritdoc/>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        /*
            given the information in the provided ModelBindingContext,

            1. determine which 'model' (named route parameter) we're trying to bind a value for (e.g. 'bar')
            2. from the full route template (e.g. api/v{version:apiVersion}/foo/{bar}), determine the index of
               the model we are looking for in the template, segmented by forward slashes (e.g. 3rd in example above)
            3. from the raw, unmanipulated request URL, extract the Nth forward-slash-separated value; this will be our
               url encoded value as provided by the client
            4. decode this value and assign it to ModelBindingContext.Result


            template: api/v{version:apiVersion}/foo/{bar}
            model: bar
            index: 3
            raw url: api/v0/foo/hello%20world
            raw value: 'hello%20world' (3rd index in split string)
            result: 'hello world'
        */
        try
        {
            var model = $"{{{bindingContext.ModelName}}}";
            var template = bindingContext.ActionContext.ActionDescriptor.AttributeRouteInfo?.Template ?? string.Empty;
            var index = template
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select((segment, index) => new { Segment = segment, Index = index })
                .Single(x => x.Segment.Equals(model, StringComparison.OrdinalIgnoreCase)).Index;

            // get the raw URL, *before* any sort of processing or default url-decoding is applied
            // this is necessary to avoid double-decoding issues; using HttpContext.Request.Path gives us
            // a value that's already had one pass of url decoding applied
            var rawUrl = bindingContext.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget ?? string.Empty;

            var request = bindingContext.HttpContext.Request;
            var absolutePath = new Uri($"{request.Scheme}://{request.Host}{rawUrl}").AbsolutePath; // discard query string

            // if the application is running with a base path (e.g., /slskd), we need to remove it
            // from the absolute path before splitting, as the route template doesn't include it
            var pathBase = request.PathBase.Value ?? string.Empty;
            if (!string.IsNullOrEmpty(pathBase) && absolutePath.StartsWith(pathBase, StringComparison.OrdinalIgnoreCase))
            {
                absolutePath = absolutePath.Substring(pathBase.Length);
            }

            var rawValue = absolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .ElementAtOrDefault(index);

            if (rawValue is null)
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Could not extract URL encoded value from URL");
                return Task.CompletedTask;
            }

            var decoded = Uri.UnescapeDataString(rawValue);
            bindingContext.Result = ModelBindingResult.Success(decoded);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            throw;
        }
    }
}

/// <summary>
///     Model binding provider for URL encoded string binding via <see cref="UrlEncodingModelBinder"/>.
/// </summary>
public class UrlEncodingModelBinderProvider : IModelBinderProvider
{
    /// <summary>
    ///     Only use this binder if ALL of the following are true:
    ///
    ///     <list>
    ///         <item>The binding source is Path (instead of Query, Form, Body, etc)</item>
    ///         <item>The target type is string</item>
    ///         <item>The target parameter has the [UrlEncoded] attribute</item>
    ///     </list>
    /// </summary>
    /// <param name="context">The model binder context.</param>
    /// <returns>An instance of <see cref="UrlEncodingModelBinder"/>, or null if the conditions haven't been met.</returns>
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context.BindingInfo.BindingSource != BindingSource.Path)
        {
            return null;
        }

        if (context.Metadata is not DefaultModelMetadata metadata || metadata.ModelType != typeof(string))
        {
            return null;
        }

        if (!metadata.Attributes.ParameterAttributes?.Any(a => a.GetType() == typeof(UrlEncodedAttribute)) ?? true)
        {
            return null;
        }

        return new BinderTypeModelBinder(typeof(UrlEncodingModelBinder));
    }
}

/// <summary>
///     Indicates that the decorated parameter is a URL encoded string.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class UrlEncodedAttribute : Attribute
{
}