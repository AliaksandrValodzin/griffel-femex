namespace griffel_femex
{
    /// <summary>
    /// Which half of the checker a <see cref="ValidationMessage"/> came from — the
    /// split <c>FEMEX_BusinessModel.md</c> §4 makes when it audits this engine, and
    /// the one Phase C's report has to be able to draw.
    ///
    /// <b>Orthogonal to <see cref="ValidationSeverity"/>, not a second spelling of
    /// it.</b> Severity says how badly the model is broken; this says what kind of
    /// question was asked. Two regions with equal priority and overlapping extents
    /// is an <see cref="ValidationSeverity.Error"/> and a
    /// <see cref="Judgement"/> finding at once, and a report that could only sort by
    /// severity would file it beside a dangling section reference.
    ///
    /// <b>Why the format carries it rather than the report.</b> A classifier living
    /// in the reporting layer would be a second statement about which rule is which,
    /// maintained apart from the rules themselves and free to disagree with them —
    /// the mapping-table-in-two-places failure, applied to the flagship claim.
    /// Decision 8 of <c>SAF_Adapter.md</c> settles it: the C# engine is
    /// authoritative, so the engine says which half a rule is in, at the point where
    /// the rule is invoked.
    ///
    /// There is deliberately no default. A new rule states its half, because §4's
    /// consequence for the roadmap — <i>"the checks worth marketing are the
    /// judgement half, and new rules should be added to that half deliberately"</i>
    /// — is not something an omitted argument should be allowed to answer.
    /// </summary>
    public enum ValidationCategory
    {
        /// <summary>
        /// Referential and structural integrity: a referenced id that does not
        /// resolve, a duplicate key, a contour with two nodes. §4: <i>"This is table
        /// stakes. It catches corrupt files and adapter bugs, and an engineer will
        /// not pay for it, because their own program would not have opened the
        /// file."</i> Finished, and not to be extended.
        /// </summary>
        Referential,

        /// <summary>
        /// Engineering judgement: the model opens cleanly, solves, and is wrong.
        /// Coincident nodes that were meant to be one joint, a projected load whose
        /// direction lies in the loaded plane, a stated area that its own dimensions
        /// contradict, no load case carrying self-weight at all. §4: <i>"this half is
        /// the product."</i>
        /// </summary>
        Judgement,

        /// <summary>
        /// A statement about the file rather than about the structure: which schema
        /// it declares, what reading it migrated, what of it this build had no
        /// property for, and how much of it carries the keys a receiving program
        /// needs to match it. Neither half of §4's audit describes these, because
        /// they are not findings about a structure at all — and they are exactly
        /// what C3 of <c>SAF_Adapter.md</c> promotes to a section of its own.
        /// </summary>
        Provenance,
    }
}
