namespace griffel_femex.Geometry.Grids
{
    /// <summary>
    /// A gridline at any angle, given by two distinct points in its grid's local
    /// coordinates — the skew line an orthogonal grid cannot express.
    ///
    /// The two points define the line's position and direction; they are not its
    /// ends. Like every gridline it is infinite in plan, and where it is drawn is
    /// the grid's <see cref="Grid.Extent"/>'s business, not its own.
    /// </summary>
    public class FreeGridline : Gridline
    {
        // Two distinct points in grid-local coordinates. Coincident points define
        // no direction and are reported by Validate().
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // Parameterless constructor for serialization
        public FreeGridline() { }

        // Convenience constructor
        public FreeGridline(string label, double x1, double y1, double x2, double y2)
        {
            Label = label;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }
}
