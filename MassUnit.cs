namespace griffel_femex
{
    /// <summary>
    /// The unit mass is stated in — which in FEMEX means
    /// <c>Material.Density</c> and nothing else.
    ///
    /// Closed for the reason <see cref="LengthUnit"/> gives. <b>Annotation only, and
    /// more purely so than the other four.</b> <c>Material.Density</c> is ρ in the
    /// unit that makes mass = force·time²/length come out consistent with this
    /// model's own <see cref="Units.Force"/> and <see cref="Units.Length"/>, as its
    /// doc comment already states — with kN and m that is tonnes, and concrete is
    /// 2.5. That derivation is what <c>GetWeightDensity</c> relies on, and it does
    /// not read this enum; stating <see cref="Pound"/> beside kN and m does not
    /// change what 2.5 means, it only says something contradictory about it.
    ///
    /// It is here because a report that prints a density has to name a unit, and
    /// because the derived answer is the one thing in FEMEX no reader can look up.
    /// </summary>
    public enum MassUnit
    {
        /// <summary>Kilograms.</summary>
        Kilogram,

        /// <summary>Tonnes — the consistent mass unit of a kN/m model.</summary>
        Tonne,

        /// <summary>Pounds-mass.</summary>
        Pound,

        /// <summary>Slugs — the consistent mass unit of a lbf/ft model.</summary>
        Slug,
    }
}
