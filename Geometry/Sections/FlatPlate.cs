namespace griffel_femex.Geometry.Sections
{
    // 1. FlatPlate Section (Primarily for Plates/Slabs/Walls)
    public class FlatPlate : Section
    {
        public double Thickness { get; set; }

        public FlatPlate(string name, double thickness)
        {
            Name = name;
            Thickness = thickness;
        }

        public override double CalculateArea()
        {
            // For a plate, "Area" is often contextual (per unit width, say 1 m) 
            return Thickness * 1;
        }
    }
}
