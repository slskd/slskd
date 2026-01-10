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
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

/// <summary>
///     Model binder for URL encoded strings.
///
///     For this to work as intended, method parameters must:
///
///     <list>
///         <item>Use either Path binding (e.g [FromRoute])</item>
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
        try
        {
            // get the raw value that was passed, if any
            var potentiallyUrlEncodedResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

            if (potentiallyUrlEncodedResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            var value = potentiallyUrlEncodedResult.FirstValue;

            if (string.IsNullOrEmpty(value))
            {
                return Task.CompletedTask;
            }

            // a value was provided; decode it
            var decodedValue = Uri.UnescapeDataString(value);

            bindingContext.Result = ModelBindingResult.Success(decodedValue);
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