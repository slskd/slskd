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

    /// <summary>
    ///     Validates that the specified path is relative.
    /// </summary>
    public class RelativePathAttribute : ValidationAttribute
    {
        public RelativePathAttribute(OperatingSystem os = OperatingSystem.Current)
        {
            if (os != OperatingSystem.Current && os != OperatingSystem.Any && os != OperatingSystem.All)
            {
                throw new System.ArgumentException("OperatingSystem argument for RelativePathAttribute must be one of: Current, Any, All", nameof(os));
            }

            OS = os;
        }

        public OperatingSystem OS { get; }

        private OperatingSystem? Injected { get; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null && value is string str && !string.IsNullOrEmpty(str))
            {
                var path = value.ToString();

                if (OS == OperatingSystem.Current)
                {
                    if (!FileSafety.IsPathRelative(path, os: Injected ?? Compute.OperatingSystem()))
                    {
                        return new ValidationResult($"The {validationContext.DisplayName} field must be a relative path on the current operating system.");
                    }

                    return ValidationResult.Success;
                }

                // OS == OperatingSystem.All or .Any;
                if (!FileSafety.IsPathRelative(path, os: OperatingSystem.Linux) || !FileSafety.IsPathRelative(path, os: OperatingSystem.Windows))
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field must be a relative path on all operating systems.");
                }
            }

            return ValidationResult.Success;
        }
    }
}