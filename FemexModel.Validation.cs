using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Loads;

namespace griffel_femex
{
    /// <summary>
    /// Referential and geometric integrity checks for <see cref="FemexModel"/>.
    /// Deferred and non-throwing: one human-readable message per problem, and an
    /// empty sequence means the model is consistent. Not required for serialization.
    /// </summary>
    public partial class FemexModel
    {
        public IEnumerable<string> Validate()
        {
            var ctx = new ValidationContext(this);

            foreach (var message in ValidateDuplicateIds(ctx)) yield return message;
            foreach (var message in ValidateNodes(ctx)) yield return message;
            foreach (var message in ValidateBars(ctx)) yield return message;
            foreach (var message in ValidatePlates(ctx)) yield return message;
            foreach (var message in ValidateLoads(ctx)) yield return message;
            foreach (var message in ValidateBoundaryConditions(ctx)) yield return message;
            foreach (var message in ValidateMesh(ctx)) yield return message;

            // Geometric checks last: they are the only ones that need coordinates,
            // and the only ones that can be approximate.
            foreach (var message in ValidateContourPlanarity(ctx)) yield return message;
            foreach (var message in ValidateRegionPriorities(ctx)) yield return message;
        }

        // ----- Ids -----

        private IEnumerable<string> ValidateDuplicateIds(ValidationContext ctx)
        {
            foreach (var m in ReportDuplicates(Levels.Select(l => l.LevelNumber), "level number")) yield return m;
            foreach (var m in ReportDuplicates(Nodes.Select(n => n.NodeNumber), "node number")) yield return m;
            foreach (var m in ReportDuplicates(Sections.Select(s => s.Id), "section id")) yield return m;
            foreach (var m in ReportDuplicates(SurfaceProperties.Select(s => s.Id), "surface property id")) yield return m;
            foreach (var m in ReportDuplicates(Materials.Select(m2 => m2.Id), "material id")) yield return m;
            foreach (var m in ReportDuplicates(LoadCases.Select(c => c.Number), "load case number")) yield return m;
            foreach (var m in ReportDuplicates(Supports.Select(s => s.Id), "support id")) yield return m;
            foreach (var m in ReportDuplicates(Hinges.Select(h => h.Id), "hinge id")) yield return m;

            if (Mesh is not null)
            {
                foreach (var m in ReportDuplicates(Mesh.Nodes.Select(n => n.Id), "mesh node id")) yield return m;
            }

            // Bars, plates and mesh faces share one element-id space.
            var seen = new HashSet<int>();
            var reported = new HashSet<int>();
            foreach (int id in ctx.AllElementIdsInOrder)
            {
                if (!seen.Add(id) && reported.Add(id))
                    yield return $"Element id {id} is used by more than one of bar/plate/mesh face.";
            }
        }

        private static IEnumerable<string> ReportDuplicates(IEnumerable<int> ids, string what)
        {
            var seen = new HashSet<int>();
            var reported = new HashSet<int>();
            foreach (int id in ids)
            {
                if (!seen.Add(id) && reported.Add(id))
                    yield return $"Duplicate {what} {id}.";
            }
        }

        // ----- Geometry -----

        private IEnumerable<string> ValidateNodes(ValidationContext ctx)
        {
            foreach (var node in Nodes)
            {
                if (!ctx.LevelNumbers.Contains(node.LevelNumber))
                    yield return $"Node {node.NodeNumber} references unknown level {node.LevelNumber}.";
            }
        }

        private IEnumerable<string> ValidateBars(ValidationContext ctx)
        {
            foreach (var bar in Bars)
            {
                if (!ctx.NodeNumbers.Contains(bar.StartNodeId))
                    yield return $"Bar {bar.Id} references unknown start node {bar.StartNodeId}.";
                if (!ctx.NodeNumbers.Contains(bar.EndNodeId))
                    yield return $"Bar {bar.Id} references unknown end node {bar.EndNodeId}.";
                if (!ctx.SectionIds.Contains(bar.SectionId))
                    yield return $"Bar {bar.Id} references unknown section {bar.SectionId}.";
                if (!ctx.MaterialIds.Contains(bar.MaterialId))
                    yield return $"Bar {bar.Id} references unknown material {bar.MaterialId}.";
            }
        }

