namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Doubly-symmetric I or H: two equal flanges joined by a web on the centreline.
    /// It covers IPE, HEA, HEB, UB, UC and W, which is very nearly all rolled steel.
    /// Singly-symmetric, tapered and compound shapes are reserved on
    /// <see cref="Section"/> and not implemented.
    ///
    /// The four field names are <see cref="TSection"/>'s exactly, so the union reads
    /// as one vocabulary rather than as unrelated classes.
    /// </summary>
    public class ISection : Section
    {
        // Overall width of each flange
        public double FlangeWidth { get; set; }

        // Thickness of each flange
        public double FlangeThickness { get; set; }

        // Thickness (width) of the web
        public double WebThickness { get; set; }

        // Overall depth of the section, flange face to flange face
        public double TotalDepth { get; set; }

        // Parameterless constructor for serialization
        public ISection() { }

        // Convenience constructor
        public ISection(int id, string? name, double flangeWidth, double flangeThickness, double webThickness, double totalDepth)
        {
            Id = id;
            Name = name;
            FlangeWidth = flangeWidth;
            FlangeThickness = flangeThickness;
            WebThickness = webThickness;
            TotalDepth = totalDepth;
        }

        /// <summary>
        /// 2·bf·tf + tw·(h − 2·tf). Shared with <see cref="Channel"/>, which differs
        /// only in where the web sits: that moves the centroid, not the area.
        ///
        /// Idealised, so it carries no root radii — what this gives for an IPE300 is
        /// 3.6% below the tabulated area. A section that states its own area is
        /// believed over this; see <see cref="Section.GetArea"/>.
        /// </summary>
        public override double CalculateArea()
        {
            double flangeArea = 2.0 * FlangeWidth * FlangeThickness;
            double webArea = WebThickness * (TotalDepth - 2.0 * FlangeThickness);
            return flangeArea + webArea;
        }
    }
}
