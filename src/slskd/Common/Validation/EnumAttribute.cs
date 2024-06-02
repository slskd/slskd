// <copyright file="EnumAttribute.cs" company="slskd Team">
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
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Validates that the value is a valid member of the specified <see cref="Enum"/>.
    /// </summary>
    public class EnumAttribute : ValidationAttribute
    {
        public EnumAttribute(Type targetType, bool ignoreCase = true)
        {
            TargetType = targetType;
            IgnoreCase = ignoreCase;
        }

        private Type TargetType { get; set; }
        private bool IgnoreCase { get; set; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null && !Enum.TryParse(TargetType, value.ToString(), IgnoreCase, out _))
            {
                return new ValidationResult($"The {validationContext.DisplayName} field must be one of: {string.Join(", ", Enum.GetNames(TargetType))}. Case {(IgnoreCase ? "insensitive" : "sensitive")}.");
            }

            return ValidationResult.Success;
        }
    }
}