        private IEnumerable<string> ValidatePlates(ValidationContext ctx)
        {
            foreach (var plate in Plates)
            {
                string owner = $"Plate {plate.Id}";

                foreach (var m in ValidateContour(ctx, $"{owner} outer contour", plate.NodeIds))
                    yield return m;

                foreach (var m in ValidateSurfaceAndMaterial(
                             ctx, owner, plate.Kind, plate.SurfacePropertyId, plate.MaterialId,
                             plate.SurfacePropertyId, plate.MaterialId))
                    yield return m;

                var regionIds = new HashSet<int>();
                var reported = new HashSet<int>();

                foreach (var region in plate.Regions)
                {
                    if (!regionIds.Add(region.Id) && reported.Add(region.Id))
                        yield return $"{owner} has more than one region with id {region.Id}.";

                    string regionOwner = $"{owner} region {region.Id}";

                    foreach (var m in ValidateContour(ctx, $"{regionOwner} contour", region.NodeIds))
                        yield return m;

                    // Null on a region means "inherit from the plate".
                    foreach (var m in ValidateSurfaceAndMaterial(
                                 ctx, regionOwner, region.Kind,
                                 region.SurfacePropertyId, region.MaterialId,
                                 region.SurfacePropertyId ?? plate.SurfacePropertyId,
                                 region.MaterialId ?? plate.MaterialId))
                        yield return m;
                }
            }
        }

        /// <summary>
        /// Checks that the ids a plate or region declares exist, and that they are
        /// consistent with its kind. The "effective" values already have region
        /// inheritance from the parent plate applied.
        /// </summary>
        private static IEnumerable<string> ValidateSurfaceAndMaterial(
            ValidationContext ctx, string owner, PlateRegionKind kind,
            int? declaredSurfacePropertyId, int? declaredMaterialId,
            int? effectiveSurfacePropertyId, int? effectiveMaterialId)
        {
            if (declaredSurfacePropertyId.HasValue && !ctx.SurfacePropertyIds.Contains(declaredSurfacePropertyId.Value))
                yield return $"{owner} references unknown surface property {declaredSurfacePropertyId.Value}.";

            if (declaredMaterialId.HasValue && !ctx.MaterialIds.Contains(declaredMaterialId.Value))
                yield return $"{owner} references unknown material {declaredMaterialId.Value}.";

            switch (kind)
            {
                case PlateRegionKind.Opening:
                    // An opening generates nothing, so carrying either is a mistake.
                    if (declaredSurfacePropertyId.HasValue)
                        yield return $"{owner} is an Opening but carries surface property {declaredSurfacePropertyId.Value}.";
                    if (declaredMaterialId.HasValue)
                        yield return $"{owner} is an Opening but carries material {declaredMaterialId.Value}.";
                    break;

                case PlateRegionKind.Structural:
                    if (effectiveSurfacePropertyId is null)
                        yield return $"{owner} is Structural but has no surface property.";
                    if (effectiveMaterialId is null)
                        yield return $"{owner} is Structural but has no material.";
                    break;

                // LoadOnly is deliberately unchecked: it may legitimately carry a
                // thickness and material for self-weight and reporting, or neither.
            }
        }

        private static IEnumerable<string> ValidateContour(ValidationContext ctx, string owner, List<int> nodeIds)
        {
            if (nodeIds.Count < 3)
                yield return $"{owner} has {nodeIds.Count} nodes; at least 3 are required.";

            var seen = new HashSet<int>();
            var reported = new HashSet<int>();

            foreach (int nodeId in nodeIds)
            {
                if (!ctx.NodeNumbers.Contains(nodeId))
                    yield return $"{owner} references unknown node {nodeId}.";

                if (!seen.Add(nodeId) && reported.Add(nodeId))
                    yield return $"{owner} repeats node {nodeId}.";
            }
        }

