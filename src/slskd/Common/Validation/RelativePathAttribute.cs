// <copyright file="RelativePathAttribute.cs" company="JP Dillingham">
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
    using System.ComponentModel.DataAnnotations;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Validates that the specified path is relative.
    /// </summary>
    public class RelativePathAttribute : ValidationAttribute
    {
        public RelativePathAttribute(bool platformAgnostic = false)
        {
            PlatformAgnostic = platformAgnostic;
        }

        public RelativePathAttribute(OSPlatform? os = null)
        {
            OS = os;
        }

        public OSPlatform? OS { get; }
        public bool PlatformAgnostic { get; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null && value is string str && !string.IsNullOrEmpty(str))
            {
                var path = value.ToString();

                if (OS.HasValue)
                {
                    // this can only be exercised via unit tests; OSPlatform can't be passeed as an attribute argument
                    // so, this takes precedent over PlatformAgnostic
                    if (!FileSafety.IsPathRelative(path, os: OS))
                    {
                        return new ValidationResult($"The {validationContext.DisplayName} field must be a relative path.");
                    }
                }
                else
                {
                    if (!PlatformAgnostic)
                    {
                        if (!FileSafety.IsPathRelative(path, os: null)) // this OS
                        {
                            return new ValidationResult($"The {validationContext.DisplayName} field must be a relative path.");
                        }
                    }
                    else
                    {
                        if (!FileSafety.IsPathRelative(path, os: OSPlatform.Linux))
                        {
                            return new ValidationResult($"The {validationContext.DisplayName} field must be a relative path.");
                        }

                        if (!FileSafety.IsPathRelative(path, os: OSPlatform.Windows))
                        {
                            return new ValidationResult($"The {validationContext.DisplayName} field must be a relative path.");
                        }
                    }
                }
            }

            return ValidationResult.Success;
        }
    }
}
