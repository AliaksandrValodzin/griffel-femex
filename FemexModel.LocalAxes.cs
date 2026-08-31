using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Loads;
using griffel_femex.Mesh;

namespace griffel_femex
{
    /// <summary>
    /// The element local-axis conventions and the load-direction rule, executable.
    ///
    /// The conventions themselves are stated in XML docs on <see cref="Bar"/>,
    /// <see cref="Plate"/> and <see cref="Hinge"/>; they live here as code as well
    /// so that a consumer and the format cannot disagree about them — the argument
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
        /// The local axes of a plate <b>edge</b>, in global coordinates: the frame a
        /// <see cref="Hinge"/> on that edge states its six releases in.
        ///
        /// <list type="bullet">
        /// <item>local <b>x</b> = the chord <paramref name="edgeStartNodeId"/> →
        /// <paramref name="edgeEndNodeId"/>, with its out-of-plane part removed.</item>
        /// <item>local <b>z</b> = the panel's own normal, exactly as
        /// <see cref="TryGetPlateLocalAxes"/> gives it.</item>
        /// <item>local <b>y</b> = ẑ × x̂, which for an edge taken in its contour's own
        /// order points <i>into</i> the panel.</item>
        /// </list>
        ///
        /// <b>The panel's <see cref="Plate.LocalAxisAngle"/> does not reach here</b>,
        /// and cannot: that angle turns the panel's x and y about the normal, and an
        /// edge takes its x from the edge. Nor does a region: z is the panel's normal
        /// whichever contour the edge belongs to, so a hinge on a void's edge and one
        /// on the outer contour agree about which side is up — which they would not if
        /// each contour's own winding decided.
        ///
        /// False when the plate is unknown or its contour degenerate, when either node
        /// does not resolve, or when the edge has no length in the panel's plane. The
        /// two nodes are <i>not</i> checked for being adjacent in a contour, or for
        /// belonging to the plate at all: <see cref="Validate()"/> reports that in its
        /// own words, and this stays a lookup.
        /// </summary>
        public bool TryGetEdgeLocalAxes(int plateId, int edgeStartNodeId, int edgeEndNodeId,
                                        out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            return TryGetPlateLocalAxes(plateId, out _, out _, out Vector3d normal)
                && TryGetAbsolutePoint(edgeStartNodeId, out Vector3d from)
                && TryGetAbsolutePoint(edgeEndNodeId, out Vector3d to)
                && TryGetEdgeAxes(normal, from, to, out x, out y, out z);
        }

        /// <summary>
        /// The frame a hinge's six releases are measured in — the whole of the
        /// convention <see cref="Hinge"/> states, in one call, so that a receiver
        /// turning a release into its own program's frame never has to decide which of
        /// the two rules applies:
        ///
        /// <list type="bullet">
        /// <item>a hinge on a <b>bar</b> gets that bar's own local axes, roll
        /// included — <see cref="TryGetBarLocalAxes"/>.</item>
        /// <item>a hinge on a <b>plate edge</b> gets the edge frame —
        /// <see cref="TryGetEdgeLocalAxes"/> — for the edge its
        /// <see cref="Hinge.EdgeStartNodeId"/>/<see cref="Hinge.EdgeEndNodeId"/> name,
        /// or, where it names none, for the edge <see cref="Hinge.EndOrEdgeIndex"/>
        /// indexes in the contour it belongs to.</item>
        /// <item>a hinge on a <b>mesh face</b> gets the same edge rule over the face's
        /// own nodes and normal, indexed the only way a face can be: a generated face
        /// has no named edge.</item>
        /// </list>
        ///
        /// False for a hinge with no element, an element whose geometry does not
        /// resolve, a region that does not exist, or an
        /// <see cref="Hinge.EndOrEdgeIndex"/> outside the contour it indexes. Every one
        /// of those <see cref="Validate()"/> reports.
        /// </summary>
        public bool TryGetHingeLocalAxes(Hinge hinge, out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            if (hinge is null)
                return false;

            if (Bars.Exists(b => b.Id == hinge.ElementId))
                return TryGetBarLocalAxes(hinge.ElementId, out x, out y, out z);

            Plate? plate = Plates.Find(p => p.Id == hinge.ElementId);
            if (plate is not null)
            {
                if (hinge.EdgeStartNodeId.HasValue && hinge.EdgeEndNodeId.HasValue)
                {
                    return TryGetEdgeLocalAxes(plate.Id, hinge.EdgeStartNodeId.Value,
                                               hinge.EdgeEndNodeId.Value, out x, out y, out z);
                }

                // No named edge: the index falls back to the contour it belongs to,
                // which is the reading the viewer already draws.
                List<int> contour = plate.NodeIds;
                if (hinge.RegionId.HasValue)
                {
                    PlateRegion? region = plate.Regions.Find(r => r.Id == hinge.RegionId.Value);
                    if (region is null)
                        return false;

                    contour = region.NodeIds;
                }

                return TryGetEdgeEnds(contour.Count, hinge.EndOrEdgeIndex, out int first, out int second)
                    && TryGetEdgeLocalAxes(plate.Id, contour[first], contour[second], out x, out y, out z);
            }

            MeshFace? face = Mesh?.Faces.Find(f => f.Id == hinge.ElementId);
            if (face is null)
                return false;

            return TryGetMeshPoints(face.NodeIds, out Vector3d[]? points)
                && TryGetNewellNormal(points!, out Vector3d faceNormal)
                && TryGetEdgeEnds(points!.Length, hinge.EndOrEdgeIndex, out int i, out int j)
                && TryGetEdgeAxes(faceNormal, points[i], points[j], out x, out y, out z);
        }