        // ----- Loads -----

        private IEnumerable<string> ValidateLoads(ValidationContext ctx)
        {
            foreach (var load in Loads)
            {
                if (!ctx.LoadCaseNumbers.Contains(load.LoadCaseNumber))
                    yield return $"Load '{load.Label}' references unknown load case {load.LoadCaseNumber}.";

                switch (load)
                {
                    case PointLoad pl when !ctx.NodeNumbers.Contains(pl.NodeNumber):
                        yield return $"Point load '{pl.Label}' references unknown node {pl.NodeNumber}.";
                        break;

                    case LinearLoad ll:
                        if (!ctx.NodeNumbers.Contains(ll.StartNode))
                            yield return $"Linear load '{ll.Label}' references unknown start node {ll.StartNode}.";
                        if (!ctx.NodeNumbers.Contains(ll.EndNode))
                            yield return $"Linear load '{ll.Label}' references unknown end node {ll.EndNode}.";
                        break;

                    case AreaLoad al:
                        foreach (var m in ValidateAreaLoad(ctx, al))
                            yield return m;
                        break;

                    case TemperatureLoad tl:
                        foreach (int elementId in tl.ElementIds)
                            if (!ctx.ElementIds.Contains(elementId))
                                yield return $"Temperature load '{tl.Label}' references unknown element {elementId}.";
                        break;
                }
            }
        }

        private static IEnumerable<string> ValidateAreaLoad(ValidationContext ctx, AreaLoad load)
        {
            bool hasPlate = load.PlateId.HasValue;
            bool hasPolygon = load.NodeSequence is { Count: > 0 };

            if (hasPlate && hasPolygon)
                yield return $"Area load '{load.Label}' sets both plateId and nodeSequence; use one.";
            else if (!hasPlate && !hasPolygon)
                yield return $"Area load '{load.Label}' has no target.";

            if (hasPlate)
            {
                if (!ctx.PlatesById.TryGetValue(load.PlateId!.Value, out Plate? plate))
                    yield return $"Area load '{load.Label}' references unknown plate {load.PlateId.Value}.";
                else if (load.RegionId.HasValue && !plate.Regions.Exists(r => r.Id == load.RegionId.Value))
                    yield return $"Area load '{load.Label}' references region {load.RegionId.Value}, which does not exist on plate {load.PlateId.Value}.";
            }
            else if (load.RegionId.HasValue)
            {
                yield return $"Area load '{load.Label}' sets regionId without plateId.";
            }

            if (hasPolygon)
            {
                if (load.NodeSequence!.Count < 3)
                    yield return $"Area load '{load.Label}' polygon has {load.NodeSequence.Count} nodes; at least 3 are required.";

                foreach (int nodeId in load.NodeSequence)
                    if (!ctx.NodeNumbers.Contains(nodeId))
                        yield return $"Area load '{load.Label}' references unknown node {nodeId}.";
            }
        }

        // ----- Boundary conditions -----

        private IEnumerable<string> ValidateBoundaryConditions(ValidationContext ctx)
        {
            foreach (var support in Supports)
            {
                foreach (int nodeId in support.NodeIds)
                    if (!ctx.NodeNumbers.Contains(nodeId))
                        yield return $"Support {support.Id} references unknown node {nodeId}.";

                if (support.PlateId.HasValue)
                {
                    if (support.Target != SupportTarget.Area)
                        yield return $"Support {support.Id} follows a plate but its target is {support.Target}; only an Area support may do that.";

                    if (!ctx.PlatesById.TryGetValue(support.PlateId.Value, out Plate? plate))
                        yield return $"Support {support.Id} references unknown plate {support.PlateId.Value}.";
                    else if (support.RegionId.HasValue && !plate.Regions.Exists(r => r.Id == support.RegionId.Value))
                        yield return $"Support {support.Id} references region {support.RegionId.Value}, which does not exist on plate {support.PlateId.Value}.";
                }
                else
                {
                    if (support.RegionId.HasValue)
                        yield return $"Support {support.Id} sets regionId without plateId.";

                    if (support.Target == SupportTarget.Area && support.NodeIds.Count == 0)
                        yield return $"Support {support.Id} is an area support with neither a plate nor any nodes.";
                }
            }

            foreach (var hinge in Hinges)
            {
                foreach (var m in ValidateHinge(ctx, hinge))
                    yield return m;
            }
        }

