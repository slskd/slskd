// <copyright file="IPAddressAttribute.cs" company="slskd Team">
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