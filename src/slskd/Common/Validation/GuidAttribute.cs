// <copyright file="GuidAttribute.cs" company="JP Dillingham">
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

    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Validates that the value is a valid Guid.
    /// </summary>
    public class GuidAttribute : ValidationAttribute
    {
        public GuidAttribute(bool allowEmpty = false)
        {
            AllowEmpty = allowEmpty;
        }

        public bool AllowEmpty { get; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is null)
            {
                return ValidationResult.Success;
            }

            if (value is Guid guid)
            {
                if (!AllowEmpty && guid == Guid.Empty)
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field must not contain an empty GUID/UUID");
                }

                return ValidationResult.Success;
            }

            if (value is string str && Guid.TryParse(str, out guid))
            {
                if (!AllowEmpty && guid == Guid.Empty)
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field must not contain an empty GUID/UUID");
                }

                return ValidationResult.Success;
            }

            return new ValidationResult($"The {validationContext.DisplayName} field must be a valid GUID/UUID");
        }
    }
}