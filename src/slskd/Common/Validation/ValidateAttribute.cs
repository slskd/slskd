// <copyright file="ValidateAttribute.cs" company="slskd Team">
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
    using System;
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

            // note: here's a great place to log the type and value if there are issues:
            // Console.WriteLine($"{value.GetType()}: {value.ToJson()}");
            var results = new List<ValidationResult>();
            var context = new ValidationContext(value, null, null);

            // we should only be using dictionaries and arrays (e.g. []), not IEnumerable, because the YAML library
            // somehow has nondeterministic behavior, sometimes turning YAML arrays into arrays, and sometimes List
            // this could be handled by checking for List<T> but let's just keep it simple and remember to use arrays
            if (value.IsDictionary() || value is Array)
            {
                var compositeResults = new CompositeValidationResult(validationContext.DisplayName);

                if (value.IsDictionary())
                {
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
                }
                else if (value is Array array)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        var val = array.GetValue(i);
                        var result = IsValid(val, new ValidationContext(val, null, null));

                        if (result != ValidationResult.Success)
                        {
                            compositeResults.AddResult(new CompositeValidationResult(i.ToString(), new[] { result }.ToList()));
                        }
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