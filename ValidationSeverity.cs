namespace griffel_femex
{
    /// <summary>
    /// How seriously to take a <see cref="ValidationMessage"/>.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// The model is inconsistent: a referenced id does not resolve, or a rule
        /// the format guarantees is broken. A consumer cannot be expected to make
        /// sense of it.
        /// </summary>
        Error,

        /// <summary>
        /// The model is well formed and a consumer can read it, but it says
        /// something that is more often an oversight than a decision. Warnings are
        /// never raised for anything the format forbids.
        /// </summary>
        Warning,
    }
}
