// <copyright file="IPAddressAttribute.cs" company="JP Dillingham">
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
    using System.Linq;
    using System.Net;

    /// <summary>
    ///     Validates that the string is a valid IPv4 or IPv6 IP address.
    /// </summary>
    public class IPAddressAttribute : ValidationAttribute
    {
        public IPAddressAttribute(bool allowCommaSeparatedValues = false)
        {
            AllowCommaSeparatedValues = allowCommaSeparatedValues;
        }

        private bool AllowCommaSeparatedValues { get; set; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null)
            {
                var fail = new ValidationResult($"The {validationContext.DisplayName} field specifies an invalid IPv4 or IPv6 IP address.");

                if (value is not string valueAsString)
                {
                    return fail;
                }

                if (!valueAsString.Contains(','))
                {
                    if (!IPAddress.TryParse(valueAsString, out _))
                    {
                        return fail;
                    }
                }
                else
                {
                    if (!AllowCommaSeparatedValues)
                    {
                        return new ValidationResult($"The {validationContext.DisplayName} field accepts a single value only (a comma separated list was specified)");
                    }

                    var values = valueAsString.Split(',').Select(v => v.Trim());

                    foreach (var currentValue in values)
                    {
                        if (!IPAddress.TryParse(currentValue, out _))
                        {
                            return new ValidationResult($"The {validationContext.DisplayName} field contains one or more invalid IPv4 or IPv6 IP addresses.");
                        }
                    }
                }
            }

            return ValidationResult.Success;
        }
    }
}