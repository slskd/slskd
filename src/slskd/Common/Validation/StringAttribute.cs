// <copyright file="StringAttribute.cs" company="JP Dillingham">
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
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Validates that the value is not null or whitespace.
    /// </summary>
    public class StringAttribute : ValidationAttribute
    {
        public bool AllowNull { get; set; } = true;
        public bool AllowEmpty { get; set; } = true;
        public bool AllowWhiteSpace { get; set; } = true;
        public char[] DisallowedCharacters { get; set; } = [];
        public int MinimumLength { get; set; } = 0;
        public int MaximumLength { get; set; } = int.MaxValue;
        public Regex Pattern { get; set; } = null;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var val = (string)value;

            if (val is null)
            {
                if (!AllowNull)
                {
                    return new ValidationResult($"The {validationContext.DisplayName} field must not be null");
                }
                else
                {
                    return ValidationResult.Success;
                }
            }

            if (!AllowEmpty && val is "")
            {
                return new ValidationResult($"The {validationContext.DisplayName} field must contain at least one character");
            }

            // check this after empty to avoid overlap
            if (!AllowWhiteSpace && val.All(c => char.IsWhiteSpace(c)))
            {
                return new ValidationResult($"The {validationContext.DisplayName} field must not contain only whitespace");
            }

            if (val.Length < MinimumLength || val.Length > MaximumLength)
            {
                return new ValidationResult($"The {validationContext.DisplayName} field must be between {MinimumLength} and {MaximumLength} characters");
            }

            var invalid = val.Where(c => DisallowedCharacters.Any(d => char.ToUpperInvariant(c) == char.ToUpperInvariant(d)));

            if (invalid.Any())
            {
                return new ValidationResult($"The {validationContext.DisplayName} field contains one or more disallowed characters: {string.Join(", ", invalid.Select(c => $"'{c}'"))}");
            }

            if (Pattern is not null && Pattern.IsMatch(val))
            {
                return new ValidationResult($"The {validationContext.DisplayName} field must match the regular expression {Pattern.ToJson()}");
            }

            return ValidationResult.Success;
        }
    }
}
