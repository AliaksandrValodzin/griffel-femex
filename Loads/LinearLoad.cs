namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents a linear (line) load, which can be trapezoidal or uniform.
    /// Defined between two specific nodes.
    ///
    /// Which way it acts comes from <see cref="DistributedLoad"/>: the force along
    /// the resolved axis, the moment about it. A local direction needs a host to
    /// resolve against, which is what <see cref="BarId"/> is for — the two-node
    /// form carries no roll angle of its own.
    /// </summary>
    public class LinearLoad : DistributedLoad
    {
        public int StartNode { get; set; }
        public int EndNode { get; set; }

        /// <summary>
        /// The bar this load sits on (references <c>Element.Id</c> of a
        /// <see cref="griffel_femex.Geometry.Bar"/>), whose local axes a
        /// <see cref="LoadCoordinateSystem.Local"/> direction is measured in —
        /// including its <see cref="griffel_femex.Geometry.Bar.RotationAngle"/>, so
        /// a load on a rolled beam follows the beam.
        ///
        /// Optional, and required only for a local direction.
        /// <see cref="StartNode"/> and <see cref="EndNode"/> keep their own job as
        /// the load's extent, so a part-length load along a bar stays expressible.
        /// </summary>
        public int? BarId { get; set; }

        // Magnitudes (Force per unit length)
        public double MagnitudeStart { get; set; }
        public double MagnitudeEnd { get; set; }

        // Moment Magnitudes (Moment per unit length), about the resolved direction
        public double MomentStart { get; set; }
        public double MomentEnd { get; set; }
    }
}
