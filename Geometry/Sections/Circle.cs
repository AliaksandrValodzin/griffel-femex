namespace griffel_femex.Geometry.Sections
{
    // 3. Circular Section (Primarily for Columns)
    public class Circle : Section
    {
        public double Diameter { get; set; }

        public Circle(string name, double diameter)
        {
            Name = name;
            Diameter = diameter;
        }

        public override double CalculateArea()
        {
            double radius = Diameter / 2.0;
            return Math.PI * Math.Pow(radius, 2);
        }
    }
}
