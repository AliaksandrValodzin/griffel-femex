using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Grids;
using griffel_femex.Geometry.Sections;
using griffel_femex.Loads;
using griffel_femex.Materials;

namespace griffel_femex
{
    /// <summary>
    /// Referential and geometric integrity checks for <see cref="FemexModel"/>.
    /// Deferred and non-throwing: one human-readable message per problem, and an
    /// empty sequence means the model is consistent. Not required for serialization.
    ///
    /// Messages carry a <see cref="ValidationSeverity"/>. Errors mean the model is
    /// inconsistent; warnings mean it is legal FEMEX that is more often an
    /// oversight than a decision, and nothing the format forbids is ever only a
    /// warning.
    /// </summary>
    public partial class FemexModel
    {
        public IEnumerable<ValidationMessage> Validate()
        {
            var ctx = new ValidationContext(this);

            foreach (var message in ValidateDuplicateIds(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateUids()) yield return ValidationMessage.Error(message);

            // Model-wide, and every self-weight check below reads it.
            foreach (var message in ValidateGravity()) yield return ValidationMessage.Error(message);

            foreach (var message in ValidateGrids(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateNodes(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateSections()) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateBars(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidatePlates(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateLoads(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateLoadCombinations(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateBoundaryConditions(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateMesh(ctx)) yield return ValidationMessage.Error(message);

            // Not about any one entity: what the file as a whole says it is, what
            // reading it did to it, what of it this build could not read, and how
            // much of it a receiver can match.
            foreach (var message in ValidateSchemaVersion()) yield return ValidationMessage.Warning(message);
            foreach (var message in ReportMigrations()) yield return ValidationMessage.Warning(message);
            foreach (var message in ReportUnknownMembers()) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateUidCoverage()) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateNameKeys()) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateSectionCompleteness()) yield return ValidationMessage.Warning(message);

            // Geometric checks last: they are the only ones that need coordinates,
            // and the only ones that can be approximate.
            foreach (var message in ValidateContourPlanarity(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateRegionPriorities(ctx)) yield return ValidationMessage.Error(message);
            foreach (var message in ValidateCoincidentNodes(ctx)) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateGridGeometry(ctx)) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateLoadCombinationUsage(ctx)) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateProjectedLoads(ctx)) yield return ValidationMessage.Warning(message);
            foreach (var message in ValidateSelfWeight()) yield return ValidationMessage.Warning(message);
        }

        /// <summary>Only the messages of one severity — <c>Validate(Error)</c> for the blocking ones.</summary>
        public IEnumerable<ValidationMessage> Validate(ValidationSeverity severity)
        {
            return Validate().Where(m => m.Severity == severity);
        }

        // ----- Ids -----

        private IEnumerable<string> ValidateDuplicateIds(ValidationContext ctx)
        {
            foreach (var m in ReportDuplicates(Grids.Select(g => g.Id), "grid id")) yield return m;
            foreach (var m in ReportDuplicates(Levels.Select(l => l.LevelNumber), "level number")) yield return m;
            foreach (var m in ReportDuplicates(Nodes.Select(n => n.NodeNumber), "node number")) yield return m;
            foreach (var m in ReportDuplicates(Sections.Select(s => s.Id), "section id")) yield return m;
            foreach (var m in ReportDuplicates(SurfaceProperties.Select(s => s.Id), "surface property id")) yield return m;
            foreach (var m in ReportDuplicates(Materials.Select(m2 => m2.Id), "material id")) yield return m;
            foreach (var m in ReportDuplicates(LoadCases.Select(c => c.Number), "load case number")) yield return m;
            foreach (var m in ReportDuplicates(Loads.Select(l => l.Id), "load id")) yield return m;
            foreach (var m in ReportDuplicates(LoadCombinations.Select(c => c.Number), "load combination number")) yield return m;
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

        // ----- Round-trip identity -----

        /// <summary>
        /// The two ways a uid can fail to be one. Both are errors rather than
        /// warnings because a uid that names two objects, or that is the "not set"
        /// value written out, makes the receiving program merge the wrong pair —
        /// which is worse than the duplication the field exists to prevent.
        ///
        /// Uniqueness is <b>model-wide</b>, not per collection: that is what a GUID
        /// means, and a receiver merging by uid does not care which list an object
        /// came from. The integer id spaces stay exactly as they were.
        /// </summary>
        private IEnumerable<string> ValidateUids()
        {
            var owners = new Dictionary<Guid, string>();

            foreach (var (entity, owner) in EnumerateIdentified())
            {
                if (entity.Uid is not Guid uid)
                    continue;

                if (uid == Guid.Empty)
                {
                    yield return $"{owner} carries the nil uid {Guid.Empty}, which is the value meaning " +
                                 "\"not set\" rather than an identity. Omit the uid instead.";
                    continue;
                }

                if (owners.TryGetValue(uid, out string? first))
                {
                    yield return $"Uid {uid} names both {first} and {owner}; a uid names one object.";
                    continue;
                }

                owners[uid] = owner;
            }
        }

        /// <summary>
        /// A file where only some objects carry a uid, which is the one coverage
        /// state that is neither of the two normal ones — nothing carries one, so
        /// the file simply has no round-trip identity, or everything does. One
        /// message model-wide rather than one per object: the fact is about the
        /// file, and a partly-stamped model of a thousand objects would otherwise
        /// bury every other message.
        /// </summary>
        private IEnumerable<string> ValidateUidCoverage()
        {
            int total = 0;
            int carrying = 0;

            foreach (var (entity, _) in EnumerateIdentified())
            {
                total++;
                if (entity.Uid.HasValue)
                    carrying++;
            }

            if (carrying == 0 || carrying == total)
                yield break;

            yield return $"{carrying} of {total} authored objects carry a uid; a receiving program merges " +
                         "those and duplicates the rest.";
        }

        /// <summary>
        /// The four entities Robot and ETABS key by <i>name</i> rather than by id —
        /// a section, a surface property, a material and a load case. A name they
        /// cannot tell apart and a name they have to invent are both collisions
        /// waiting on export, exactly as they are for a load combination, whose two
        /// messages this reuses the shape of.
        ///
        /// Warnings and not errors, deliberately: the interop review wanted these
        /// names required, and this is the half-step that keeps every existing file
        /// valid at error level while still telling an author what an exporter is
        /// about to have to invent.
        /// </summary>
        private IEnumerable<string> ValidateNameKeys()
        {
            foreach (var m in ReportNameKeys(
                         Sections.Select(s => ($"Section {s.Id}", s.Name)),
                         "section", "sections", "name", "named"))
                yield return m;

            foreach (var m in ReportNameKeys(
                         SurfaceProperties.Select(s => ($"Surface property {s.Id}", s.Name)),
                         "surface property", "surface properties", "name", "named"))
                yield return m;

            foreach (var m in ReportNameKeys(
                         Materials.Select(m2 => ($"Material {m2.Id}", m2.Name)),
                         "material", "materials", "name", "named"))
                yield return m;

            foreach (var m in ReportNameKeys(
                         LoadCases.Select(c => ($"Load case {c.Number}", c.Label)),
                         "load case", "load cases", "label", "labelled"))
                yield return m;
        }

        /// <summary>
        /// One entity kind's names, checked the way
        /// <see cref="ValidateLoadCombinationUsage"/> checks combination labels: one
        /// message per object with no name, and one per duplicated name with the
        /// <i>name</i> as the subject, so three colliding entries produce one
        /// message rather than three.
        /// </summary>
        private static IEnumerable<string> ReportNameKeys(
            IEnumerable<(string Owner, string? Name)> entities,
            string singular, string plural, string field, string verb)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (owner, name) in entities)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    yield return $"{owner} has no {field}; a program that keys {plural} by name will " +
                                 "invent one.";
                }
                else if (!seen.Add(name) && reported.Add(name))
                {
                    yield return $"More than one {singular} is {verb} \"{name}\". A program that keys " +
                                 $"{plural} by name cannot tell them apart.";
                }
            }
        }

