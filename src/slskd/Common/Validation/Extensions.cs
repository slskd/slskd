// <copyright file="Extensions.cs" company="slskd Team">
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
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        ///     Validates options.
        /// </summary>
        /// <param name="options">The options instance to validate.</param>
        /// <param name="result">The result of the validation, if invalid.</param>
        /// <returns>A value indicating whether the instance is valid.</returns>
        public static bool TryValidate(this Options options, out CompositeValidationResult result)
        {
            result = null;
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(options, new ValidationContext(options), results, true))
            {
                result = new CompositeValidationResult("Invalid configuration", results);
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Validates options.
        /// </summary>
        /// <param name="overlay">The options overlay instance to validate.</param>
        /// <param name="result">The result of the validation, if invalid.</param>
        /// <returns>A value indicating whether the instance is valid.</returns>
        public static bool TryValidate(this OptionsOverlay overlay, out CompositeValidationResult result)
        {
            result = null;
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(overlay, new ValidationContext(overlay), results, true))
            {
                result = new CompositeValidationResult("Invalid configuration", results);
                return false;
            }

            return true;
        }
    }
}