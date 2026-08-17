namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// How the profile was made. An enum where
    /// <see cref="SectionCatalogue.Source"/> is free text, and the line between them
    /// is deliberate: the set of national standards is open, unbounded and still
    /// growing, whereas the set of ways a steel section is made is small and closed.
    ///
    /// It is also the one distinction the <c>type</c> discriminator cannot make.
    /// Robot's SHSH-versus-SHSC naming turns on exactly this, and it is the worked
    /// example of catalogue naming failing without it.
    /// </summary>
    public enum SectionManufacture
    {
        /// <summary>Hot-rolled, with root radii — IPE, HEB, UB, W.</summary>
        HotRolled,

        /// <summary>Cold-formed from strip, with formed corners — cold SHS, purlins.</summary>
        ColdFormed,

        /// <summary>Fabricated from plate — plate girders, welded boxes.</summary>
        Welded,

        /// <summary>Anything else, or a library that does not say.</summary>
        Other,
    }
}
