using griffel_femex.Geometry;
using griffel_femex.Loads;

namespace griffel_femex
{
    /// <summary>
    /// The element local-axis conventions and the load-direction rule, executable.
    ///
    /// The conventions themselves are stated in XML docs on <see cref="Bar"/> and
    /// <see cref="Plate"/>; they live here as code as well so that a consumer and
    /// the format cannot disagree about them — the argument
    /// <see cref="GetDesignEnvelope"/> and <see cref="GetGridsForLevel"/> already
    /// made for their own rules.
    ///
    /// Every member is a lookup: none of them changes the model, and an unresolvable
    /// host or a degenerate geometry is answered with false rather than an
    /// exception. <see cref="Validate()"/> reports those cases separately, so these
    /// stay silent.
    /// </summary>
    public partial class FemexModel
    {
        /// <summary>
        /// Below this, Newell's sum — twice the contour's vector area — is taken as
        /// collinear or zero-area, and the contour has no normal.
        /// </summary>
        private const double DegenerateContourArea = 1e-12;

        /// <summary>
        /// The local axes of a bar, in global coordinates. False when the bar is
        /// unknown, when either of its nodes does not resolve, or when its two ends
        /// are at the same place and it therefore has no direction.
        ///
        /// See <see cref="Bar"/> for the convention. A bar counts as vertical when
        /// its two ends are coincident <i>in plan</i> to within
        /// <see cref="GetCoincidenceTolerance"/> — the same tolerance, used as the
        /// distance it actually is.
        /// </summary>
        public bool TryGetBarLocalAxes(int barId, out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            Bar? bar = Bars.Find(b => b.Id == barId);
            if (bar is null)
                return false;

            if (!TryGetAbsolutePoint(bar.StartNodeId, out Vector3d start) ||
                !TryGetAbsolutePoint(bar.EndNodeId, out Vector3d end))
                return false;

            Vector3d axis = end - start;
            if (axis.Length <= 0.0)
                return false;

            x = axis.Normalized();

            // Vertical when the two ends are at the same point in plan: the usual
            // construction below has nothing left to project once the member is
            // along Ẑ.
            if (Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y) <= GetCoincidenceTolerance())
            {
                // ETABS' substitution. Taking z as the cross product rather than
                // literally as global +Y keeps the triad right-handed for a bar
                // drawn downward too; for the upward bar the convention describes,
                // it is global +Y either way.
                y = Vector3d.UnitX;
                z = x.Cross(y);
            }
            else
            {
                // Global up with the axial part removed: in the vertical plane
                // through the member, pointing upward.
                y = (Vector3d.UnitZ - x * Vector3d.UnitZ.Dot(x)).Normalized();
                z = x.Cross(y);
            }

            Roll(x, bar.RotationAngle, ref y, ref z);
            return true;
        }

        /// <summary>
        /// The local axes of a plate, in global coordinates. False when the plate is
        /// unknown, when a contour node does not resolve, or when the contour is
        /// degenerate — collinear, zero-area, or with its first chord parallel to
        /// its own normal.
        /// </summary>
        public bool TryGetPlateLocalAxes(int plateId, out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            Plate? plate = Plates.Find(p => p.Id == plateId);
            if (plate is null)
                return false;

            return TryGetAbsolutePoints(plate.NodeIds, out Vector3d[]? points)
                && TryGetContourAxes(points!, plate.LocalAxisAngle, out x, out y, out z);
        }

        /// <summary>
        /// The one call a consumer actually needs: the global unit vector a
        /// distributed load's force acts along, and its moment acts about.
        ///
        /// Resolves <see cref="LoadCoordinateSystem"/> × <see cref="LoadDirection"/>
        /// for any <see cref="DistributedLoad"/>. False for a load that carries no
        /// direction at all — a <see cref="PointLoad"/> or a
        /// <see cref="TemperatureLoad"/> — and equally when the host element or the
        /// geometry does not resolve, or when a <see cref="LoadDirection.Vector"/>
        /// load's components are absent or all zero. Every one of those is reported
        /// by <see cref="Validate()"/> in its own words.
        ///
        /// The sign is the direction's alone: it is a unit vector, and the load's
        /// own sign stays in its magnitude.
        /// </summary>
        public bool TryGetLoadDirection(Load load, out Vector3d direction)
        {
            direction = Vector3d.Zero;

            if (load is not DistributedLoad distributed)
                return false;

            Vector3d local;

            switch (distributed.Direction)
            {
                case LoadDirection.Vector:
                    if (distributed.Dx is null || distributed.Dy is null || distributed.Dz is null)
                        return false;

                    local = new Vector3d(distributed.Dx.Value, distributed.Dy.Value, distributed.Dz.Value).Normalized();
                    if (local.Length <= 0.0)
                        return false;
                    break;

                case LoadDirection.X:
                    local = Vector3d.UnitX;
                    break;

                case LoadDirection.Y:
                    local = Vector3d.UnitY;
                    break;

                default:
                    local = Vector3d.UnitZ;
                    break;
            }

            if (distributed.CoordinateSystem == LoadCoordinateSystem.Global)
            {
                direction = local;
                return true;
            }

            if (!TryGetHostAxes(distributed, out Vector3d x, out Vector3d y, out Vector3d z))
                return false;

            direction = (x * local.X + y * local.Y + z * local.Z).Normalized();
            return direction.Length > 0.0;
        }

