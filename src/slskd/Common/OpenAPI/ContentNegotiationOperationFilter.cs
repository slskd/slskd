// <copyright file="ContentNegotiationOperationFilter.cs" company="slskd Team">
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

using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
///     OpenAPI operation filter that properly handles content negotiation with different response schemas.
/// </summary>
public class ContentNegotiationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // check to see if the current operation is decorated with [Produces], which is what enables content negotiation
        var producesAttribute = context.MethodInfo
            .GetCustomAttributes(typeof(ProducesAttribute), false)
            .Cast<ProducesAttribute>()
            .FirstOrDefault();

        if (producesAttribute == null || producesAttribute.ContentTypes.Count == 0)
        {
            return;
        }

        // next, get all of the [ProducesResponseType] attributes, if any
        var producesResponseTypeAttributes = context.MethodInfo
            .GetCustomAttributes(typeof(ProducesResponseTypeAttribute), false)
            .Cast<ProducesResponseTypeAttribute>()
            .ToList();

        if (producesResponseTypeAttributes.Count == 0)
        {
            return;
        }

        // map over the list of status codes; we need to (attempt to) perform this mapping for each status code and each
        // content type listed in [Produces]
        foreach (var statusCodeGroup in producesResponseTypeAttributes.GroupBy(x => x.StatusCode))
        {
            foreach (var contentType in producesAttribute.ContentTypes)
            {
                // find the first [ProducesResponseType] that contains this content type in its list of content types
                var attribute = statusCodeGroup.FirstOrDefault(x => GetContentTypes(x).Contains(contentType));

                if (attribute is not null)
                {
                    var response = operation.Responses[statusCodeGroup.Key.ToString()];
                    response.Content[contentType] = new OpenApiMediaType
                    {
                        Schema = context.SchemaGenerator.GenerateSchema(attribute.Type, context.SchemaRepository),
                    };
                }
            }
        }
    }

    /// <summary>
    ///     Extracts the value of the _contentTypes field of <see cref="ProducesResponseTypeAttribute"/>.
    ///
    ///     This is necessary because Microsoft didn't see fit to bless us with the ability to retrieve the content types
    ///     we specifify along with our [ProducesResponseType] attributes.
    ///
    ///     It will most likely break at some point.
    /// </summary>
    /// <param name="attribute">The attribute from which to extract the _contentTypes field.</param>
    /// <returns>The value of the extracted field.</returns>
    private MediaTypeCollection GetContentTypes(ProducesResponseTypeAttribute attribute)
    {
        var contentTypesField = typeof(ProducesResponseTypeAttribute).GetField("_contentTypes", BindingFlags.NonPublic | BindingFlags.Instance);

        MediaTypeCollection value = (MediaTypeCollection)contentTypesField.GetValue(attribute);

        if (value is null)
        {
            return [];
        }

        return value;
    }
}
