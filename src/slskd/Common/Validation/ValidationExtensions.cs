// <copyright file="ValidationExtensions.cs" company="slskd Team">
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
    using System.Linq;

    /// <summary>
    ///     Extension methods for types involved with validation.
    /// </summary>
    public static class ValidationExtensions
    {
        public static string GetResultString(this CompositeValidationResult result)
            => string.Join(' ', result.GetResultView(0).Select(s => s.Trim()));

        public static string GetResultView(this CompositeValidationResult result)
            => string.Join("\n", result.GetResultView(0));

        private static IEnumerable<string> GetResultView(this ValidationResult result, int depth = 0)
        {
            var indent = new string(' ', depth * 2);

            if (result is CompositeValidationResult composite)
            {
                var lines = new[] { indent + result + ":" }.ToList();

                foreach (var child in composite.Results)
                {
                    lines.AddRange(child.GetResultView(depth + 1));
                }

                return lines;
            }
            else
            {
                return new[] { indent + result };
            }
        }
    }
}
