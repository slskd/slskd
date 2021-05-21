// <copyright file="CompositeValidationResult.cs" company="slskd Team">
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

    /// <summary>
    ///     The composite result of a recursive validation.
    /// </summary>
    public class CompositeValidationResult : ValidationResult
    {
        public CompositeValidationResult(string errorMessage)
            : base(errorMessage)
        {
        }

        public CompositeValidationResult(string errorMessage, IEnumerable<string> memberNames)
            : base(errorMessage, memberNames)
        {
        }

        public CompositeValidationResult(string errorMessage, IEnumerable<ValidationResult> validationResults)
            : base(errorMessage)
        {
            ResultsList = (List<ValidationResult>)validationResults;
        }

        protected CompositeValidationResult(ValidationResult validationResult)
            : base(validationResult)
        {
        }

        public IEnumerable<ValidationResult> Results => ResultsList.AsReadOnly();

        private List<ValidationResult> ResultsList { get; set; } = new List<ValidationResult>();

        public void AddResult(ValidationResult validationResult)
            => ResultsList.Add(validationResult);
    }
}