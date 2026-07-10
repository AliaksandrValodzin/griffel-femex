namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents a point load and moment applied directly to a node.
    /// </summary>
    public class PointLoad : Load
    {
        public int NodeNumber { get; set; }

        // Forces in X, Y, Z directions
        public double Fx { get; set; }
        public double Fy { get; set; }
        public double Fz { get; set; }

        // Moments about X, Y, Z axes
        public double Mx { get; set; }
        public double My { get; set; }
        public double Mz { get; set; }
    }
}
