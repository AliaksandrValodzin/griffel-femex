namespace griffel_femex.Materials
{
    /// <summary>
    /// What family of material this is — SAF's <c>StructuralMaterial.Type</c> set
    /// exactly, and the first of the two columns SAF marks mandatory that FEMEX had
    /// no home for at all.
    ///
    /// An enum where <see cref="Material.Quality"/> beside it is free text, and the
    /// line between them is the same one
    /// <see cref="Geometry.Sections.SectionManufacture"/> draws against
    /// <c>SectionCatalogue.Source</c>: the set of grades is national, open and still
    /// growing — S235 and C25/30 and GL24h come out of different codes and no list
    /// closes over them — whereas the set of families a structural material belongs
    /// to is small and closed, and has been the same six for as long as anyone has
    /// written a design code.
    ///
    /// It is also the field the design half of a material hangs off.
    /// <see cref="MaterialProperties"/> is three groups of numbers and nothing in
    /// the block says which group applies; the type is that statement, which is why
    /// a <see cref="Material.Quality"/> stated without one is warned about — a grade
    /// with no code family behind it names nothing a receiver can look up.
    /// </summary>
    public enum MaterialType
    {
        /// <summary>Concrete, reinforced or not — the reinforcement is its own material.</summary>
        Concrete,

        /// <summary>Structural steel, and reinforcement steel stated as a material of its own.</summary>
        Steel,

        /// <summary>Timber: solid, glued-laminated, or an engineered board.</summary>
        Timber,

        /// <summary>Aluminium alloy.</summary>
        Aluminium,

        /// <summary>Masonry — brick, block or stone, with its mortar, as one continuum.</summary>
        Masonry,

        /// <summary>Anything else, or a library that does not say.</summary>
        Other,
    }
}
