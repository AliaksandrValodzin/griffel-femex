namespace griffel_femex.Interop
{
    /// <summary>
    /// One shared vocabulary for lossy mapping, so that five adapters report
    /// comparably and a user reading two loss reports is reading the same kind of
    /// document twice. <c>FEMEX_Adapters.md</c> §4 defines each.
    ///
    /// Every one of the five is a <see cref="ValidationSeverity.Warning"/>.
    /// <see cref="ValidationSeverity.Error"/> is reserved for a transfer that did
    /// not happen, and carries a null category — see <see cref="TransferMessage"/>.
    /// </summary>
    public enum LossCategory
    {
        /// <summary>
        /// FEMEX said something the target cannot express, so the information does
        /// not arrive. Plate region priority into a program with no region model is
        /// the canonical case.
        /// </summary>
        Dropped,

        /// <summary>
        /// Expressible, but not exactly: a finite <c>Restraint.Stiffness</c> into a
        /// fixed-or-free-only target, or a chorded curve re-chorded at a different
        /// density.
        /// </summary>
        Approximated,

        /// <summary>
        /// The target required something FEMEX does not say, and the adapter
        /// supplied a default. <b>The important category, and the one naive adapters
        /// never report</b>, because from inside the adapter an invention does not
        /// feel like a loss — it feels like success. Everything worked. A number was
        /// produced. The user got a model.
        /// </summary>
        Invented,

        /// <summary>
        /// The import-side mirror of <see cref="Dropped"/>: the native model said
        /// something and FEMEX has no noun for it. Reported <b>per concept, not per
        /// object</b> — one message saying "142 members carried stiffness modifiers"
        /// is a useful report; 142 messages saying it is a denial of service against
        /// the person reading them.
        /// </summary>
        Unmapped,

        /// <summary>
        /// The only category that is not about the native boundary. A loss between
        /// two FEMEX builds, with no program involved: an adapter built against an
        /// older schema reading a file written by a newer one. Every adapter
        /// declares its schema version in <see cref="AdapterInfo"/>, compares it to
        /// <see cref="FemexModel.SchemaVersion"/> on read, and reports a higher one
        /// here rather than proceeding silently.
        /// </summary>
        Stale,
    }
}
