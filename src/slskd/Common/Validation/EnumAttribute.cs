// <copyright file="EnumAttribute.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Validation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

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
            if (value is IList<string> array)
            {
                if (array.Any(x => string.IsNullOrEmpty(x)))
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field contains one or more null or empty values");
                }

                if (array.Any(x => !Enum.TryParse(TargetType, x, IgnoreCase, out _)))
                {
                    return new ValidationResult($"The elements in the {validationContext.DisplayName} field must all be one of: {string.Join(", ", Enum.GetNames(TargetType))}. Case {(IgnoreCase ? "insensitive" : "sensitive")}.");
                }
            }
            else
            {
                if (value != null && !Enum.TryParse(TargetType, value.ToString(), IgnoreCase, out _))
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field must be one of: {string.Join(", ", Enum.GetNames(TargetType))}. Case {(IgnoreCase ? "insensitive" : "sensitive")}.");
                }
            }

            return ValidationResult.Success;
        }
    }
}