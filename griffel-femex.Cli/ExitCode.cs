namespace griffel_femex.Cli
{
    /// <summary>
    /// What the process returns, per C4 — and the distinction is the whole point of
    /// having three values rather than two.
    ///
    /// <b>1 is not a failure.</b> A model with findings is the tool working: it read
    /// the file, it checked it, and it has something to say. A build that treats
    /// every non-zero exit as a broken tool will learn to ignore this one, which is
    /// why 2 exists and is reserved.
    /// </summary>
    public static class ExitCode
    {
        /// <summary>Nothing to report: no findings, no differences, no failed leg.</summary>
        public const int Clean = 0;

        /// <summary>
        /// The tool ran and has findings. Per P4 this includes <b>a <c>.femex</c>
        /// this build cannot read</b>: an unrecognised enum value in a file from a
        /// later schema is a finding about the file, not a crash in the reader, and
        /// a batch run over forty models must not stop at it.
        /// </summary>
        public const int Findings = 1;

        /// <summary>
        /// The tool did not run: a verb that does not exist, an input that is not
        /// there, an output folder that cannot be written. Never a bad input file —
        /// that is <see cref="Findings"/>, and a 2 that could be caused by a
        /// customer's model is a 2 nobody can act on.
        /// </summary>
        public const int ToolFailure = 2;
    }
}
