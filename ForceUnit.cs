namespace griffel_femex
{
    /// <summary>
    /// The unit every force in the model is in — load magnitudes, spring stiffness
    /// numerators, and the force half of every stress and every density.
    ///
    /// Closed for the reason <see cref="LengthUnit"/> gives, and with the same five
    /// members: three metric, two US customary. <c>Tonne-force</c> and
    /// <c>Kilogram-force</c> are deliberately absent — both are a mass standing in
    /// for a force through an assumed g, which is exactly the confusion
    /// <c>Material.Density</c> was renamed out of <c>unitWeight</c> to end, and
    /// admitting them here would reintroduce it one field away.
    /// </summary>
    public enum ForceUnit
    {
        /// <summary>Newtons.</summary>
        Newton,

        /// <summary>Kilonewtons — the usual unit of a structural load.</summary>
        Kilonewton,

        /// <summary>Meganewtons.</summary>
        Meganewton,

        /// <summary>Pounds-force (lbf).</summary>
        PoundForce,

        /// <summary>Kips — a thousand pounds-force.</summary>
        Kip,
    }
}