        // ----- Grids -----

        /// <summary>
        /// Grids are annotation, so nothing here can make a model unsolvable —
        /// but a grid whose lines cannot be told apart cannot locate anything,
        /// which is the whole of what a grid is for. A blank or repeated label,
        /// a line with no direction and a back-to-front extent are all errors for
        /// that reason.
        /// </summary>
        private IEnumerable<string> ValidateGrids(ValidationContext ctx)
        {
            foreach (var m in ValidateGridReferences(ctx, DefaultGridIds, "Model default grid list"))
                yield return m;

            foreach (var level in Levels)
            {
                if (level.GridIds is null)
                    continue;

                foreach (var m in ValidateGridReferences(ctx, level.GridIds, $"Level {level.LevelNumber}"))
                    yield return m;
            }

            // A repeated grid id is one grid as far as its contents go, and is
            // already reported as an error in its own right.
            var seenGrids = new HashSet<int>();
            double tolerance = GetCoincidenceTolerance();

            foreach (var grid in Grids)
            {
                if (!seenGrids.Add(grid.Id))
                    continue;

                var seenLabels = new HashSet<string>(StringComparer.Ordinal);
                var reportedLabels = new HashSet<string>(StringComparer.Ordinal);

                foreach (var line in grid.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.Label))
                    {
                        yield return $"Grid {grid.Id} has a line with no label.";
                    }
                    else if (!seenLabels.Add(line.Label) && reportedLabels.Add(line.Label))
                    {
                        yield return $"Grid {grid.Id} has more than one line labelled \"{line.Label}\".";
                    }

                    if (line is FreeGridline free)
                    {
                        double ex = free.X2 - free.X1;
                        double ey = free.Y2 - free.Y1;
                        if (Math.Sqrt(ex * ex + ey * ey) <= tolerance)
                        {
                            yield return $"Grid {grid.Id} line \"{line.Label}\" has coincident end points " +
                                         "and defines no direction.";
                        }
                    }
                }

                if (grid.Extent is null)
                    continue;

                if (grid.Extent.MinX >= grid.Extent.MaxX)
                    yield return $"Grid {grid.Id} has an extent whose minX is not less than its maxX.";

