namespace griffel_femex.Geometry
{
    /// <summary>
    /// Which degrees of freedom a plate activates in analysis.
    /// Material directionality (orthotropy) is deliberately not expressed here —
    /// it belongs on the surface property, so that the two can never disagree.
    /// </summary>
    public enum PlateBehaviour
    {
        // Both in-plane and out-of-plane action.
        Shell,

        // Out-of-plane bending only.
        Plate,

        // In-plane action only, no bending stiffness.
        Membrane,

        // In-plane action that carries compression but not tension.
        CompressionOnly,
    }
}
