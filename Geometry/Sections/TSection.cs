namespace griffel_femex.Geometry.Sections
{
    // T-shaped section: a flange sitting on top of a web.
    public class TSection : Section
    {
        // Overall width of the flange
        public double FlangeWidth { get; set; }

        // Thickness of the flange
        public double FlangeThickness { get; set; }

        // Thickness (width) of the web
        public double WebThickness { get; set; }

        // Overall depth of the section (flange + web)
        public double TotalDepth { get; set; }

        // Parameterless constructor for serialization
        public TSection() { }

        // Convenience constructor
        public TSection(int id, string? name, double flangeWidth, double flangeThickness, double webThickness, double totalDepth)
        {
            Id = id;
            Name = name;
            FlangeWidth = flangeWidth;
            FlangeThickness = flangeThickness;
            WebThickness = webThickness;
            TotalDepth = totalDepth;
        }

        public override double CalculateArea()
        {
            double flangeArea = FlangeWidth * FlangeThickness;
            double webArea = WebThickness * (TotalDepth - FlangeThickness);
            return flangeArea + webArea;
        }
    }
}
