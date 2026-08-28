namespace griffel_femex
{
    /// <summary>
    /// One problem found by <see cref="FemexModel.Validate()"/>: a human-readable
    /// sentence, how seriously to take it, and which half of the checker asked the
    /// question.
    /// </summary>
    public sealed class ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string text, ValidationCategory category)
        {
            Severity = severity;
            Text = text;
            Category = category;
        }

        public ValidationSeverity Severity { get; }

        public string Text { get; }

        /// <summary>
        /// Referential, judgement or provenance — see
        /// <see cref="ValidationCategory"/>. Orthogonal to
        /// <see cref="Severity"/>: the two axes answer different questions and a
        /// report needs both.
        /// </summary>
        public ValidationCategory Category { get; }

        public static ValidationMessage Error(string text, ValidationCategory category) =>
            new ValidationMessage(ValidationSeverity.Error, text, category);

        public static ValidationMessage Warning(string text, ValidationCategory category) =>
            new ValidationMessage(ValidationSeverity.Warning, text, category);

        public override string ToString() => $"{Severity}: {Text}";
    }
}
