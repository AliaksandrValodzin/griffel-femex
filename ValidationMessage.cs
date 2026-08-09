namespace griffel_femex
{
    /// <summary>
    /// One problem found by <see cref="FemexModel.Validate()"/>: a human-readable
    /// sentence plus how seriously to take it.
    /// </summary>
    public sealed class ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string text)
        {
            Severity = severity;
            Text = text;
        }

        public ValidationSeverity Severity { get; }

        public string Text { get; }

        public static ValidationMessage Error(string text) => new ValidationMessage(ValidationSeverity.Error, text);

        public static ValidationMessage Warning(string text) => new ValidationMessage(ValidationSeverity.Warning, text);

        public override string ToString() => $"{Severity}: {Text}";
    }
}
