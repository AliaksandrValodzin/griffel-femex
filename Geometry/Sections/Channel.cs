namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Channel — UPN, UPE, PFC, C: two flanges joined by a web at one edge rather
    /// than on the centreline. The four field names are <see cref="ISection"/>'s and
    /// <see cref="TSection"/>'s exactly.
    /// </summary>
    public class Channel : Section
    {
        // Overall width of each flange
        public double FlangeWidth { get; set; }

        // Thickness of each flange
        public double FlangeThickness { get; set; }

        // Thickness (depth) of the web
        public double WebThickness { get; set; }

        // Overall depth of the section, flange face to flange face
        public double TotalDepth { get; set; }

        // Parameterless constructor for serialization
        public Channel() { }

        // Convenience constructor
        public Channel(int id, string? name, double flangeWidth, double flangeThickness, double webThickness, double totalDepth)
        {
            Id = id;
            Name = name;
            FlangeWidth = flangeWidth;
            FlangeThickness = flangeThickness;
            WebThickness = webThickness;
            TotalDepth = totalDepth;
        }

        /// <summary>
        /// 2·bf·tf + tw·(h − 2·tf) — deliberately the same formula as
        /// <see cref="ISection.CalculateArea"/>. A channel and an I differ only in
        /// where the web sits, which moves the centroid and not the area.
        /// </summary>
        public override double CalculateArea()
        {
            double flangeArea = 2.0 * FlangeWidth * FlangeThickness;
            double webArea = WebThickness * (TotalDepth - 2.0 * FlangeThickness);
            return flangeArea + webArea;
        }
    }
}
