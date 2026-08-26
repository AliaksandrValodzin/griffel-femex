namespace griffel_femex.Interop
{
    /// <summary>
    /// The vocabulary an adapter declares its capabilities in, and the axis a
    /// <see cref="TransferMessage"/> names its subject on — FEMEX's own root entity
    /// lists, which is the natural axis because it is the axis the model is
    /// actually made of.
    ///
    /// A fixed vocabulary rather than free-form strings, because
    /// <c>FEMEX_Adapters.md</c> §7.3 wants a test asserting a declaration matches
    /// what the adapter does, and that test cannot be written against prose.
    ///
    /// <c>Units</c> and <c>Gravity</c> are deliberately absent. They are not
    /// entities but model-level facts every adapter must handle, so making them
    /// capability-gated would let an adapter declare its way out of §6.5 and §6.6 —
    /// two of the rules most worth not being able to opt out of.
    ///
    /// <see cref="Geometry.PlateRegion"/> is absent for a different reason: it is
    /// uid-carrying but is not a root list, so a message about a region anchors to
    /// <see cref="Plate"/> carrying the region's own id and uid. The uid is what
    /// tells the two apart, which is enough for §7.2's matching and is a known
    /// coarseness of the declared vocabulary.
    /// </summary>
    public enum FemexEntity
    {
        Grid,
        Level,
        Node,
        Section,
        SurfaceProperty,
        Bar,
        Plate,
        Material,
        LoadCase,
        Load,
        LoadCombination,
        Support,
        Hinge,
        Mesh,
    }
}
