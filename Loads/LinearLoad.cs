namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents a linear (line) load, which can be trapezoidal or uniform.
    /// Defined between two specific nodes.
    /// </summary>
    public class LinearLoad : Load
    {
        public int StartNode { get; set; }
        public int EndNode { get; set; }

        // Magnitudes (Force per unit length)
        public double MagnitudeStart { get; set; }
        public double MagnitudeEnd { get; set; }

        // Moment Magnitudes (Moment per unit length)
        public double MomentStart { get; set; }
        public double MomentEnd { get; set; }
    }
}
