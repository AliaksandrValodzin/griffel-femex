namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Rectangular or square hollow section — RHS, SHS, and a welded box. A box is a
    /// hollow <see cref="Rectangle"/>, and it reuses that shape's two field names for
    /// the outside so the union reads as one vocabulary.
    /// </summary>
    public class Box : Section
    {
        // Overall outside width
        public double Width { get; set; }

        // Overall outside depth
        public double Depth { get; set; }

        // Wall thickness, the same on all four sides
        public double WallThickness { get; set; }

        // Parameterless constructor for serialization
        public Box() { }

        // Convenience constructor
        public Box(int id, string? name, double width, double depth, double wallThickness)
        {
            Id = id;
            Name = name;
            Width = width;
            Depth = depth;
            WallThickness = wallThickness;
        }

        /// <summary>
        /// w·d − (w − 2t)·(d − 2t): the outside less the void. Square corners, so a
        /// cold-formed section's outer radii are not carried — a stated area is
        /// believed over this.
        /// </summary>
        public override double CalculateArea()
        {
            double outer = Width * Depth;
            double inner = (Width - 2.0 * WallThickness) * (Depth - 2.0 * WallThickness);
            return outer - inner;
        }
    }
}
