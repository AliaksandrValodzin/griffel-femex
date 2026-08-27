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
    ///
    /// From 1.9 that same host also carries the load's <i>extent</i>, where the
    /// author states one: <see cref="StartPosition"/> and <see cref="EndPosition"/>
    /// place the load along the bar without inventing nodes at its ends. See
    /// <see cref="PointLoad"/> for why the position is stored rather than resolved.
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
        /// Optional, and required for a local direction and for either of the two
        /// positions below. <see cref="StartNode"/> and <see cref="EndNode"/> keep
        /// their own job as the load's extent, so a part-length load along a bar
        /// stays expressible without them.
        /// </summary>
        public int? BarId { get; set; }

        /// <summary>
        /// Where along <see cref="BarId"/> the load begins: relative, 0 at the bar's
        /// start node and 1 at its end node. Null means the bar's start, which is
        /// what every file written before 1.9 means.
        /// </summary>
        public double? StartPosition { get; set; }

        /// <summary>
        /// Where along <see cref="BarId"/> the load ends, on the same scale as
        /// <see cref="StartPosition"/>. Null means the bar's end.
        /// </summary>
        public double? EndPosition { get; set; }

        // Magnitudes (Force per unit length)
        public double MagnitudeStart { get; set; }
        public double MagnitudeEnd { get; set; }

        // Moment Magnitudes (Moment per unit length), about the resolved direction
        public double MomentStart { get; set; }
        public double MomentEnd { get; set; }
    }
}
