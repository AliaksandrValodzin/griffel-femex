namespace griffel_femex.Geometry
{
    /// <summary>
    /// What a plate or one of its subregions contributes to the model.
    /// </summary>
    public enum PlateRegionKind
    {
        /// <summary>
        /// Normal structural surface: generates elements and carries surface load.
        /// </summary>
        Structural,

        /// <summary>
        /// A void. No elements are generated and no surface load is applied over it.
        /// A region of this kind carries neither a surface property nor a material.
        /// </summary>
        Opening,

        /// <summary>
        /// Contributes no stiffness but still carries surface load, so that load
        /// patterns can be drawn independently of the thickness zones.
        /// </summary>
        LoadOnly,
    }
}
