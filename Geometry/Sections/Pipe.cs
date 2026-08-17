namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Circular hollow section — CHS, tube, pipe. A pipe is a hollow
    /// <see cref="Circle"/>, and it reuses that shape's field name for the outside
    /// so the union reads as one vocabulary.
    /// </summary>
    public class Pipe : Section
    {
        // Overall outside diameter
        public double Diameter { get; set; }

        // Wall thickness
        public double WallThickness { get; set; }

        // Parameterless constructor for serialization
        public Pipe() { }

        // Convenience constructor
        public Pipe(int id, string? name, double diameter, double wallThickness)
        {
            Id = id;
            Name = name;
            Diameter = diameter;
            WallThickness = wallThickness;
        }

        /// <summary>π/4·(D² − (D − 2t)²): the outside less the bore.</summary>
        public override double CalculateArea()
        {
            double inner = Diameter - 2.0 * WallThickness;
            return Math.PI / 4.0 * (Diameter * Diameter - inner * inner);
        }
    }
}
