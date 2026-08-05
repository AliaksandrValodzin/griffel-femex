namespace griffel_femex.Geometry.Surfaces
{
    /// <summary>
    /// A surface property of uniform thickness over the whole region it governs.
    /// </summary>
    public class ConstantThickness : SurfaceProperty
    {
        // Thickness measured along the plate normal.
        public double Thickness { get; set; }

        // Parameterless constructor for serialization
        public ConstantThickness() { }

        // Convenience constructor
        public ConstantThickness(int id, string? name, double thickness)
        {
            Id = id;
            Name = name;
            Thickness = thickness;
        }

        public override double GetNominalThickness()
        {
            return Thickness;
        }
    }
}
