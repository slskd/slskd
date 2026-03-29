// <copyright file="Extensions.cs" company="JP Dillingham">
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