namespace griffel_femex.Interop
{
    /// <summary>
    /// One line of the loss report: <b>this crossing was lossy</b>, and about what.
    ///
    /// A distinct type from <see cref="ValidationMessage"/> on meaning rather than
    /// on shape. A validation message says <i>this model is wrong</i>; conflating
    /// the two means an adapter's honest report that it approximated a spring
    /// support reads, downstream, as a defect in the model. It is not — the model is
    /// fine, the target could not hold it. An adapter that is punished for reporting
    /// accurately will stop reporting accurately.
    ///
    /// <see cref="ValidationSeverity"/> itself is reused rather than duplicated: the
    /// discipline is right and there is no argument for a second two-valued severity
    /// enum in the same library.
    ///
    /// <b>The invariant:</b> every one of the five loss categories is a
    /// <see cref="ValidationSeverity.Warning"/>. <see cref="ValidationSeverity.Error"/>
    /// is reserved for a transfer that did not happen, and carries a null
    /// <see cref="Category"/>. A loss never blocks, because losing something is what
    /// adapters are for; a failure blocks, because there is no model. The two
    /// factory methods are the only way to build one, so the invariant cannot be
    /// broken by a caller filling in fields.
    /// </summary>
    public sealed class TransferMessage
    {
        private TransferMessage(ValidationSeverity severity, LossCategory? category,
                                ObjectRef? subject, string text, string? nativeHandle)
        {
            Severity = severity;
            Category = category;
            Subject = subject;
            Text = text;
            NativeHandle = nativeHandle;
        }

        public ValidationSeverity Severity { get; }

        /// <summary>
        /// Which of the five losses this is, or null for a failure — the one case
        /// where <see cref="Severity"/> is <see cref="ValidationSeverity.Error"/>.
        /// </summary>
        public LossCategory? Category { get; }

        /// <summary>
        /// The object the message is about, and null when the message is about the
        /// model itself.
        ///
        /// Nullable, where <c>FEMEX_Adapters.md</c> §3.5 writes it as a bare
        /// <see cref="ObjectRef"/>, and the deviation is forced by two of that same
        /// document's own rules. §3.3 keeps <c>Units</c> and <c>Gravity</c> out of
        /// <see cref="FemexEntity"/> deliberately, and §6.5, §6.6 and §4.5 then
        /// require an adapter to report exactly those — an assumed unit system, an
        /// invented gravity, a stale schema — as losses. Those three are about no
        /// entity at all, and a non-nullable struct would make each of them claim to
        /// be about grid 0, which is worse than saying nothing.
        ///
        /// The case §3.5 argues therefore still cannot go unanchored by accident:
        /// <see cref="Loss"/> takes a non-nullable subject, and the only door to a
        /// null one is <see cref="ModelLoss"/>, named for what it is.
        /// </summary>
        public ObjectRef? Subject { get; }

        public string Text { get; }

        /// <summary>
        /// The third leg, and what makes a report diagnosable: knowing that FEMEX
        /// bar 41 lost something is useful; knowing it was Robot bar <c>B41</c> is
        /// what lets somebody go and look.
        /// </summary>
        public string? NativeHandle { get; }

        /// <summary>
        /// A loss. Always a warning, by the invariant above — the category is what
        /// says how it was lost, and the severity is not the adapter's to choose.
        /// </summary>
        public static TransferMessage Loss(LossCategory category, ObjectRef subject, string text,
                                           string? nativeHandle = null)
        {
            if (text is null)
                throw new ArgumentNullException(nameof(text));

            return new TransferMessage(ValidationSeverity.Warning, category, subject, text, nativeHandle);
        }

        /// <summary>
        /// A loss about the model rather than about anything in it. There are
        /// exactly three such facts, and they are the three §3.3 keeps out of
        /// <see cref="FemexEntity"/> on purpose: the unit convention (§6.6), gravity
        /// (§6.5) and the schema version (§4.5).
        ///
        /// <b>Not a shortcut for a loss whose subject is inconvenient to look up.</b>
        /// A loss about an entity is anchored — with <see cref="Loss"/> — and the
        /// per-concept report §4.4 asks for is still anchored, to the entity kind
        /// with a null <see cref="ObjectRef.Id"/>, which is the shape that struct
        /// exists in. An unanchored message cannot be highlighted in a UI, matched
        /// to a difference by a round-trip test, or acted on by a reader, so
        /// choosing this factory should feel like a decision.
        ///
        /// Any category can apply. The unit block is <i>Dropped</i> when five typed
        /// enums meet a format with one flag, <i>Invented</i> when a model states
        /// none and the target demands one, and either is about the model.
        /// </summary>
        public static TransferMessage ModelLoss(LossCategory category, string text,
                                                string? nativeHandle = null)
        {
            if (text is null)
                throw new ArgumentNullException(nameof(text));

            return new TransferMessage(ValidationSeverity.Warning, category, null, text, nativeHandle);
        }

        /// <summary>
        /// A transfer that did not happen: the program is not running, a licence
        /// check failed, a file will not parse. Null category, Error severity, and
        /// <see cref="TransferResult{T}.Value"/> null beside it.
        ///
        /// This is also the exception policy. §3.6's rule is that a native API
        /// failure <b>returns, it does not throw</b>, and the loss report is already
        /// the vehicle the caller must read.
        /// </summary>
        public static TransferMessage Failure(string text, ObjectRef? subject = null,
                                              string? nativeHandle = null)
        {
            if (text is null)
                throw new ArgumentNullException(nameof(text));

            return new TransferMessage(ValidationSeverity.Error, null, subject, text, nativeHandle);
        }

        public override string ToString()
        {
            string category = Category.HasValue ? Category.Value.ToString() : "Failure";
            string subject = Subject.HasValue ? Subject.Value.ToString() : "model";
            return $"{Severity} [{category}] {subject}: {Text}";
        }
    }
}
