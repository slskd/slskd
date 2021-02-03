namespace slskd.Validation
{ 
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

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