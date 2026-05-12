namespace griffel_femex.Geometry.Sections
{
    public abstract class Section
    {
        public string Name { get; set; }

        // Abstract property to force every section to provide its area
        public abstract double CalculateArea();
    }
}
