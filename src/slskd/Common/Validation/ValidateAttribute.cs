﻿// <copyright file="ValidateAttribute.cs" company="slskd Team">
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

namespace slskd.Validation
{
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    /// <summary>
    ///     Indicates that attributed properties should be recursively validated.
    /// </summary>
    public class ValidateAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var results = new List<ValidationResult>();
            var context = new ValidationContext(value, null, null);

            if (value.IsDictionary())
            {
                var compositeResults = new CompositeValidationResult(validationContext.DisplayName);

                // given the context in this application, key type should never be anything but a string
                foreach (string key in ((IDictionary)value).Keys)
                {
                    var val = ((IDictionary)value)[key];
                    var result = IsValid(val, new ValidationContext(val, null, null));

                    if (result != ValidationResult.Success)
                    {
                        compositeResults.AddResult(new CompositeValidationResult(key, new[] { result }.ToList()));
                    }
                }

                if (compositeResults.Results.Any())
                {
                    return compositeResults;
                }

                return ValidationResult.Success;
            }

            Validator.TryValidateObject(value, context, results, true);

            if (results.Count != 0)
            {
                var compositeResults = new CompositeValidationResult(validationContext.DisplayName);
                results.ForEach(compositeResults.AddResult);

                return compositeResults;
            }

            return ValidationResult.Success;
        }
    }
}