        /// <summary>
        /// The local axes of whatever a load's local direction is measured against:
        /// the targeted plate, the free polygon's own contour, or the bar named by
        /// <see cref="LinearLoad.BarId"/>. False when there is no host — which for a
        /// line load without a <c>barId</c> is exactly the error
        /// <see cref="Validate()"/> reports.
        /// </summary>
        private bool TryGetHostAxes(DistributedLoad load, out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            switch (load)
            {
                case AreaLoad area when area.PlateId.HasValue:
                    return TryGetPlateLocalAxes(area.PlateId.Value, out x, out y, out z);

                // A free polygon is its own host, by the plate rule with no angle.
                case AreaLoad area when area.NodeSequence is { Count: >= 3 }:
                    return TryGetAbsolutePoints(area.NodeSequence, out Vector3d[]? points)
                        && TryGetContourAxes(points!, 0.0, out x, out y, out z);

                case LinearLoad line when line.BarId.HasValue:
                    return TryGetBarLocalAxes(line.BarId.Value, out x, out y, out z);

                default:
                    return false;
            }
        }

        /// <summary>
        /// The plate rule applied to any closed contour: Newell's normal for local
        /// z, the first chord projected into the plane and rotated by
        /// <paramref name="localAxisAngle"/> degrees for local x, and ẑ × x̂ for
        /// local y.
        /// </summary>
        private static bool TryGetContourAxes(
            IReadOnlyList<Vector3d> points, double localAxisAngle,
            out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            if (!TryGetNewellNormal(points, out z))
                return false;

            // The first chord with its out-of-plane part removed. A contour whose
            // first two nodes coincide leaves nothing to normalize.
            Vector3d chord = points[1] - points[0];
            x = (chord - z * z.Dot(chord)).Normalized();
            if (x.Length <= 0.0)
            {
                z = Vector3d.Zero;
                return false;
            }

            y = z.Cross(x);

            // The angle turns x and y about z, which is the same rotation Roll
            // performs about a bar's own axis — counter-clockwise seen from +z.
            Roll(z, localAxisAngle, ref x, ref y);
            return true;
        }

        /// <summary>
        /// The unit normal of a closed contour by Newell's method, which gives a
        /// correct normal for non-convex polygons: right-hand rule over the points
        /// in order, so a contour that is counter-clockwise seen from above has
        /// normal +Z. False when the contour is collinear or of zero area, and so
        /// has no normal.
        /// </summary>
        private static bool TryGetNewellNormal(IReadOnlyList<Vector3d> points, out Vector3d normal)
        {
            normal = Vector3d.Zero;

            int count = points.Count;
            if (count < 3)
                return false;

            double nx = 0.0, ny = 0.0, nz = 0.0;

            for (int i = 0; i < count; i++)
            {
                Vector3d a = points[i];
                Vector3d b = points[(i + 1) % count];

                nx += (a.Y - b.Y) * (a.Z + b.Z);
                ny += (a.Z - b.Z) * (a.X + b.X);
                nz += (a.X - b.X) * (a.Y + b.Y);
            }

            // Twice the vector area, so its own floor rather than a length one.
            var sum = new Vector3d(nx, ny, nz);
            if (sum.Length < DegenerateContourArea)
                return false;

            normal = sum.Normalized();
            return true;
        }

        /// <summary>
        /// Rotates the two axes perpendicular to <paramref name="axis"/> about it by
        /// <paramref name="degrees"/>, right-hand rule — counter-clockwise seen from
        /// the positive end of the axis. Both a bar's roll and a plate's
        /// local-axis angle are this one operation.
        /// </summary>
        private static void Roll(Vector3d axis, double degrees, ref Vector3d first, ref Vector3d second)
        {
            if (degrees == 0.0)
                return;

            double radians = degrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            Vector3d rotated = first * cos + second * sin;
            first = rotated.Normalized();
            second = axis.Cross(first);
        }

        /// <summary>
        /// Every node of a sequence in absolute coordinates, or false as soon as one
        /// of them does not resolve.
        /// </summary>
        private bool TryGetAbsolutePoints(IReadOnlyList<int> nodeNumbers, out Vector3d[]? points)
        {
            points = null;

            var resolved = new Vector3d[nodeNumbers.Count];
            for (int i = 0; i < nodeNumbers.Count; i++)
            {
                if (!TryGetAbsolutePoint(nodeNumbers[i], out resolved[i]))
                    return false;
            }

            points = resolved;
            return true;
        }

        /// <summary>
        /// Resolves a node number to absolute coordinates. False when the node or
        /// its level is unknown, both of which <see cref="Validate()"/> reports.
        /// </summary>
        private bool TryGetAbsolutePoint(int nodeNumber, out Vector3d point)
        {
            point = Vector3d.Zero;

            Node? node = Nodes.Find(n => n.NodeNumber == nodeNumber);
            if (node is null || !TryGetAbsolutePoint(node, out double x, out double y, out double z))
                return false;

            point = new Vector3d(x, y, z);
            return true;
        }
    }
}
