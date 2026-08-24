namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// Which way a restraint acts. Crossed with <see cref="Restraint.Fixed"/> and
    /// <see cref="Restraint.Stiffness"/>, this is what takes FEMEX from three of
    /// SAF's eight translation states to seven; <see cref="Restraint"/> tabulates
    /// the mapping and names the eighth.
    ///
    /// Closed, and small enough that the argument barely needs making: a restraint
    /// either resists both directions or one of the two, and there is no fourth
    /// answer. What is <i>not</i> here is the answer that is not a sense at all —
    /// SAF's <c>Non linear</c>, which is a stiffness curve.
    ///
    /// <b>Null is <see cref="Both"/></b>, and the two are deliberately not the same
    /// value. A 1.7 file states no sense because the concept did not exist, and
    /// reading it as bidirectional is the only reading available; a 1.8 file that
    /// writes <c>Both</c> is an author saying so. That distinction costs nothing —
    /// the property is nullable with no initializer, so silence stays silence in the
    /// file — and it is the same one <see cref="Materials.MaterialType"/> draws
    /// between null and <c>Other</c>.
    /// </summary>
    public enum RestraintSense
    {
        /// <summary>Resists in both directions. What null has always meant.</summary>
        Both,

        /// <summary>
        /// Resists only when the node moves into the support — an uplift-free pad
        /// bearing, a footing on soil. Free in the opposite direction.
        /// </summary>
        CompressionOnly,

        /// <summary>
        /// Resists only when the node moves away from the support — a tie or an
        /// anchor. Free in the opposite direction.
        /// </summary>
        TensionOnly,
    }
}