        /// <summary>
        /// The edge rule itself, given a surface normal and the edge's two ends: the
        /// chord projected into the surface for x, the normal for z, ẑ × x̂ for y.
        /// False when the chord has nothing left once its out-of-plane part is
        /// removed — an edge of no length, or one running along the normal.
        /// </summary>
        private static bool TryGetEdgeAxes(Vector3d normal, Vector3d from, Vector3d to,
                                           out Vector3d x, out Vector3d y, out Vector3d z)
        {
            x = y = z = Vector3d.Zero;

            Vector3d chord = to - from;
            Vector3d along = (chord - normal * normal.Dot(chord)).Normalized();
            if (along.Length <= 0.0)
                return false;

            x = along;
            z = normal;
            y = z.Cross(x);
            return true;
        }

        /// <summary>
        /// The two positions in a closed contour of <paramref name="count"/> points
        /// that edge <paramref name="index"/> runs between: the index itself and the
        /// next one round. False for a contour too short to have an edge, or an index
        /// outside it — the same reading <see cref="Validate()"/> takes of an
        /// <see cref="Hinge.EndOrEdgeIndex"/> it rejects, rather than a silent clamp
        /// onto some other edge.
        /// </summary>
        private static bool TryGetEdgeEnds(int count, int index, out int first, out int second)
        {
            first = second = 0;

            if (count < 2 || index < 0 || index >= count)
                return false;

            first = index;
            second = (index + 1) % count;
            return true;
        }

        /// <summary>
        /// Every node of a mesh face in absolute coordinates, or false as soon as one
        /// of them does not resolve. A mesh node carries its own absolute z, so this is
        /// a lookup where <see cref="TryGetAbsolutePoints"/> is a resolution.
        /// </summary>
        private bool TryGetMeshPoints(IReadOnlyList<int> meshNodeIds, out Vector3d[]? points)
        {
            points = null;

            if (Mesh is null)
                return false;

            var resolved = new Vector3d[meshNodeIds.Count];
            for (int i = 0; i < meshNodeIds.Count; i++)
            {
                MeshNode? node = Mesh.Nodes.Find(n => n.Id == meshNodeIds[i]);
                if (node is null)
                    return false;

                resolved[i] = new Vector3d(node.X, node.Y, node.Z);
            }

            points = resolved;
            return true;
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
        /// the targeted plate, the free polygon's own contour, the bar named by
        /// <see cref="LinearLoad.BarId"/>, or — from 1.11 — the contour edge of the
        /// plate named by <see cref="LinearLoad.PlateId"/>. False when there is no
        /// host, which for a line load naming neither is exactly the error
        /// <see cref="Validate()"/> reports.
        ///
        /// The edge case is <see cref="TryGetEdgeLocalAxes"/> and nothing else: the
        /// frame a load on a panel's edge is measured in is the frame a
        /// <see cref="Hinge"/> on that same edge already states, and stating it twice
        /// is how the two would come to disagree.
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

                case LinearLoad line when line.PlateId.HasValue:
                    return TryGetEdgeLocalAxes(line.PlateId.Value, line.StartNode, line.EndNode,
                                               out x, out y, out z);

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
