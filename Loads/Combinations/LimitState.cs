namespace griffel_femex.Loads.Combinations
{
    /// <summary>
    /// Which design check a <see cref="LoadCombination"/> exists for. It is also
    /// the partition <see cref="FemexModel.GetDesignEnvelope"/> envelopes over:
    /// combinations are enveloped within their own limit state, because an
    /// envelope mixing strength and deflection results means nothing.
    ///
    /// Spelled out rather than abbreviated, because enum values serialize as
    /// declared. The usual acronyms map straight across: Ultimate = ULS,
    /// Serviceability = SLS, Accidental = ALS.
    /// </summary>
    public enum LimitState
    {
        // The source did not say. The honest default for an imported combination
        // whose origin carries no limit state of its own.
        Unspecified,

        // Strength and stability — the factored combinations a member is sized by.
        Ultimate,

        // Deflection, vibration, crack width — behaviour in service.
        Serviceability,

        // Fire, impact, blast and the other extraordinary events.
        Accidental,
    }
}
