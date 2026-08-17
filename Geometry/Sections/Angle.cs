namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Angle — L: two legs of one thickness meeting at a right angle. Equal and
    /// unequal alike, the two leg lengths being stated separately.
    ///
    /// <b>Geometric axes only.</b> An angle's principal axes are rotated from its
    /// legs, and FEMEX states no <c>Iu</c>, <c>Iv</c> or principal angle, so an angle
    /// crosses with geometric-axis stiffness — a real approximation for a single
    /// angle in bending.
    /// </summary>
    public class Angle : Section
    {
        // Length of the leg along local y
        public double LegLengthY { get; set; }

        // Length of the leg along local z
        public double LegLengthZ { get; set; }

        // Thickness, the same in both legs
        public double Thickness { get; set; }

        // Parameterless constructor for serialization
        public Angle() { }

        // Convenience constructor
        public Angle(int id, string? name, double legLengthY, double legLengthZ, double thickness)
        {
            Id = id;
            Name = name;
            LegLengthY = legLengthY;
            LegLengthZ = legLengthZ;
            Thickness = thickness;
        }

        /// <summary>
        /// (ly + lz − t)·t — the two legs less the corner they share, which would
        /// otherwise be counted twice.
        /// </summary>
        public override double CalculateArea()
        {
            return (LegLengthY + LegLengthZ - Thickness) * Thickness;
        }
    }
}
