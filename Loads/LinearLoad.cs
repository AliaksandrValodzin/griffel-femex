namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents a linear (line) load, which can be trapezoidal or uniform.
    /// Defined between two specific nodes.
    ///
    /// Which way it acts comes from <see cref="DistributedLoad"/>: the force along
    /// the resolved axis, the moment about it. A local direction needs a host to
    /// resolve against — <see cref="BarId"/> for a load on a member, and from 1.11
    /// <see cref="PlateId"/> for one on a panel's edge. The two-node form carries no
    /// roll angle of its own.
    ///
    /// From 1.9 that same host also carries the load's <i>extent</i>, where the
    /// author states one: <see cref="StartPosition"/> and <see cref="EndPosition"/>
    /// place the load along the host without inventing nodes at its ends. See
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
        /// The plate whose contour edge this load runs along (references
        /// <c>Element.Id</c> of a <see cref="griffel_femex.Geometry.Plate"/>) — the
        /// other host a line load may have, and from 1.11 the one that makes an
        /// edge-hosted load expressible at all.
        ///
        /// <b>At most one host.</b> A load naming both a bar and a plate says two
        /// different things about what its direction and its positions are measured
        /// against, and <see cref="FemexModel.Validate()"/> reports it.
        ///
        /// The <i>edge</i> itself is named by <see cref="StartNode"/> and
        /// <see cref="EndNode"/>, which must be adjacent in the named contour, and
        /// <b>their order is not cosmetic</b>: it is what local x runs along, so
        /// writing the same edge the other way round reverses x and y. The frame is
        /// the one <see cref="griffel_femex.BoundaryConditions.Hinge"/> states for a
        /// hinged edge and <see cref="FemexModel.TryGetEdgeLocalAxes"/> computes —
        /// stated there rather than restated here, because a convention written twice
        /// is a convention that can disagree with itself.
        ///
        /// Null on every load written before 1.11, and on every load that sits on a
        /// bar or carries a global direction and no extent.
        /// </summary>
        public int? PlateId { get; set; }

        /// <summary>
        /// References <c>PlateRegion.Id</c> within <see cref="PlateId"/>; null means
        /// the plate's own contour. The pair mirrors
        /// <c>Support.PlateId</c>/<c>Support.RegionId</c>, and it is the region's
        /// contour that <see cref="StartNode"/> and <see cref="EndNode"/> must be
        /// adjacent in when one is named.
        /// </summary>
        public int? RegionId { get; set; }

        /// <summary>
        /// Where along the host the load begins: relative, 0 at the start and 1 at
        /// the end. Measured along <see cref="BarId"/> from that bar's start node
        /// when there is a bar, and along the edge segment
        /// <see cref="StartNode"/> → <see cref="EndNode"/> when there is a plate.
        /// Null means the host's start, which is what every file written before 1.9
        /// means.
        /// </summary>
        public double? StartPosition { get; set; }

        /// <summary>
        /// Where along the host the load ends, on the same scale as
        /// <see cref="StartPosition"/>. Null means the host's end.
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
