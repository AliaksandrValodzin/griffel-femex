namespace griffel_femex.Geometry.Sections
{
    // Rectangular section (primarily for bars/beams)
    public class Rectangle : Section
    {
        public double Width { get; set; }
        public double Depth { get; set; }

        // Parameterless constructor for serialization
        public Rectangle() { }

        // Convenience constructor
        public Rectangle(int id, string? name, double width, double depth)
        {
            Id = id;
            Name = name;
            Width = width;
            Depth = depth;
        }

        public override double CalculateArea()
        {
            return Width * Depth;
        }
    }
}