        private static IEnumerable<string> ValidateHinge(ValidationContext ctx, Hinge hinge)
        {
            if (!ctx.ElementIds.Contains(hinge.ElementId))
                yield return $"Hinge {hinge.Id} references unknown element {hinge.ElementId}.";

            foreach (int nodeId in hinge.NodeIds)
                if (!ctx.NodeNumbers.Contains(nodeId))
                    yield return $"Hinge {hinge.Id} references unknown node {nodeId}.";

            if (!ctx.PlatesById.TryGetValue(hinge.ElementId, out Plate? plate))
            {
                // A bar or a mesh face: the plate-edge fields must be unused.
                if (hinge.RegionId.HasValue)
                    yield return $"Hinge {hinge.Id} sets regionId but element {hinge.ElementId} is not a plate.";
                if (hinge.EdgeStartNodeId.HasValue || hinge.EdgeEndNodeId.HasValue)
                    yield return $"Hinge {hinge.Id} sets plate edge nodes but element {hinge.ElementId} is not a plate.";
                if (ctx.BarIds.Contains(hinge.ElementId) && hinge.EndOrEdgeIndex != 0 && hinge.EndOrEdgeIndex != 1)
                    yield return $"Hinge {hinge.Id} targets bar {hinge.ElementId} with end index {hinge.EndOrEdgeIndex}; expected 0 or 1.";

                yield break;
            }

            List<int>? contour = plate.NodeIds;
            string where = "the contour";

            if (hinge.RegionId.HasValue)
            {
                PlateRegion? region = plate.Regions.Find(r => r.Id == hinge.RegionId.Value);
                if (region is null)
                {
                    yield return $"Hinge {hinge.Id} references region {hinge.RegionId.Value}, which does not exist on plate {hinge.ElementId}.";
                    contour = null;
                }
                else
                {
                    contour = region.NodeIds;
                    where = $"region {region.Id}'s contour";
                }
            }

            if (hinge.EdgeStartNodeId.HasValue != hinge.EdgeEndNodeId.HasValue)
            {
                yield return $"Hinge {hinge.Id} sets only one end of its plate edge; set both or neither.";
            }
            else if (hinge.EdgeStartNodeId.HasValue && contour is not null)
            {
                int start = hinge.EdgeStartNodeId.Value;
                int end = hinge.EdgeEndNodeId!.Value;

                if (!AreAdjacent(contour, start, end))
                    yield return $"Hinge {hinge.Id} targets plate {hinge.ElementId} edge {start}->{end}, but those nodes are not adjacent in {where}.";
            }
        }

        private static bool AreAdjacent(List<int> contour, int a, int b)
        {
            for (int i = 0; i < contour.Count; i++)
            {
                int j = (i + 1) % contour.Count;
                if ((contour[i] == a && contour[j] == b) || (contour[i] == b && contour[j] == a))
                    return true;
            }

            return false;
        }

        // ----- Mesh -----

