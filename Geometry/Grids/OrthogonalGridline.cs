namespace griffel_femex.Geometry.Grids
{
    /// <summary>
    /// A gridline parallel to one of its grid's two local axes — the lines a
    /// regular orthogonal grid is made of. A regular grid is not a special type
    /// here: it is these lines at evenly spaced offsets, so a grid can be part
    /// regular and part not without changing shape.
    ///
    /// <see cref="Direction"/> names the direction the line <em>runs</em> and
    /// <see cref="Offset"/> positions it along the perpendicular local axis:
    ///  - <c>Y</c> is the grid-local vertical line at local X = <c>Offset</c>.
    ///  - <c>X</c> is the grid-local horizontal line at local Y = <c>Offset</c>.
    /// </summary>
    public class OrthogonalGridline : Gridline
    {
        // Which local axis the line runs along.
        public GridDirection Direction { get; set; }

        // Position along the perpendicular local axis.
        public double Offset { get; set; }

        // Parameterless constructor for serialization
        public OrthogonalGridline() { }

        // Convenience constructor
        public OrthogonalGridline(string label, GridDirection direction, double offset)
        {
            Label = label;
            Direction = direction;
            Offset = offset;
        }
    }
}
