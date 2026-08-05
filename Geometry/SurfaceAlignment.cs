namespace griffel_femex.Geometry
{
    /// <summary>
    /// Which face of a plate the contour nodes lie on.
    /// For a non-horizontal plate (a wall) the names are normal-relative:
    /// Bottom is the -normal face and Top is the +normal face.
    /// </summary>
    public enum SurfaceAlignment
    {
        Bottom,
        Centre,
        Top,
    }
}
