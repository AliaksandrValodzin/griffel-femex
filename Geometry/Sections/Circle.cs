namespace griffel_femex.Geometry.Sections
{
    // Circular section (primarily for columns)
    public class Circle : Section
    {
        public double Diameter { get; set; }

        // Parameterless constructor for serialization
        public Circle() { }

        // Convenience constructor
        public Circle(int id, string? name, double diameter)
        {
            Id = id;
            Name = name;
            Diameter = diameter;
        }

        public override double CalculateArea()
        {
            double radius = Diameter / 2.0;
            return Math.PI * radius * radius;
        }
    }
}
