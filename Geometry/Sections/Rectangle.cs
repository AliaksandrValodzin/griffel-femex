namespace griffel_femex.Geometry.Sections
{
    // 2. Rectangular Section (Primarily for Bars/Beams)
    public class Rectangle : Section
    {
        public double Width { get; set; }
        public double Depth { get; set; }

        public Rectangle(string name, double width, double depth)
        {
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