                if (grid.Extent.MinY >= grid.Extent.MaxY)
                    yield return $"Grid {grid.Id} has an extent whose minY is not less than its maxY.";
            }
        }

        /// <summary>
        /// One list of grid references — the model's default or a level's
        /// override — checked for ids that do not resolve and for repeats, which
        /// would silently make a grid count twice.
        /// </summary>
        private static IEnumerable<string> ValidateGridReferences(
            ValidationContext ctx, List<int> gridIds, string owner)
        {
            var seen = new HashSet<int>();
            var reported = new HashSet<int>();

            foreach (int id in gridIds)
            {
                if (!ctx.GridIds.Contains(id))
                    yield return $"{owner} references unknown grid {id}.";

                if (!seen.Add(id) && reported.Add(id))
                    yield return $"{owner} repeats grid {id}.";
            }
        }

        /// <summary>
        /// Two grid problems that are legal FEMEX but almost never meant: a line
        /// drawn twice in one grid, and one label reaching two grids on a level,
        /// which makes "grid B" ambiguous at exactly the moment someone is
        /// standing on site trying to use it.
        /// </summary>
        private IEnumerable<string> ValidateGridGeometry(ValidationContext ctx)
        {
            double tolerance = GetCoincidenceTolerance();
            var seenGrids = new HashSet<int>();

            foreach (var grid in Grids)
            {
                if (!seenGrids.Add(grid.Id))
                    continue;

                for (int i = 0; i < grid.Lines.Count; i++)
                {
                    if (!TryGetLocalRay(grid.Lines[i], tolerance,
                            out double ax, out double ay, out double adx, out double ady))
                        continue;

                    for (int j = i + 1; j < grid.Lines.Count; j++)
                    {
                        if (!TryGetLocalRay(grid.Lines[j], tolerance,
                                out double bx, out double by, out double bdx, out double bdy))
                            continue;

                        // Same infinite line: parallel, and one's point lies on the
                        // other. Both directions are unit length, so both cross
                        // products are the quantities they look like — a sine and a
                        // perpendicular distance.
                        if (Math.Abs(adx * bdy - ady * bdx) >= ParallelDirectionTolerance)
                            continue;

                        if (Math.Abs((bx - ax) * ady - (by - ay) * adx) > tolerance)
                            continue;

                        yield return $"Grid {grid.Id} lines \"{grid.Lines[i].Label}\" and " +
                                     $"\"{grid.Lines[j].Label}\" are the same line.";
                    }
                }
            }

            foreach (var level in Levels)
            {
                var labelOwners = new Dictionary<string, int>(StringComparer.Ordinal);
                var reported = new HashSet<string>(StringComparer.Ordinal);
                var usedGrids = new HashSet<int>();

                foreach (var grid in GetGridsForLevel(level.LevelNumber))
                {
                    if (!usedGrids.Add(grid.Id))
                        continue;

                    foreach (string label in grid.Lines.Select(l => l.Label).Distinct(StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(label))
                            continue;

                        if (labelOwners.TryGetValue(label, out int other))
                        {
                            if (reported.Add(label))
                            {
                                yield return $"Level {level.LevelNumber} uses grids {other} and {grid.Id}, " +
                                             $"which both have a line labelled \"{label}\". A location given " +
                                             "by label alone is ambiguous.";
                            }

                            continue;
                        }

                        labelOwners[label] = grid.Id;
                    }
                }
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

                var (plateKind, plateSurface, plateMaterial) = GetEffectiveProperties(plate, null);

                foreach (var m in ValidateSurfaceAndMaterial(
                             ctx, owner, plateKind, plate.SurfacePropertyId, plate.MaterialId,
                             plateSurface, plateMaterial))
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

                    // Null on a region means "inherit from the plate", which
                    // GetEffectiveProperties states once for validation and the
                    // self-weight helpers alike.
                    var (regionKind, regionSurface, regionMaterial) = GetEffectiveProperties(plate, region);

                    foreach (var m in ValidateSurfaceAndMaterial(
                                 ctx, regionOwner, regionKind,
                                 region.SurfacePropertyId, region.MaterialId,
                                 regionSurface, regionMaterial))
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

        // ----- Sections -----

        /// <summary>
        /// How far a stated area may sit from the one the section's own dimensions
        /// give before it is worth reporting. Root radii and fillets explain a few
        /// percent — a tabulated IPE300 area is 3.6% above the parametric one — and
        /// ten is past what any idealisation accounts for. A unit error is what it
        /// usually is.
        /// </summary>
        private const double SectionAreaAgreementTolerance = 0.10;

        /// <summary>
        /// The two ways a section cannot be built as written. Both are errors
        /// because in each case there is nothing for a receiver to fall back on: a
        /// section with neither geometry nor stiffness gives it nothing at all, and
        /// a non-positive stiffness gives it nothing it can solve with.
        ///
        /// A <see cref="GenericSection"/> named <c>"IPE300"</c> with no properties
        /// is caught by the first of these, which is why there is no separate rule
        /// about catalogue names: a receiver without that library gets nothing from
        /// it.
        /// </summary>
        private IEnumerable<string> ValidateSections()
        {
            foreach (var section in Sections)
            {
                if (section is GenericSection && section.Properties?.Area is null)
                {
                    yield return $"Section {section.Id} is generic and states no area, so it has no " +
                                 "geometry and no stiffness; nothing can be built from it.";
                }

                if (section.Properties is null)
                    continue;

                foreach (var (what, value) in EnumerateStatedProperties(section.Properties))
                {
                    // Zero is rejected with the negatives: a stated property is a
                    // claim about stiffness, and zero is not a claim a solver can
                    // build with. Not in tension with a zero-width Rectangle staying
                    // legal — that field has been legal FEMEX since the first commit,
                    // whereas these are new and can be given a contract from the start.
                    if (value <= 0.0)
                    {
                        yield return $"Section {section.Id} states {what} of {value:G6}, which is not a " +
                                     "positive quantity.";
                    }
                }
            }
        }

        /// <summary>
        /// Every property this block actually states, named as the file names it.
        /// </summary>
        private static IEnumerable<(string What, double Value)> EnumerateStatedProperties(SectionProperties properties)
        {
            if (properties.Area is double area) yield return ("an area", area);
            if (properties.ShearAreaY is double ay) yield return ("a shearAreaY", ay);
            if (properties.ShearAreaZ is double az) yield return ("a shearAreaZ", az);
            if (properties.Iy is double iy) yield return ("an iy", iy);
            if (properties.Iz is double iz) yield return ("an iz", iz);
            if (properties.J is double j) yield return ("a j", j);
            if (properties.Iw is double iw) yield return ("an iw", iw);
            if (properties.Wely is double wely) yield return ("a wely", wely);
            if (properties.Welz is double welz) yield return ("a welz", welz);
            if (properties.Wply is double wply) yield return ("a wply", wply);
            if (properties.Wplz is double wplz) yield return ("a wplz", wplz);
        }

        /// <summary>
        /// Sections that are legal FEMEX and that a receiver gets wrong. The two
        /// checks <b>partition the space</b>, and each is scoped so the other's case
        /// cannot trip it.
        ///
        /// The first is <i>geometry and stiffness disagree</i>, and it is scoped to
        /// sections that have dimensions. <see cref="GenericSection.CalculateArea"/>
        /// returns zero by design, so an unscoped version would read every
        /// correctly-authored generic section — a stated area against a computed
        /// zero — as a 100% disagreement and fire on the exact case the escape hatch
        /// exists to make legal.
        ///
        /// The second is <i>no geometry, and the stiffness is incomplete</i>, and it
        /// is scoped to generic sections for the mirror reason. A shaped section with
        /// no properties is fine: it hands the receiver its dimensions, and every
        /// program FEMEX targets can integrate them. It is only when there is no
        /// geometry <i>and</i> no stiffness that the number is unrecoverable.
        ///
        /// So no input trips both, and a generic section carrying an area, an iy and
        /// an iz trips neither.
        /// </summary>
        private IEnumerable<string> ValidateSectionCompleteness()
        {
            foreach (var section in Sections)
            {
                bool isGeneric = section is GenericSection;

                if (section.Properties?.Area is not double stated)
                    continue;

                if (!isGeneric)
                {
                    double computed = section.CalculateArea();

                    // A section whose dimensions give nothing has nothing to
                    // disagree with; a zero-width Rectangle is legal FEMEX.
                    if (computed > 0.0 &&
                        Math.Abs(stated - computed) / computed > SectionAreaAgreementTolerance)
                    {
                        yield return $"Section {section.Id} states an area of {stated:G3} and its " +
                                     $"dimensions give {computed:G3}; one of the two is wrong.";
                    }

                    continue;
                }

                // Naming the missing one, because either alone is enough to bend the
                // member wrongly about that axis.
                if (section.Properties.Iy is null)
                {
                    yield return $"Section {section.Id} is generic and states an area but no iy; every " +
                                 "bar using it will weigh correctly and bend wrongly.";
                }

                if (section.Properties.Iz is null)
                {
                    yield return $"Section {section.Id} is generic and states an area but no iz; every " +
                                 "bar using it will weigh correctly and bend wrongly.";
                }
            }
        }

        // ----- Loads -----

        private IEnumerable<string> ValidateLoads(ValidationContext ctx)
        {
            foreach (var load in Loads)
            {
                if (!ctx.LoadCaseNumbers.Contains(load.LoadCaseNumber))
                    yield return $"Load '{load.Label}' references unknown load case {load.LoadCaseNumber}.";

                if (load is DistributedLoad distributed)
                    foreach (var m in ValidateLoadOrientation(ctx, distributed))
                        yield return m;

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

        /// <summary>
        /// The three orientation fields, checked against each other and against the
        /// host they name. Everything here makes a load impossible to resolve or
        /// self-contradictory, so all of it is an error — the one thing that is
        /// merely suspect, a projection that comes out to nothing, needs
        /// coordinates and lives in <see cref="ValidateProjectedLoads"/>.
        /// </summary>
        private static IEnumerable<string> ValidateLoadOrientation(ValidationContext ctx, DistributedLoad load)
        {
            string owner = Describe(load);

            if (load is LinearLoad line)
            {
                if (line.BarId.HasValue)
                {
                    if (!ctx.ElementIds.Contains(line.BarId.Value))
                        yield return $"{owner} references unknown bar {line.BarId.Value}.";
                    else if (!ctx.BarIds.Contains(line.BarId.Value))
                        yield return $"{owner} names element {line.BarId.Value} as its bar, but that element is not a bar.";
                }
                else if (load.CoordinateSystem == LoadCoordinateSystem.Local)
                {
                    yield return $"{owner} has a local direction but no barId; there is nothing to resolve it against.";
                }
            }

            if (load.Direction == LoadDirection.Vector)
            {
                if (load.Dx is null || load.Dy is null || load.Dz is null)
                    yield return $"{owner} has direction Vector but does not set all of dx, dy and dz.";
                else if (load.Dx == 0.0 && load.Dy == 0.0 && load.Dz == 0.0)
                    yield return $"{owner} has direction Vector with dx, dy and dz all zero, which is no direction at all.";
            }
            else if (load.Dx.HasValue || load.Dy.HasValue || load.Dz.HasValue)
            {
                // Not a harmless extra: it says two different things about which way
                // the load acts, and a reader has no rule for choosing between them.
                yield return $"{owner} sets dx/dy/dz but its direction is {load.Direction}; " +
                             "they are only read for direction Vector.";
            }

            if (load.Projected && load.CoordinateSystem == LoadCoordinateSystem.Local)
            {
                yield return $"{owner} is projected and in local coordinates. None of the programs FEMEX " +
                             "targets has a projected local variant, and the concept is not meaningful: a " +
                             "local direction is already defined relative to the surface being projected.";
            }
        }

        /// <summary>"Area load 'A1'", in the wording the load messages already use.</summary>
        private static string Describe(Load load)
        {
            string kind = load switch
            {
                PointLoad => "Point load",
                LinearLoad => "Linear load",
                AreaLoad => "Area load",
                TemperatureLoad => "Temperature load",
                _ => "Load",
            };

            return $"{kind} '{load.Label}'";
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

        /// <summary>
        /// A projected load whose projected extent is zero: the direction runs
        /// along the loaded line, or lies in the loaded surface's plane. Legal
        /// FEMEX — nothing about the model is inconsistent — but the load applies
        /// no force at all, which is almost never what was meant.
        ///
        /// Skipped whenever the direction or the geometry does not resolve; those
        /// are reported in their own words elsewhere, and guessing here would only
        /// duplicate them.
        /// </summary>
        private IEnumerable<string> ValidateProjectedLoads(ValidationContext ctx)
        {
            double tolerance = GetCoincidenceTolerance();

            foreach (var load in Loads)
            {
                // A projected load in local coordinates is already an error, and
                // saying so twice in two different ways would only muddy it.
                if (load is not DistributedLoad
                    { Projected: true, CoordinateSystem: LoadCoordinateSystem.Global } distributed)
                    continue;

                if (!TryGetLoadDirection(load, out Vector3d direction))
                    continue;

                switch (distributed)
                {
                    case LinearLoad line:
                        if (!ctx.TryGetPoint(line.StartNode, out double sx, out double sy, out double sz) ||
                            !ctx.TryGetPoint(line.EndNode, out double ex, out double ey, out double ez))
                            continue;

                        // The direction is a unit vector, so the cross product's
                        // length is the projected length itself.
                        var extent = new Vector3d(ex - sx, ey - sy, ez - sz);
                        if (extent.Length > 0.0 && extent.Cross(direction).Length <= tolerance)
                        {
                            yield return $"{Describe(load)} is projected but its direction runs along the " +
                                         "loaded line, so the projected length is zero.";
                        }

                        break;

                    case AreaLoad area:
                        // Likewise a cosine, both vectors being unit length.
                        if (TryGetHostAxes(area, out _, out _, out Vector3d normal) &&
                            Math.Abs(normal.Dot(direction)) <= ParallelDirectionTolerance)
                        {
                            yield return $"{Describe(load)} is projected but its direction lies in the " +
                                         "loaded surface's plane, so the projected area is zero.";
                        }

                        break;
                }
            }
        }

        // ----- Schema version -----

        /// <summary>
        /// What the file says it is. Every case here is a warning because the model
        /// is still perfectly readable — it is only the <i>meaning</i> of what was
        /// read that is in doubt, and only the author can settle it.
        ///
        /// The cases are the answers <see cref="ReadableSchemaVersions"/> allows: no
        /// version at all, the current one, any of the older versions that were
        /// recognised and migrated, and one this build has never heard of.
        ///
        /// Each branch says only what is true of <i>that</i> version. What the
        /// migrations actually did to this file is
        /// <see cref="ReportMigrations"/>'s to say, once, rather than repeated into
        /// every branch it applies to.
        /// </summary>
        private IEnumerable<string> ValidateSchemaVersion()
        {
            if (SchemaVersion is null)
            {
                yield return "The model has no schemaVersion, so it was written before load directions " +
                             "existed: its distributed loads are read as acting along global +Z, and every " +
                             "gravity load in it therefore has the wrong sign.";
            }
            else if (string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
            {
                // Current: nothing to say.
            }
            else if (string.Equals(SchemaVersion, "1.1", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.1\", written before self-weight existed, " +
                             "so no load case in it carries any, and each material's unit weight has been " +
                             "read as a density through the model's gravity. Re-saving it writes the " +
                             "current format.";
            }
            else if (string.Equals(SchemaVersion, "1.2", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.2\", written before round-trip identity " +
                             "existed, so nothing in it carries a uid and a program re-importing it cannot " +
                             "tell which objects are the ones it exported. Re-saving it writes the current " +
                             "format.";
            }
            else if (string.Equals(SchemaVersion, "1.3", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.3\", written before file metadata " +
                             "existed, so it does not say what produced it or when. Re-saving it writes " +
                             "the current format.";
            }
            else if (string.Equals(SchemaVersion, "1.4", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.4\", written before a section could " +
                             "state its own stiffness, so every section in it is only its dimensions and " +
                             "a shape this build has no class for could not have been written at all. " +
                             "Re-saving it writes the current format.";
            }
            else
            {
                yield return $"The model declares schemaVersion \"{SchemaVersion}\", which this build does " +
                             $"not recognise; it is read as {CurrentSchemaVersion}.";
            }
        }

        // ----- Self weight -----

        /// <summary>
        /// The gravity block, which is the one place in FEMEX where numbers are
        /// checked at all. The exception is deliberate rather than an oversight of
        /// the "validate no numeric field" convention: the block is <i>entirely</i>
        /// numeric, so refusing to check its numbers would leave it with no checks,
        /// and neither failure produces a visibly wrong answer — each one silently
        /// deletes the structure's own weight.
        ///
        /// The direction is otherwise unpoliced: an unnormalized dx/dy/dz is
        /// normalized silently, exactly as a distributed load's vector direction
        /// already is.
        /// </summary>
        private IEnumerable<string> ValidateGravity()
        {
            if (Gravity.Dx == 0.0 && Gravity.Dy == 0.0 && Gravity.Dz == 0.0)
                yield return "Gravity has dx, dy and dz all zero, which is no direction at all.";

            if (Gravity.Acceleration <= 0.0)
            {
                yield return $"Gravity has a non-positive acceleration ({Gravity.Acceleration:G6}). Which way " +
                             "gravity acts is dx/dy/dz's job; the acceleration is only how strong it is.";
            }
        }

        /// <summary>
        /// Self-weight that is legal FEMEX but that a receiving program will probably
        /// get wrong. The first two messages are the two halves of the failure this
        /// field exists to remove: a weight applied twice, and a weight applied
        /// nowhere at all.
        /// </summary>
        /// <summary>
        /// Every version whose files can carry self-weight — 1.2, when it arrived,
        /// onwards.
        ///
        /// A matched list rather than "any version from 1.2 on", for the reason
        /// <see cref="ReadableSchemaVersions"/> gives in as many words: FEMEX has no
        /// ordering policy over versions, and an inverted test would also quietly
        /// change behaviour for unrecognised ones — <c>"2.0"</c> fails this gate
        /// today and draws no self-weight warning, and would pass an inverted one.
        /// The cost is a line to touch at every bump, paid knowingly.
        /// </summary>
        private static readonly string[] SelfWeightVersions = { "1.2", "1.3", "1.4", CurrentSchemaVersion };

        private IEnumerable<string> ValidateSelfWeight()
        {
            var selfWeightCases = new List<LoadCase>();
            foreach (var loadCase in LoadCases)
                if (loadCase.SelfWeightFactor != 0.0)
                    selfWeightCases.Add(loadCase);

            if (selfWeightCases.Count > 1)
            {
                string numbers = FormatNumberList(selfWeightCases.Select(c => c.Number));
                yield return $"Load cases {numbers} both carry self-weight; the structure's own weight is " +
                             "applied once in each of them, and any combination naming more than one counts " +
                             "it twice.";
            }

            foreach (var loadCase in selfWeightCases)
            {
                if (loadCase.Nature != LoadNature.Dead)
                {
                    yield return $"Load case {loadCase.Number} carries self-weight but its nature is " +
                                 $"{loadCase.Nature}; the structure's own weight is a dead action.";
                }
            }

            // Which materials actually clothe something, so that a spare material in
            // the library is never the subject of either message below.
            var usedMaterialIds = new HashSet<int>();
            foreach (var bar in Bars)
                usedMaterialIds.Add(bar.MaterialId);

            foreach (var plate in Plates)
            {
                var (kind, _, materialId) = GetEffectiveProperties(plate, null);
                if (kind != PlateRegionKind.Opening && materialId.HasValue)
                    usedMaterialIds.Add(materialId.Value);

                foreach (var region in plate.Regions)
                {
                    var (regionKind, _, regionMaterialId) = GetEffectiveProperties(plate, region);
                    if (regionKind != PlateRegionKind.Opening && regionMaterialId.HasValue)
                        usedMaterialIds.Add(regionMaterialId.Value);
                }
            }

            if (selfWeightCases.Count == 0)
            {
                // Scoped so it cannot nag a model with nothing to weigh, and skipped
                // for a file whose version warning already says no case in it carries
                // any self-weight — the repo never reports one fact twice.
                bool hasSomethingToWeigh =
                    (Bars.Count > 0 || Plates.Count > 0) &&
                    Materials.Exists(m => m.Density != 0.0 && usedMaterialIds.Contains(m.Id));

                // 1.2 is the version self-weight arrived in, so every version from
                // it on is asked the question; a 1.1 or unversioned one is not, its
                // own version warning having already said no case in it carries any.
                bool versionHasSelfWeight =
                    SchemaVersion is not null &&
                    Array.IndexOf(SelfWeightVersions, SchemaVersion) >= 0;

                if (hasSomethingToWeigh && versionHasSelfWeight)
                {
                    yield return "No load case carries self-weight: every selfWeightFactor is zero, so the " +
                                 "structure's own weight is nowhere in this model and a receiving program " +
                                 "will not add it.";
                }
            }
            else
            {
                // Gated on self-weight being active, so a model that never uses
                // density is not nagged about it.
                foreach (var material in Materials)
                {
                    if (material.Density == 0.0 && usedMaterialIds.Contains(material.Id))
                    {
                        yield return $"Material {material.Id} has a density of zero, so every bar and plate " +
                                     "made of it weighs nothing in the self-weight case.";
                    }
                }
            }
        }

        /// <summary>
        /// What reading an older file did to it. A property of the read and not of
        /// the model: a model built in memory has nothing to report, and a migrated
        /// one re-emitted through <see cref="ToJson"/> is a current-format file
        /// carrying what the migration produced, so it must not report again.
        ///
        /// One report covering every migration, rather than one per version, so
        /// that each fact is stated once — the load numbering in particular is true
        /// of a 1.1 file and of a 1.2 file alike, and the version branches in
        /// <see cref="ValidateSchemaVersion"/> would otherwise both have to say it.
        /// </summary>
        private IEnumerable<string> ReportMigrations()
        {
            if (_migratedLoadIdCount is int count)
            {
                yield return $"This model was written before loads had ids; its {count} loads have been " +
                             $"numbered 1–{count} in list order. Re-saving the model writes those ids.";
            }

            if (_migratedUnitWeightMaterialIds is not null)
            {
                foreach (int id in _migratedUnitWeightMaterialIds)
                {
                    Material? material = Materials.Find(m => m.Id == id);
                    if (material is null)
                        continue;

                    yield return $"Material {id} was written as a unit weight and has been read as a density " +
                                 $"of {material.Density:G6} through the model's gravity " +
                                 $"({Gravity.Acceleration:G6}). Re-saving the model writes the density.";
                }
            }

            if (_bothDensitySpellingsMaterialIds is null)
                yield break;

            foreach (int id in _bothDensitySpellingsMaterialIds)
            {
                yield return $"Material {id} carries both a unitWeight and a density; the density is used " +
                             "and the unit weight ignored.";
            }
        }

        // ----- Load combinations -----

        /// <summary>
        /// A combination that cannot be evaluated: one with nothing in it, or one
        /// factoring a load case that is not in the model. Duplicate combination
        /// numbers are reported by <see cref="ValidateDuplicateIds"/>.
        /// </summary>
        private IEnumerable<string> ValidateLoadCombinations(ValidationContext ctx)
        {
            var seen = new HashSet<int>();

            foreach (var combination in LoadCombinations)
            {
                // A repeated combination number is one combination as far as its
                // contents go, and is already reported as an error in its own right.
                if (!seen.Add(combination.Number))
                    continue;

                if (combination.Terms.Count == 0)
                    yield return $"Load combination {combination.Number} has no terms.";

                foreach (var term in combination.Terms)
                {
                    if (!ctx.LoadCaseNumbers.Contains(term.LoadCaseNumber))
                        yield return $"Load combination {combination.Number} references unknown load case {term.LoadCaseNumber}.";
                }
            }
        }

        /// <summary>
        /// Combinations that are legal FEMEX but that a receiving program will
        /// probably get wrong. A load case named twice is legal and its factors
        /// add — ETABS behaves the same way — but it is far more often a mistake
        /// than a choice, so unlike a repeated node it is a warning rather than an
        /// error.
        ///
        /// The two label checks are the format's answer to programs that key
        /// combinations by name rather than by number, which Robot, ETABS and SAF
        /// all do: a name they cannot tell apart, and a name they have to invent,
        /// are both collisions waiting on export even though FEMEX itself
        /// references combinations by number throughout.
        /// </summary>
        private IEnumerable<string> ValidateLoadCombinationUsage(ValidationContext ctx)
        {
            var seen = new HashSet<int>();
            var seenLabels = new HashSet<string>(StringComparer.Ordinal);
            var reportedLabels = new HashSet<string>(StringComparer.Ordinal);

            foreach (var combination in LoadCombinations)
            {
                if (!seen.Add(combination.Number))
                    continue;

                var seenCases = new HashSet<int>();
                var reportedCases = new HashSet<int>();

                foreach (var term in combination.Terms)
                {
                    if (!seenCases.Add(term.LoadCaseNumber) && reportedCases.Add(term.LoadCaseNumber))
                    {
                        yield return $"Load combination {combination.Number} includes load case " +
                                     $"{term.LoadCaseNumber} more than once; the factors add.";
                    }
                }

                if (string.IsNullOrWhiteSpace(combination.Label))
                {
                    yield return $"Load combination {combination.Number} has no label; a program that " +
                                 "keys combinations by name will invent one.";
                }
                else if (!seenLabels.Add(combination.Label) && reportedLabels.Add(combination.Label))
                {
                    // The label is the subject, so no combination number appears:
                    // three combinations sharing one label produce one message.
                    yield return $"More than one load combination is labelled \"{combination.Label}\". " +
                                 "A program that keys combinations by name cannot tell them apart.";
                }
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

            var points = new Vector3d[count];

            for (int i = 0; i < count; i++)
            {
                if (!ctx.TryGetPoint(nodeIds[i], out double x, out double y, out double z))
                    yield break; // unresolvable node or level — already reported

                points[i] = new Vector3d(x, y, z);
            }

            if (TryGetPlanarityDeviation(points, out double deviation, out double tolerance) && deviation > tolerance)
                yield return $"{owner} is not planar (max out-of-plane deviation {deviation:G3}, tolerance {tolerance:G3}).";
        }

        /// <summary>
        /// Fits a plane through the contour and returns the largest out-of-plane
        /// distance together with a size-scaled tolerance. Returns false when the
        /// contour is degenerate, in which case planarity is not meaningful.
        ///
        /// The plane's normal is <see cref="TryGetNewellNormal"/> — the same one
        /// <see cref="TryGetPlateLocalAxes"/> calls local z, so the planarity check
        /// and the local-axis convention cannot drift apart.
        /// </summary>
        private static bool TryGetPlanarityDeviation(
            IReadOnlyList<Vector3d> points, out double deviation, out double tolerance)
        {
            deviation = 0.0;
            tolerance = 0.0;

            if (!TryGetNewellNormal(points, out Vector3d normal))
                return false;

            int count = points.Count;
            var centroid = Vector3d.Zero;
            foreach (Vector3d point in points)
                centroid += point;

            centroid *= 1.0 / count;

            foreach (Vector3d point in points)
            {
                double distance = Math.Abs((point - centroid).Dot(normal));
                if (distance > deviation)
                    deviation = distance;
            }

            double dx = points.Max(p => p.X) - points.Min(p => p.X);
            double dy = points.Max(p => p.Y) - points.Min(p => p.Y);
            double dz = points.Max(p => p.Z) - points.Min(p => p.Z);
            double diagonal = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            tolerance = Math.Max(RelativeGeometricTolerance * diagonal, MinimumGeometricTolerance);
            return true;
        }

        /// <summary>
        /// Nodes are the model's connectivity: two elements are joined where they
        /// name the same node number, and only there. The format deliberately allows
        /// several nodes at one location, because that is the only way to express a
        /// joint that is meant to be disconnected — a movement joint, a slip plane,
        /// two structures that merely touch. So this is a warning, not an error.
        ///
        /// It is worth warning about because the two cases look identical: an
        /// element list, a plot and a rendered model are all the same whether the
        /// duplicate was intended or was a node added where one already existed, and
        /// the second case silently produces a mechanism.
        /// </summary>
        private IEnumerable<string> ValidateCoincidentNodes(ValidationContext ctx)
        {
            var placed = new List<(int Number, double X, double Y, double Z)>();
            var seen = new HashSet<int>();

            foreach (var node in Nodes)
            {
                // A repeated node number is one node as far as location goes, and is
                // already reported as an error in its own right.
                if (!seen.Add(node.NodeNumber))
                    continue;

                if (ctx.TryGetPoint(node, out double x, out double y, out double z))
                    placed.Add((node.NodeNumber, x, y, z));
            }

            foreach (var group in FindCoincidentGroups(placed, GetCoincidenceTolerance()))
            {
                var (_, x, y, z) = placed[group[0]];
                string numbers = FormatNumberList(group.Select(i => placed[i].Number));

                yield return $"Nodes {numbers} are at the same location ({x:G6}, {y:G6}, {z:G6}). " +
                             "Elements only connect where they reference the same node number, " +
                             "so unless the joint is meant to be disconnected they should share one node.";
            }
        }

        /// <summary>
        /// Groups nodes that lie within <paramref name="tolerance"/> of one another,
        /// via a grid of tolerance-sized cells so only nearby nodes are compared.
        /// Groups are transitive, so a chain of nodes each within tolerance of the
        /// next is reported as one group even if its ends are further apart; that
        /// only happens for nodes that are all but coincident anyway. Returned
        /// smallest-node-number first, each group sorted, singletons dropped.
        /// </summary>
        private static List<List<int>> FindCoincidentGroups(
            List<(int Number, double X, double Y, double Z)> placed, double tolerance)
        {
            var groups = new List<List<int>>();
            if (placed.Count < 2)
                return groups;

            double cell = Math.Max(tolerance, MinimumGeometricTolerance);
            double toleranceSquared = tolerance * tolerance;

            var grid = new Dictionary<(long, long, long), List<int>>();
            var parent = new int[placed.Count];
            for (int i = 0; i < parent.Length; i++)
                parent[i] = i;

            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            for (int i = 0; i < placed.Count; i++)
            {
                var (_, x, y, z) = placed[i];
                long cx = (long)Math.Floor(x / cell);
                long cy = (long)Math.Floor(y / cell);
                long cz = (long)Math.Floor(z / cell);

                // Points within one cell of each other may still straddle a cell
                // boundary, so the 26 neighbours are searched as well.
                for (long ox = -1; ox <= 1; ox++)
                for (long oy = -1; oy <= 1; oy++)
                for (long oz = -1; oz <= 1; oz++)
                {
                    if (!grid.TryGetValue((cx + ox, cy + oy, cz + oz), out List<int>? bucket))
                        continue;

                    foreach (int j in bucket)
                    {
                        double ex = placed[j].X - x, ey = placed[j].Y - y, ez = placed[j].Z - z;
                        if (ex * ex + ey * ey + ez * ez > toleranceSquared)
                            continue;

                        int a = Find(i), b = Find(j);
                        if (a != b)
                            parent[Math.Max(a, b)] = Math.Min(a, b);
                    }
                }

                if (!grid.TryGetValue((cx, cy, cz), out List<int>? own))
                    grid[(cx, cy, cz)] = own = new List<int>();
                own.Add(i);
            }

            var byRoot = new Dictionary<int, List<int>>();
            for (int i = 0; i < placed.Count; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out List<int>? members))
                    byRoot[root] = members = new List<int>();
                members.Add(i);
            }

            foreach (var members in byRoot.Values)
            {
                if (members.Count < 2)
                    continue;

                members.Sort((a, b) => placed[a].Number.CompareTo(placed[b].Number));
                groups.Add(members);
            }

            groups.Sort((a, b) => placed[a[0]].Number.CompareTo(placed[b[0]].Number));
            return groups;
        }

        /// <summary>"11", "11 and 41", "11, 41 and 44" — node numbers or case numbers alike.</summary>
        private static string FormatNumberList(IEnumerable<int> numbers)
        {
            var list = numbers.ToList();
            if (list.Count <= 1)
                return string.Join(string.Empty, list);

            return string.Join(", ", list.Take(list.Count - 1)) + " and " + list[^1];
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
            public readonly HashSet<int> GridIds;
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
                GridIds = new HashSet<int>(model.Grids.Select(g => g.Id));
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

                return _nodesByNumber.TryGetValue(nodeNumber, out Node? node)
                    && TryGetPoint(node, out x, out y, out z);
            }

            /// <summary>
            /// The same for a node held directly, which is how the coincidence check
            /// reaches nodes that share a number with another.
            /// </summary>
            public bool TryGetPoint(Node node, out double x, out double y, out double z)
            {
                x = y = z = 0.0;

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
