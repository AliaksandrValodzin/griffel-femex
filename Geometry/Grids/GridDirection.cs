namespace griffel_femex.Geometry.Grids
{
    /// <summary>
    /// Which of a grid's two local axes an <see cref="OrthogonalGridline"/> runs
    /// along. It names the direction the line <em>runs</em>, not the axis its
    /// offset is measured along — those are perpendicular to each other.
    /// </summary>
    public enum GridDirection
    {
        // The line runs parallel to the grid's local X axis, at local Y = Offset.
        // Conventionally the numbered lines.
        X,

        // The line runs parallel to the grid's local Y axis, at local X = Offset.
        // Conventionally the lettered lines.
        Y,
    }
}