        private IEnumerable<string> ValidateMesh(ValidationContext ctx)
        {
            if (Mesh is null)
                yield break;

            foreach (var meshNode in Mesh.Nodes)
            {
                if (meshNode.SourceNodeId.HasValue && !ctx.NodeNumbers.Contains(meshNode.SourceNodeId.Value))
                    yield return $"Mesh node {meshNode.Id} references unknown source node {meshNode.SourceNodeId.Value}.";
            }

            foreach (var face in Mesh.Faces)
            {
                if (face.NodeIds.Count != 3 && face.NodeIds.Count != 4)
                    yield return $"Mesh face {face.Id} has {face.NodeIds.Count} nodes; 3 or 4 are required.";

                foreach (int nodeId in face.NodeIds)
                    if (!ctx.MeshNodeIds.Contains(nodeId))
                        yield return $"Mesh face {face.Id} references unknown mesh node {nodeId}.";

                if (face.SurfacePropertyId.HasValue && !ctx.SurfacePropertyIds.Contains(face.SurfacePropertyId.Value))
                    yield return $"Mesh face {face.Id} references unknown surface property {face.SurfacePropertyId.Value}.";

                if (face.MaterialId.HasValue && !ctx.MaterialIds.Contains(face.MaterialId.Value))
                    yield return $"Mesh face {face.Id} references unknown material {face.MaterialId.Value}.";

                if (!ctx.PlatesById.TryGetValue(face.PlateId, out Plate? plate))
                {
                    yield return $"Mesh face {face.Id} references unknown plate {face.PlateId}.";
                    continue;
                }

                PlateRegionKind kind = plate.Kind;

                if (face.RegionId.HasValue)
                {
                    PlateRegion? region = plate.Regions.Find(r => r.Id == face.RegionId.Value);
                    if (region is null)
                    {
                        yield return $"Mesh face {face.Id} references region {face.RegionId.Value}, which does not exist on plate {face.PlateId}.";
                        continue;
                    }

                    kind = region.Kind;
                }

                if (kind == PlateRegionKind.Opening)
                    yield return $"Mesh face {face.Id} belongs to an opening on plate {face.PlateId}; openings generate no elements.";
            }
        }

        // ----- Geometry checks -----

        /// <summary>
        /// Every contour must be planar to within a tolerance scaled to its own
        /// size. Contours whose nodes cannot be located, and degenerate (collinear
        /// or zero-area) contours, are skipped rather than reported here.
        /// </summary>
        private IEnumerable<string> ValidateContourPlanarity(ValidationContext ctx)
        {
            foreach (var plate in Plates)
            {
                foreach (var m in CheckPlanarity(ctx, $"Plate {plate.Id} outer contour", plate.NodeIds))
                    yield return m;

                foreach (var region in plate.Regions)
                    foreach (var m in CheckPlanarity(ctx, $"Plate {plate.Id} region {region.Id} contour", region.NodeIds))
                        yield return m;
            }
        }

        private static IEnumerable<string> CheckPlanarity(ValidationContext ctx, string owner, List<int> nodeIds)
        {
            // Three points are always coplanar.
            int count = nodeIds.Count;
            if (count < 4)
                yield break;

            var xs = new double[count];
            var ys = new double[count];
            var zs = new double[count];

            for (int i = 0; i < count; i++)
            {
                if (!ctx.TryGetPoint(nodeIds[i], out xs[i], out ys[i], out zs[i]))
                    yield break; // unresolvable node or level — already reported
            }

            if (TryGetPlanarityDeviation(xs, ys, zs, out double deviation, out double tolerance) && deviation > tolerance)
                yield return $"{owner} is not planar (max out-of-plane deviation {deviation:G3}, tolerance {tolerance:G3}).";
        }

        /// <summary>
        /// Fits a plane through the contour using Newell's method, which gives a
        /// correct normal for non-convex polygons, and returns the largest
        /// out-of-plane distance together with a size-scaled tolerance.
        /// Returns false when the contour is degenerate, in which case planarity is
        /// not meaningful.
        /// </summary>
        private static bool TryGetPlanarityDeviation(
            double[] xs, double[] ys, double[] zs, out double deviation, out double tolerance)
        {
            deviation = 0.0;
            tolerance = 0.0;

            int count = xs.Length;
            double nx = 0.0, ny = 0.0, nz = 0.0;

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                nx += (ys[i] - ys[j]) * (zs[i] + zs[j]);
                ny += (zs[i] - zs[j]) * (xs[i] + xs[j]);
                nz += (xs[i] - xs[j]) * (ys[i] + ys[j]);
            }

            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length < 1e-12)
                return false; // collinear or zero area

