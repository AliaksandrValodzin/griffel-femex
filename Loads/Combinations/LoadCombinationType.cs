namespace griffel_femex.Loads.Combinations
{
    /// <summary>
    /// How a combination's own terms are combined. Without it every combination
    /// would be read as a linear sum, so an enveloping or SRSS combination would
    /// arrive as a wrong answer rather than an obviously incomplete one.
    ///
    /// This says nothing about the design envelope — see
    /// <see cref="LoadCombination.IncludeInDesignEnvelope"/>, which is unrelated
    /// despite the shared word.
    /// </summary>
    public enum LoadCombinationType
    {
        // The factored terms are added, signs included. The default, and what all
        // ordinary code combinations are.
        LinearAdd,

        // The worst of the factored terms, taken term by term rather than summed.
        Envelope,

        // The absolute values of the factored terms are added.
        AbsoluteAdd,

        // Square root of the sum of the squares — the usual modal and directional
        // seismic rule.
        Srss,
    }
}