            nx /= length;
            ny /= length;
            nz /= length;

            double cx = 0.0, cy = 0.0, cz = 0.0;
            for (int i = 0; i < count; i++)
            {
                cx += xs[i];
                cy += ys[i];
                cz += zs[i];
            }

            cx /= count;
            cy /= count;
            cz /= count;

            for (int i = 0; i < count; i++)
            {
                double distance = Math.Abs((xs[i] - cx) * nx + (ys[i] - cy) * ny + (zs[i] - cz) * nz);
                if (distance > deviation)
                    deviation = distance;
            }

            double dx = xs.Max() - xs.Min();
            double dy = ys.Max() - ys.Min();
            double dz = zs.Max() - zs.Min();
            double diagonal = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            tolerance = Math.Max(1e-6 * diagonal, 1e-9);
            return true;
        }

        /// <summary>
        /// Two regions of one plate with the same kind and the same priority are
        /// resolved by list order, which is deterministic but rarely intended.
        /// Overlap is tested on axis-aligned bounding boxes, so this over-reports
        /// for interlocking shapes whose boxes overlap but whose areas do not.
        /// It never under-reports.
        /// </summary>
        private IEnumerable<string> ValidateRegionPriorities(ValidationContext ctx)
        {
            foreach (var plate in Plates)
            {
                for (int i = 0; i < plate.Regions.Count; i++)
                {
                    PlateRegion a = plate.Regions[i];
                    if (!ctx.TryGetBounds(a.NodeIds, out Bounds? boundsA))
                        continue;

                    for (int j = i + 1; j < plate.Regions.Count; j++)
                    {
                        PlateRegion b = plate.Regions[j];
                        if (a.Priority != b.Priority || a.Kind != b.Kind)
                            continue;

                        if (!ctx.TryGetBounds(b.NodeIds, out Bounds? boundsB))
                            continue;

                        if (!boundsA!.Overlaps(boundsB!))
                            continue;

                        yield return $"Plate {plate.Id} regions {a.Id} and {b.Id} have the same kind ({a.Kind}) " +
                                     $"and priority ({a.Priority}) and overlapping extents; the outcome depends on list order.";
                    }
                }
            }
        }

        // ----- Helpers -----

        /// <summary>
        /// An axis-aligned bounding box in absolute coordinates.
        /// </summary>
        private sealed class Bounds
        {
            private const double Tolerance = 1e-9;

            public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;

            public bool Overlaps(Bounds other)
            {
                return AxisOverlaps(MinX, MaxX, other.MinX, other.MaxX)
                    && AxisOverlaps(MinY, MaxY, other.MinY, other.MaxY)
                    && AxisOverlaps(MinZ, MaxZ, other.MinZ, other.MaxZ);
            }

            private static bool AxisOverlaps(double minA, double maxA, double minB, double maxB)
            {
                double low = Math.Max(minA, minB);
                double high = Math.Min(maxA, maxB);

                if (high < low - Tolerance)
                    return false;

                // A flat contour has zero extent along one axis and can only ever
                // touch there; require a real overlap on the other two.
                bool degenerate = (maxA - minA) <= Tolerance || (maxB - minB) <= Tolerance;
                return degenerate || (high - low) > Tolerance;
            }
        }

        /// <summary>
        /// Lookup tables built once per <see cref="Validate"/> call. Built
        /// defensively so that duplicate ids (reported separately) cannot throw.
        /// </summary>
        private sealed class ValidationContext
        {
            public readonly HashSet<int> LevelNumbers;
            public readonly HashSet<int> NodeNumbers;
            public readonly HashSet<int> SectionIds;
            public readonly HashSet<int> SurfacePropertyIds;
            public readonly HashSet<int> MaterialIds;
            public readonly HashSet<int> LoadCaseNumbers;
            public readonly HashSet<int> BarIds;
            public readonly HashSet<int> ElementIds;
            public readonly HashSet<int> MeshNodeIds;
            public readonly Dictionary<int, Plate> PlatesById;
            public readonly List<int> AllElementIdsInOrder;

            private readonly Dictionary<int, Node> _nodesByNumber;
            private readonly Dictionary<int, double> _elevationsByLevel;

            public ValidationContext(FemexModel model)
            {
                LevelNumbers = new HashSet<int>(model.Levels.Select(l => l.LevelNumber));
                NodeNumbers = new HashSet<int>(model.Nodes.Select(n => n.NodeNumber));
                SectionIds = new HashSet<int>(model.Sections.Select(s => s.Id));
                SurfacePropertyIds = new HashSet<int>(model.SurfaceProperties.Select(s => s.Id));
                MaterialIds = new HashSet<int>(model.Materials.Select(m => m.Id));
                LoadCaseNumbers = new HashSet<int>(model.LoadCases.Select(c => c.Number));
                BarIds = new HashSet<int>(model.Bars.Select(b => b.Id));

                AllElementIdsInOrder = new List<int>();
                AllElementIdsInOrder.AddRange(model.Bars.Select(b => b.Id));
                AllElementIdsInOrder.AddRange(model.Plates.Select(p => p.Id));

                MeshNodeIds = new HashSet<int>();
                if (model.Mesh is not null)
                {
                    AllElementIdsInOrder.AddRange(model.Mesh.Faces.Select(f => f.Id));
                    foreach (var meshNode in model.Mesh.Nodes)
                        MeshNodeIds.Add(meshNode.Id);
                }

                ElementIds = new HashSet<int>(AllElementIdsInOrder);

                PlatesById = new Dictionary<int, Plate>();
                foreach (var plate in model.Plates)
                    PlatesById[plate.Id] = plate;

                _nodesByNumber = new Dictionary<int, Node>();
                foreach (var node in model.Nodes)
                    _nodesByNumber[node.NodeNumber] = node;

                _elevationsByLevel = new Dictionary<int, double>();
                foreach (var level in model.Levels)
                    _elevationsByLevel[level.LevelNumber] = level.AbsoluteElevation;
            }

            /// <summary>
            /// Resolves a node to absolute coordinates. Returns false when the node
            /// or its level is unknown — both reported by other checks.
            /// </summary>
            public bool TryGetPoint(int nodeNumber, out double x, out double y, out double z)
            {
                x = y = z = 0.0;

                if (!_nodesByNumber.TryGetValue(nodeNumber, out Node? node))
                    return false;

                if (!_elevationsByLevel.TryGetValue(node.LevelNumber, out double elevation))
                    return false;

                x = node.X;
                y = node.Y;
                z = elevation + node.VerticalOffset;
                return true;
            }

            public bool TryGetBounds(List<int> nodeIds, out Bounds? bounds)
            {
                bounds = null;

                foreach (int nodeId in nodeIds)
                {
                    if (!TryGetPoint(nodeId, out double x, out double y, out double z))
                        return false;

                    if (bounds is null)
                    {
                        bounds = new Bounds { MinX = x, MaxX = x, MinY = y, MaxY = y, MinZ = z, MaxZ = z };
                        continue;
                    }

                    bounds.MinX = Math.Min(bounds.MinX, x);
                    bounds.MaxX = Math.Max(bounds.MaxX, x);
                    bounds.MinY = Math.Min(bounds.MinY, y);
                    bounds.MaxY = Math.Max(bounds.MaxY, y);
                    bounds.MinZ = Math.Min(bounds.MinZ, z);
                    bounds.MaxZ = Math.Max(bounds.MaxZ, z);
                }

                return bounds is not null;
            }
        }
    }
}
