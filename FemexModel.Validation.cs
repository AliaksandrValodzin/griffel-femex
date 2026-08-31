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
        /// <summary>
        /// Every finding, in a fixed order, each carrying the severity it deserves
        /// and the <see cref="ValidationCategory"/> its family belongs to.
        ///
        /// <b>The category is stated here, once per family, rather than inside the
        /// families.</b> This list is the only place in the library where every rule
        /// is visible at once, so it is the only place where "which half is this
        /// one in" can be read as an answer to §4 of <c>FEMEX_BusinessModel.md</c>
        /// rather than as thirty-five separate opinions. The two axes are
        /// independent on purpose: <see cref="ValidateRegionPriorities"/> is an
        /// Error and a <see cref="ValidationCategory.Judgement"/> finding, and a
        /// report that could only sort by severity would bury it among the dangling
        /// references.
        /// </summary>
        public IEnumerable<ValidationMessage> Validate()
        {
            var ctx = new ValidationContext(this);

            const ValidationCategory Referential = ValidationCategory.Referential;
            const ValidationCategory Judgement = ValidationCategory.Judgement;
            const ValidationCategory Provenance = ValidationCategory.Provenance;

            foreach (var message in ValidateDuplicateIds(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateUids()) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateParentUids()) yield return ValidationMessage.Error(message, Referential);

            // Model-wide, and every self-weight check below reads it. Judgement, not
            // referential: nothing about the file is inconsistent, and a model whose
            // gravity has no direction at all solves and is wrong.
            foreach (var message in ValidateGravity()) yield return ValidationMessage.Error(message, Judgement);

            foreach (var message in ValidateGrids(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateNodes(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateSections()) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateMaterials()) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateBars(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidatePlates(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateLoadGroups(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateLoads(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateLoadCombinations(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateBoundaryConditions(ctx)) yield return ValidationMessage.Error(message, Referential);
            foreach (var message in ValidateMesh(ctx)) yield return ValidationMessage.Error(message, Referential);

            // Not about any one entity: what the file as a whole says it is, what
            // reading it did to it, what of it this build could not read, and how
            // much of it a receiver can match.
            foreach (var message in ValidateSchemaVersion()) yield return ValidationMessage.Warning(message, Provenance);
            foreach (var message in ReportMigrations()) yield return ValidationMessage.Warning(message, Provenance);
            foreach (var message in ReportUnknownMembers()) yield return ValidationMessage.Warning(message, Provenance);
            foreach (var message in ValidateUidCoverage()) yield return ValidationMessage.Warning(message, Provenance);
            foreach (var message in ReportUnresolvedParents(ctx)) yield return ValidationMessage.Warning(message, Referential);
            foreach (var message in ValidateNameKeys()) yield return ValidationMessage.Warning(message, Provenance);
            foreach (var message in ValidateSectionCompleteness()) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateBarCompleteness(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateMaterialCompleteness(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateSupportCompleteness()) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateLoadGroupUsage(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateLoadDistributions()) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateThermalGradients(ctx)) yield return ValidationMessage.Warning(message, Judgement);

            // Geometric checks last: they are the only ones that need coordinates,
            // and the only ones that can be approximate. Every one of them is
            // judgement — they are the checks §4 quotes when it says what the
            // product is.
            foreach (var message in ValidateContourPlanarity(ctx)) yield return ValidationMessage.Error(message, Judgement);
            foreach (var message in ValidateRegionPriorities(ctx)) yield return ValidationMessage.Error(message, Judgement);
            foreach (var message in ValidateCoincidentNodes(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateGridGeometry(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateLoadCombinationUsage(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateProjectedLoads(ctx)) yield return ValidationMessage.Warning(message, Judgement);
            foreach (var message in ValidateSelfWeight()) yield return ValidationMessage.Warning(message, Judgement);
        }

        /// <summary>Only the messages of one severity — <c>Validate(Error)</c> for the blocking ones.</summary>
        public IEnumerable<ValidationMessage> Validate(ValidationSeverity severity)
        {
            return Validate().Where(m => m.Severity == severity);
        }

        /// <summary>
        /// Only the messages of one category — <c>Validate(Judgement)</c> for the
        /// half <c>FEMEX_BusinessModel.md</c> §4 calls the product.
        /// </summary>
        public IEnumerable<ValidationMessage> Validate(ValidationCategory category)
        {
            return Validate().Where(m => m.Category == category);
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
            foreach (var m in ReportDuplicates(LoadGroups.Select(g => g.Id), "load group id")) yield return m;
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

            foreach (var (entity, _, owner) in EnumerateIdentified())
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
        /// The two ways a <see cref="IIdentified.ParentUid"/> can fail to be
        /// provenance at all: the nil guid, which is the "not set" value written out
        /// rather than an identity, and an object naming itself, which is a claim
        /// that resolves to nothing a consumer can act on.
        ///
        /// Errors, for the reason <see cref="ValidateUids"/> gives of a uid: a
        /// pointer that says something false is worse than one that says nothing,
        /// and both of these are false rather than absent.
        ///
        /// <b>What is deliberately not here is type compatibility, cycles and
        /// depth.</b> The field is a provenance pointer and nothing in this library
        /// traverses it, so there is no traversal to protect; a chain of derivations
        /// is not this format's business and a rule about one would be inventing a
        /// design nothing has asked for. A parent that names no object in the model
        /// is not an error at all — see <see cref="ReportUnresolvedParents"/>.
        /// </summary>
        private IEnumerable<string> ValidateParentUids()
        {
            foreach (var (entity, _, owner) in EnumerateIdentified())
            {
                if (entity.ParentUid is not Guid parent)
                    continue;

                if (parent == Guid.Empty)
                {
                    yield return $"{owner} carries the nil uid {Guid.Empty} as its parentUid, which is " +
                                 "the value meaning \"not set\" rather than an identity. Omit it instead.";
                    continue;
                }

                if (entity.Uid is Guid own && own == parent)
                    yield return $"{owner} names itself as its own parentUid; an object is not derived from itself.";
            }
        }

        /// <summary>
        /// A <see cref="IIdentified.ParentUid"/> naming an object that is not in this
        /// model. Legal, and reported so that it is not mistaken for a broken
        /// reference — the field's whole purpose includes pointing at something that
        /// never was a FEMEX object. A circular arc chorded into eight bars has no
        /// arc in the file; the eight chords point at what they came from, which is
        /// exactly what lets a write back out re-emit the arc instead of the chords.
        ///
        /// One message per distinct parent rather than one per object, so eight
        /// chords of one arc produce one line rather than eight.
        /// </summary>
        private IEnumerable<string> ReportUnresolvedParents(ValidationContext ctx)
        {
            var reported = new HashSet<Guid>();

            foreach (var (entity, _, owner) in EnumerateIdentified())
            {
                if (entity.ParentUid is not Guid parent || parent == Guid.Empty)
                    continue;

                if (ctx.Uids.Contains(parent) || !reported.Add(parent))
                    continue;

                yield return $"{owner} names parentUid {parent}, which is no object in this model. That " +
                             "is legal — provenance may point at something that was never a FEMEX " +
                             "object — but nothing here can resolve it.";
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

            foreach (var (entity, _, _) in EnumerateIdentified())
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

            // SAF keys load groups by name too, and treats a duplicate within the
            // sheet as fatal where FEMEX reports it. Same half-step as the four
            // above: every existing file stays valid at error level, and an author
            // is told what an exporter is about to have to invent.
            foreach (var m in ReportNameKeys(
                         LoadGroups.Select(g => ($"Load group {g.Id}", g.Name)),
                         "load group", "load groups", "name", "named"))
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
                // name! rather than name: netstandard2.0's string.IsNullOrWhiteSpace
                // carries no [NotNullWhen(false)], so the netstandard leg cannot see
                // what the branch above has already established. The net8.0 leg can.
                else if (!seen.Add(name!) && reported.Add(name!))
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
                if (bar.EndSectionId is int endSectionId && !ctx.SectionIds.Contains(endSectionId))
                    yield return $"Bar {bar.Id} references unknown end section {endSectionId}.";
                if (!ctx.MaterialIds.Contains(bar.MaterialId))
                    yield return $"Bar {bar.Id} references unknown material {bar.MaterialId}.";
            }
        }

        /// <summary>
        /// Members that are legal FEMEX and that a receiver builds wrongly or cannot
        /// build at all. Four rules, and each of them is a claim that says nothing or
        /// contradicts itself rather than a number out of range.
        ///
        /// <b>A taper to the same section</b> says the member varies and then names
        /// the thing it varies to as the thing it started from. Null is how a
        /// prismatic member is stated, and a receiver reading a taper will build the
        /// machinery for one.
        ///
        /// <b>A taper across two different shapes</b> is not buildable at all: no
        /// program FEMEX targets can vary a rectangle linearly into a circle, and the
        /// one that tries will interpolate the numbers and produce a member with
        /// neither shape's properties. An error would be defensible; it is a warning
        /// because it is legal FEMEX and because a receiver that falls back on
        /// <c>SectionId</c> gets the prismatic member, which is the degrade-don't-lose
        /// behaviour the format asks of it.
        ///
        /// <b>An eccentricity block with eight nulls</b> is an empty claim, mirroring
        /// <see cref="ValidateSectionCompleteness"/>'s treatment of a section that
        /// states nothing.
        ///
        /// <b>A tension- or compression-only bar carrying a hinge that releases
        /// its axial DOF</b> is the one that changes an answer. Such a member carries
        /// axial force and nothing else; releasing <c>Ux</c> as well leaves it
        /// carrying nothing, so it is a member drawn into the model that resists
        /// none of it. The lookup this needs is element-id → hinge, because
        /// <see cref="Geometry.Bar"/> carries no hinge — hinges are a root list
        /// pointing back at their element.
        /// </summary>
        private IEnumerable<string> ValidateBarCompleteness(ValidationContext ctx)
        {
            foreach (var bar in Bars)
            {
                if (bar.EndSectionId is int endSectionId)
                {
                    if (endSectionId == bar.SectionId)
                    {
                        yield return $"Bar {bar.Id} tapers from section {bar.SectionId} to itself, which " +
                                     "says nothing; omit endSectionId to state a prismatic member.";
                    }
                    else
                    {
                        Section? start = Sections.Find(s => s.Id == bar.SectionId);
                        Section? end = Sections.Find(s => s.Id == endSectionId);

                        // An unknown id is reported by the bar's own reference check,
                        // and this rule has nothing to add to it.
                        if (start is not null && end is not null && start.GetType() != end.GetType())
                        {
                            yield return $"Bar {bar.Id} tapers from section {bar.SectionId} to section " +
                                         $"{endSectionId}, which are different shapes; nothing can build " +
                                         "a member that varies linearly between them.";
                        }
                    }
                }

                if (bar.Eccentricity is not null && bar.Eccentricity.IsEmpty())
                {
                    yield return $"Bar {bar.Id} carries an eccentricity block stating no offset at all; " +
                                 "omit the block instead.";
                }

                if (bar.Behaviour is not (BarBehaviour.TensionOnly or BarBehaviour.CompressionOnly))
                    continue;

                foreach (var hinge in ctx.HingesOn(bar.Id))
                {
                    if (!hinge.Ux.Released)
                        continue;

                    yield return $"Bar {bar.Id} is {bar.Behaviour} and hinge {hinge.Id} releases its ux; " +
                                 "the member carries axial force and nothing else, so releasing that too " +
                                 "leaves it carrying nothing.";
                }
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

                foreach (var m in ValidateDistributionMembers(ctx, owner, plate.Distribution))
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

                    foreach (var m in ValidateDistributionMembers(ctx, regionOwner, region.Distribution))
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

        /// <summary>
        /// The members a panel's load distribution names, checked as the references
        /// they are. The same wording template as every other reference check —
        /// "{Owner} references unknown {kind} {id}." — and the same two-step a load's
        /// host bar takes, since an id in the shared element space may resolve to a
        /// plate, which cannot receive a distributed panel load.
        /// </summary>
        private static IEnumerable<string> ValidateDistributionMembers(
            ValidationContext ctx, string owner, LoadDistribution? distribution)
        {
            if (distribution?.BarIds is null)
                yield break;

            foreach (int barId in distribution.BarIds)
            {
                foreach (var m in ValidateHostBar(ctx, $"{owner} load distribution", barId))
                    yield return m;
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
        /// Sections that are legal FEMEX and that a receiver gets wrong: a profile
        /// named with no library to look it up in, and the two ways geometry and
        /// stiffness fail to add up.
        ///
        /// Those two <b>partition the space</b>, and each is scoped so the other's
        /// case cannot trip it.
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

                // The failure SAF's form code exists to prevent, and the one FEMEX
                // answers with provenance rather than with a code.
                if (section.Catalogue is { Profile: not null } catalogue &&
                    !string.IsNullOrWhiteSpace(catalogue.Profile) &&
                    string.IsNullOrWhiteSpace(catalogue.Source))
                {
                    yield return $"Section {section.Id} names profile \"{catalogue.Profile}\" with no " +
                                 "source; the same designation names different profiles in different " +
                                 "libraries.";
                }

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

        // ----- Materials -----

        /// <summary>
        /// The material numbers that cannot be built with. Errors for the reason
        /// <see cref="ValidateSections"/> gives of a section's: a stated property is
        /// a claim, and a non-positive one is not a claim anything downstream can
        /// use. A zero G deletes every shear deformation in the model, a zero α
        /// deletes every thermal load, and a zero strength designs nothing.
        ///
        /// α is required strictly positive with the rest. Every family
        /// <see cref="MaterialType"/> names expands when it is heated, so a
        /// non-positive one is far more often a sign error than a statement about an
        /// exotic composite — and FEMEX has no orthotropic material for the exotic
        /// composite to be stated as anyway.
        ///
        /// Deliberately scoped to what 1.7 added. E, ν and ρ are unpoliced here and
        /// stay so: they have been legal FEMEX since the first commit, and a zero
        /// density is already reported in its own words by
        /// <see cref="ValidateSelfWeight"/>. These fields are new and can be given a
        /// contract from the start — the same line <see cref="ValidateSections"/>
        /// draws against a zero-width <see cref="Rectangle"/>.
        /// </summary>
        private IEnumerable<string> ValidateMaterials()
        {
            foreach (var material in Materials)
            {
                foreach (var (what, value) in EnumerateStatedMaterialValues(material))
                {
                    if (value <= 0.0)
                    {
                        yield return $"Material {material.Id} states {what} of {value:G6}, which is not a " +
                                     "positive quantity.";
                    }
                }
            }
        }

        /// <summary>
        /// Every 1.7 value a material actually states, named as the file names it —
        /// the two scalars on the material itself, then the design block.
        /// </summary>
        private static IEnumerable<(string What, double Value)> EnumerateStatedMaterialValues(Material material)
        {
            if (material.ShearModulus is double g) yield return ("a shearModulus", g);
            if (material.ThermalExpansion is double alpha) yield return ("a thermalExpansion", alpha);

            if (material.Properties is not MaterialProperties properties)
                yield break;

            if (properties.Fy is double fy) yield return ("a fy", fy);
            if (properties.Fu is double fu) yield return ("a fu", fu);
            if (properties.FuMinimum is double fuMinimum) yield return ("a fuMinimum", fuMinimum);
            if (properties.Ry is double ry) yield return ("a ry", ry);
            if (properties.Rt is double rt) yield return ("a rt", rt);
            if (properties.Fck is double fck) yield return ("a fck", fck);
            if (properties.Fcm is double fcm) yield return ("a fcm", fcm);
            if (properties.Fctm is double fctm) yield return ("a fctm", fctm);
            if (properties.Fctk05 is double fctk05) yield return ("a fctk05", fctk05);
            if (properties.Fctk95 is double fctk95) yield return ("a fctk95", fctk95);
            if (properties.EpsC2 is double epsC2) yield return ("an epsC2", epsC2);
            if (properties.EpsCu2 is double epsCu2) yield return ("an epsCu2", epsCu2);
            if (properties.EpsC3 is double epsC3) yield return ("an epsC3", epsC3);
            if (properties.EpsCu3 is double epsCu3) yield return ("an epsCu3", epsCu3);
            if (properties.E005 is double e005) yield return ("an e005", e005);
            if (properties.E90Mean is double e90Mean) yield return ("an e90Mean", e90Mean);
            if (properties.Fmk is double fmk) yield return ("a fmk", fmk);
            if (properties.Ft0k is double ft0k) yield return ("a ft0k", ft0k);
            if (properties.Ft90k is double ft90k) yield return ("a ft90k", ft90k);
            if (properties.Fc0k is double fc0k) yield return ("a fc0k", fc0k);
            if (properties.Fc90k is double fc90k) yield return ("a fc90k", fc90k);
            if (properties.Fvk is double fvk) yield return ("a fvk", fvk);
        }

        /// <summary>
        /// Materials that are legal FEMEX and that a receiver gets wrong: one that
        /// does not say what it is, and one a thermal load cannot be resolved
        /// against.
        ///
        /// The first warns on <i>absence</i>, which
        /// <see cref="ValidateSectionCompleteness"/> deliberately never does — it
        /// warns only about incoherent claims. The precedent is
        /// <see cref="ReportNameKeys"/>, which warns that an unnamed entity will have
        /// a name invented for it: the argument is the same one, and so is the
        /// consequence. SAF marks <c>Type</c> mandatory, so there is no writing the
        /// material out without a value, and what an exporter cannot read it will
        /// guess from the density and the modulus.
        ///
        /// That first rule has <b>two wordings and fires once</b>, the graded one
        /// saying strictly more. A material with a quality and no type is a subset of
        /// a material with no type, and reporting both would state one fact twice.
        ///
        /// The second is the thermal inconsistency made executable — a temperature
        /// change that reaches a material with no α is a number nothing can turn into
        /// a strain. Reported once per material a load reaches, not once per element:
        /// a thermal load on a meshed slab names eleven faces of one concrete, and
        /// eleven copies of one message would bury every other one.
        /// </summary>
        private IEnumerable<string> ValidateMaterialCompleteness(ValidationContext ctx)
        {
            foreach (var material in Materials)
            {
                if (material.Type is not null)
                    continue;

                if (!string.IsNullOrWhiteSpace(material.Quality))
                {
                    yield return $"Material {material.Id} is graded \"{material.Quality}\" but states no " +
                                 "type; a grade names nothing without the code family it belongs to.";
                }
                else
                {
                    yield return $"Material {material.Id} states no type; a program that has to write one " +
                                 "will guess it from the density and the modulus.";
                }
            }

            foreach (var load in Loads)
            {
                if (load is not TemperatureLoad temperature)
                    continue;

                var reported = new HashSet<int>();

                foreach (int elementId in temperature.ElementIds)
                {
                    if (!ctx.TryGetElementMaterialId(elementId, out int materialId))
                        continue;

                    if (!reported.Add(materialId))
                        continue;

                    Material? material = Materials.Find(m => m.Id == materialId);

                    // An unknown material id is reported by the element's own
                    // reference check; this rule has nothing to add to it.
                    if (material is null || material.ThermalExpansion is not null)
                        continue;

                    yield return $"{Describe(temperature)} acts on material {materialId}, which states no " +
                                 "thermalExpansion; the receiving program has a temperature change and " +
                                 "nothing to turn it into a strain with.";
                }
            }
        }

        // ----- Load groups -----

        /// <summary>
        /// The one reference a load group participates in, checked the way every
        /// other reference in this file is: a case naming a group that is not there.
        /// </summary>
        private IEnumerable<string> ValidateLoadGroups(ValidationContext ctx)
        {
            foreach (var loadCase in LoadCases)
            {
                if (loadCase.LoadGroupId is int groupId && !ctx.LoadGroupIds.Contains(groupId))
                    yield return $"Load case {loadCase.Number} references unknown load group {groupId}.";
            }
        }

        /// <summary>
        /// The two ways a load group is legal FEMEX and still wrong in the receiving
        /// program.
        ///
        /// <b>The first is a group nothing is in.</b> A group exists to say how its
        /// cases combine, so an empty one says nothing about anything — and unlike an
        /// unused section it will be written into SAF's <c>StructuralLoadGroup</c>
        /// sheet, where it becomes a row a code-combination generator has to decide
        /// what to do with.
        ///
        /// <b>The second is the one this bump had to design in rather than
        /// discover.</b> After 1.9 a case carries two statements of the same
        /// category: its own <see cref="LoadNature"/>, and the
        /// <see cref="LoadGroupType"/> of the group it names. They overlap almost
        /// entirely and nothing stops them disagreeing — <c>Nature = Dead</c> in a
        /// group typed <c>Variable</c> — and combination factors are exactly what
        /// that changes. A bump written to close one silent wrong answer would
        /// otherwise have manufactured another. The compatibility map is stated once,
        /// in <see cref="NatureGroupType"/>, and this is it made executable. The
        /// reference workbook contains precisely this defect, and no validator on
        /// that side said a word about it.
        ///
        /// <see cref="LoadGroupType.Tensioning"/> gets its own wording because it is
        /// not an author's slip: FEMEX has no <see cref="LoadNature"/> for prestress,
        /// so <i>every</i> case in a tensioning group disagrees with its group and
        /// the fix is not to change the nature.
        /// </summary>
        private IEnumerable<string> ValidateLoadGroupUsage(ValidationContext ctx)
        {
            var populated = new HashSet<int>();
            foreach (var loadCase in LoadCases)
                if (loadCase.LoadGroupId is int id)
                    populated.Add(id);

            foreach (var group in LoadGroups)
            {
                if (!populated.Contains(group.Id))
                {
                    yield return $"Load group {group.Id} names no load case; a group exists to say how " +
                                 "its cases combine, and an empty one says nothing.";
                }
            }

            foreach (var loadCase in LoadCases)
            {
                if (loadCase.LoadGroupId is not int groupId ||
                    !ctx.LoadGroupsById.TryGetValue(groupId, out LoadGroup? group))
                {
                    continue;
                }

                if (group.Type == LoadGroupType.Tensioning)
                {
                    yield return $"Load case {loadCase.Number} is in load group {group.Id}, typed " +
                                 "Tensioning, which no load nature corresponds to; its nature " +
                                 $"{loadCase.Nature} and its group say different things about how it " +
                                 "combines.";
                    continue;
                }

                LoadGroupType expected = NatureGroupType(loadCase.Nature);

                if (group.Type != expected)
                {
                    yield return $"Load case {loadCase.Number} has nature {loadCase.Nature} and is in " +
                                 $"load group {group.Id}, typed {group.Type}; {expected} is the type " +
                                 "that nature corresponds to, and the partial factors a code applies " +
                                 "are what the disagreement changes.";
                }
            }
        }

        /// <summary>
        /// The compatibility map between FEMEX's own load category and SAF's, stated
        /// once so that the validator and any adapter read the same table.
        /// <see cref="LoadGroupType.Tensioning"/> is not produced by it: no
        /// <see cref="LoadNature"/> corresponds to prestress, which is handled in its
        /// own words by the caller.
        /// </summary>
        private static LoadGroupType NatureGroupType(LoadNature nature)
        {
            switch (nature)
            {
                case LoadNature.Dead: return LoadGroupType.Permanent;
                case LoadNature.Accidental: return LoadGroupType.Accidental;
                case LoadNature.Seismic: return LoadGroupType.Seismic;
                default: return LoadGroupType.Variable;
            }
        }

        // ----- Load distribution -----

        /// <summary>
        /// A panel's spanning statement that is legal FEMEX and says nothing, or says
        /// something the receiver cannot act on.
        ///
        /// A rotation on a two-way panel is the first: the frame it rotates is not
        /// read, so the number is an instruction with no effect, and an author who
        /// wrote one meant the panel to span one way. An empty
        /// <see cref="Geometry.LoadDistribution.BarIds"/> is the second — "these
        /// members, and there are none" is a different claim from null, which means
        /// whatever bounds the panel, and it is one no receiver can carry out.
        ///
        /// The unknown-member reference itself is an error and is checked with the
        /// panel's other references in <see cref="ValidatePlates"/>.
        ///
        /// The third belongs to the other half of 1.9's placement work rather than to
        /// panels: a line load whose extent along its bar runs backwards. Legal —
        /// a receiver can swap the two — but the format's canonical form is relative
        /// from the start node, so a file stating one is either drawn the other way
        /// round or has its magnitudes on the wrong ends, and those two readings give
        /// different answers for a trapezoidal load.
        /// </summary>
        private IEnumerable<string> ValidateLoadDistributions()
        {
            foreach (var load in Loads)
            {
                if (load is not LinearLoad { StartPosition: double start, EndPosition: double end } line)
                    continue;

                if (start < end)
                    continue;

                string host = line.BarId.HasValue ? $"bar {line.BarId.Value}"
                            : line.PlateId.HasValue ? $"plate {line.PlateId.Value}"
                            : "no host";

                yield return $"{Describe(line)} runs from {start:G6} to {end:G6} along {host}; a " +
                             "position along a member is measured from the start node, so this extent " +
                             "runs backwards or is empty.";
            }

            foreach (var plate in Plates)
            {
                foreach (var m in ReportDistribution(plate.Distribution, $"Plate {plate.Id}"))
                    yield return m;

                foreach (var region in plate.Regions)
                {
                    foreach (var m in ReportDistribution(region.Distribution,
                                                         $"Plate {plate.Id} region {region.Id}"))
                        yield return m;
                }
            }
        }

        private static IEnumerable<string> ReportDistribution(LoadDistribution? distribution, string owner)
        {
            if (distribution is null)
                yield break;

            if (distribution.Spanning == SurfaceLoadSpanning.TwoWay && distribution.RotationAngle != 0.0)
            {
                yield return $"{owner} states a load distribution rotated {distribution.RotationAngle:G6} " +
                             "degrees and spanning two ways; nothing reads the angle of a two-way panel.";
            }

            if (distribution.BarIds is { Count: 0 })
            {
                yield return $"{owner} states a load distribution whose barIds list is empty, which says " +
                             "the load goes to no member at all. Omit the list to mean \"whatever bounds " +
                             "the panel\".";
            }
        }

        // ----- Loads -----

        /// <summary>
        /// A thermal gradient stated about an axis the element it acts on does not
        /// have. A surface has one through-thickness direction, its local z, so a
        /// <c>gradientY</c> reaching a plate or a mesh face is a number with nowhere
        /// to go — where on a bar it is the across-the-width gradient of a beam
        /// heated on one flank, and perfectly meaningful.
        ///
        /// A warning and not a schema rule, because one temperature load may name
        /// bars and plates together and the same field is right for half of them.
        /// Reported once per load rather than once per element, the discipline
        /// <see cref="ValidateMaterialCompleteness"/> follows for a thermal load
        /// reaching a meshed slab.
        /// </summary>
        private IEnumerable<string> ValidateThermalGradients(ValidationContext ctx)
        {
            foreach (var load in Loads)
            {
                if (load is not TemperatureLoad temperature || temperature.GradientY is null)
                    continue;

                var surfaces = new List<int>();

                foreach (int elementId in temperature.ElementIds)
                    if (!ctx.BarIds.Contains(elementId) && ctx.ElementIds.Contains(elementId))
                        surfaces.Add(elementId);

                if (surfaces.Count == 0)
                    continue;

                yield return $"{Describe(temperature)} states a gradientY and acts on surface elements " +
                             $"{FormatNumberList(surfaces)}; a surface has one through-thickness axis, " +
                             "its local z, and the other gradient has nowhere to act.";
            }
        }

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
                    case PointLoad pl:
                        // The node is the target only when no bar is named; a load
                        // placed along a member does not have to sit on a node at
                        // all, which is the whole of what 1.9 added here.
                        if (pl.BarId is null && !ctx.NodeNumbers.Contains(pl.NodeNumber))
                            yield return $"Point load '{pl.Label}' references unknown node {pl.NodeNumber}.";

                        foreach (var m in ValidateHostBar(ctx, Describe(pl), pl.BarId))
                            yield return m;

                        foreach (var m in ValidatePosition(Describe(pl), "position", pl.Position, pl.BarId))
                            yield return m;
                        break;

                    case LinearLoad ll:
                        if (!ctx.NodeNumbers.Contains(ll.StartNode))
                            yield return $"Linear load '{ll.Label}' references unknown start node {ll.StartNode}.";
                        if (!ctx.NodeNumbers.Contains(ll.EndNode))
                            yield return $"Linear load '{ll.Label}' references unknown end node {ll.EndNode}.";

                        foreach (var m in ValidateLoadPosition(Describe(ll), "startPosition",
                                                               ll.StartPosition, ll))
                            yield return m;
                        foreach (var m in ValidateLoadPosition(Describe(ll), "endPosition",
                                                               ll.EndPosition, ll))
                            yield return m;
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
                foreach (var m in ValidateLinearLoadHost(ctx, owner, line))
                    yield return m;
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

        /// <summary>
        /// The host a line load names, and the edge it names on it. Everything here
        /// is worded to match <see cref="ValidateHinge"/>, because a load on a
        /// panel's contour edge and a hinge on that same edge are the same claim
        /// about the same two nodes — and, from 1.11, are resolved through the same
        /// <see cref="TryGetEdgeLocalAxes"/>.
        ///
        /// <b>At most one host.</b> A load naming both a bar and a plate says two
        /// different things about what its direction and its positions are measured
        /// against, and a receiver has no rule for choosing between them.
        ///
        /// Errors throughout, for the reason <see cref="ValidateLoadOrientation"/>
        /// gives: a load whose host does not resolve has no direction and no extent,
        /// and there is nothing for a receiver to fall back on.
        /// </summary>
        private static IEnumerable<string> ValidateLinearLoadHost(ValidationContext ctx, string owner,
                                                                  LinearLoad line)
        {
            foreach (var m in ValidateHostBar(ctx, owner, line.BarId))
                yield return m;

            if (line.BarId.HasValue && line.PlateId.HasValue)
            {
                yield return $"{owner} names both bar {line.BarId.Value} and plate " +
                             $"{line.PlateId.Value}; a load is measured along one of them, not both.";
            }

            if (line.PlateId is int plateId)
            {
                if (!ctx.ElementIds.Contains(plateId))
                {
                    yield return $"{owner} references unknown plate {plateId}.";
                }
                else if (!ctx.PlatesById.TryGetValue(plateId, out Plate? plate))
                {
                    yield return $"{owner} names element {plateId} as its plate, but that element is not a plate.";
                }
                else
                {
                    List<int>? contour = plate.NodeIds;
                    string where = "the contour";

                    if (line.RegionId is int regionId)
                    {
                        PlateRegion? region = plate.Regions.Find(r => r.Id == regionId);
                        if (region is null)
                        {
                            yield return $"{owner} references region {regionId}, which does not exist on plate {plateId}.";
                            contour = null;
                        }
                        else
                        {
                            contour = region.NodeIds;
                            where = $"region {region.Id}'s contour";
                        }
                    }

                    if (contour is not null && !AreAdjacent(contour, line.StartNode, line.EndNode))
                    {
                        yield return $"{owner} runs along plate {plateId} edge {line.StartNode}->{line.EndNode}, " +
                                     $"but those nodes are not adjacent in {where}.";
                    }
                }
            }
            else if (line.RegionId.HasValue)
            {
                yield return $"{owner} sets regionId without plateId.";
            }

            if (line.BarId is null && line.PlateId is null &&
                line.CoordinateSystem == LoadCoordinateSystem.Local)
            {
                yield return $"{owner} has a local direction but names neither a bar nor a plate; " +
                             "there is nothing to resolve it against.";
            }
        }

        /// <summary>
        /// The bar a load, support or hinge names as its host: it must exist, and it
        /// must be a bar. Both wordings are the ones
        /// <see cref="ValidateLinearLoadHost"/> already uses for
        /// <c>LinearLoad.BarId</c>, shared rather than restated so the four consumers
        /// of a host reference cannot drift apart.
        ///
        /// Silent on null, which is every object written before 1.9 and every one
        /// that acts at a node.
        /// </summary>
        private static IEnumerable<string> ValidateHostBar(ValidationContext ctx, string owner, int? barId)
        {
            if (barId is not int id)
                yield break;

            if (!ctx.ElementIds.Contains(id))
                yield return $"{owner} references unknown bar {id}.";
            else if (!ctx.BarIds.Contains(id))
                yield return $"{owner} names element {id} as its bar, but that element is not a bar.";
        }

        /// <summary>
        /// A position along a member: relative, so it lies between 0 and 1, and
        /// meaningless without the member it is measured along.
        ///
        /// Errors both. A station outside the member is not an approximation of
        /// anything — a receiver has no rule for a load at 1.4 of a beam — and a
        /// position with no host is a number about nothing. 1.9's whole argument for
        /// storing the position rather than snapping it is that the value is exact;
        /// a value that cannot be resolved is the one case where that is untrue.
        /// </summary>
        private static IEnumerable<string> ValidatePosition(string owner, string field,
                                                            double? position, int? barId)
        {
            if (position is not double value)
                yield break;

            if (barId is null)
            {
                yield return $"{owner} states a {field} but names no bar; a position along a member " +
                             "needs the member it is measured along.";
            }

            foreach (var m in ValidateRelative(owner, field, value))
                yield return m;
        }

        /// <summary>
        /// The same rule for a line load, whose host may be a bar or, from 1.11, a
        /// plate contour edge.
        ///
        /// Split from <see cref="ValidatePosition"/> on the wording alone, and the
        /// range check is shared rather than copied. A support and a hinge sit on a
        /// member and nothing else, so telling their author to name a plate would be
        /// advice they cannot take; a line load can sit on either, so it has to be
        /// told about both.
        /// </summary>
        private static IEnumerable<string> ValidateLoadPosition(string owner, string field,
                                                                double? position, LinearLoad line)
        {
            if (position is not double value)
                yield break;

            if (line.BarId is null && line.PlateId is null)
            {
                yield return $"{owner} states a {field} but names neither a bar nor a plate; a position " +
                             "needs the member or the edge it is measured along.";
            }

            foreach (var m in ValidateRelative(owner, field, value))
                yield return m;
        }

        private static IEnumerable<string> ValidateRelative(string owner, string field, double value)
        {
            if (value < 0.0 || value > 1.0)
            {
                yield return $"{owner} states a {field} of {value:G6}; a position along a member is " +
                             "relative, 0 at the start node and 1 at the end node.";
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
            else if (string.Equals(SchemaVersion, "1.5", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.5\", written before standard steel " +
                             "shapes and catalogue identity existed, so no section in it names a profile " +
                             "or the library it came out of. Re-saving it writes the current format.";
            }
            else if (string.Equals(SchemaVersion, "1.6", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.6\", written before a material could " +
                             "say what it is, so no material in it carries a type, a grade, a thermal " +
                             "expansion coefficient or any design value beyond one unnamed strength. " +
                             "Re-saving it writes the current format.";
            }
            else if (string.Equals(SchemaVersion, "1.7", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.7\", written before units were typed " +
                             "and before a restraint had a direction, so its unit convention was free " +
                             "text and every support in it resists in both directions. Re-saving it " +
                             "writes the current format.";
            }
            else if (string.Equals(SchemaVersion, "1.8", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.8\", written before load groups " +
                             "existed, before a panel could say which way it spans, and before a " +
                             "thermal gradient had a sign convention, so every panel in it is read as " +
                             "spanning two ways and no load in it sits anywhere but on a node. " +
                             "Re-saving it writes the current format.";
            }
            else if (string.Equals(SchemaVersion, "1.9", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.9\", written before a member could " +
                             "say how it behaves, where its system line runs, how far it is offset from " +
                             "it or that it tapers, so every bar in it is read as a prismatic frame " +
                             "member on its own centroid. Re-saving it writes the current format.";
            }
            else if (string.Equals(SchemaVersion, "1.10", StringComparison.Ordinal))
            {
                yield return "The model declares schemaVersion \"1.10\", written before a line load " +
                             "could name the plate whose contour edge it runs along, so no edge-hosted " +
                             "load in it states a local direction or a partial extent. Re-saving it " +
                             "writes the current format.";
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
        private static readonly string[] SelfWeightVersions = { "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "1.10", CurrentSchemaVersion };

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

            if (_bothDensitySpellingsMaterialIds is not null)
            {
                foreach (int id in _bothDensitySpellingsMaterialIds)
                {
                    yield return $"Material {id} carries both a unitWeight and a density; the density is " +
                                 "used and the unit weight ignored.";
                }
            }

            if (_migratedUnits is not null)
            {
                foreach (var (what, text, unit) in _migratedUnits)
                {
                    yield return $"The units block states a {what} of \"{text}\" as free text, which has " +
                                 $"been read as {unit}. Re-saving the model writes the typed spelling and " +
                                 $"cannot write the free-text one.";
                }
            }

            // The one migration in FEMEX that loses something, and it says exactly
            // what. Free text that names no unit is not an annotation, and carrying
            // it forward is the defect the bump exists to end.
            if (_droppedUnits is not null)
            {
                foreach (var (what, text) in _droppedUnits)
                {
                    yield return $"The units block states a {what} of \"{text}\", which names no unit this " +
                                 $"build knows. It has been dropped, and the model now states no {what} " +
                                 "unit at all.";
                }
            }

            if (_bothUnitSpellings is not null)
            {
                foreach (string what in _bothUnitSpellings)
                {
                    yield return $"The units block states the {what} unit both as free text and as a typed " +
                                 $"{what}Unit; the typed one is used and the free-text one ignored.";
                }
            }

            // The one migration that changes what a number means rather than what a
            // key is called, and the word "reinterpretation" is the point: 1.6's
            // gradientPerDepth carried no sign convention, and gradientZ has one, so
            // reading the value across gives it a meaning nobody ever stated. It is
            // said per load, with the value, because the author has to be able to
            // find the number and confirm it.
            if (_reinterpretedGradients is not null)
            {
                foreach (var (id, label, value) in _reinterpretedGradients)
                {
                    yield return $"Load {id} '{label}' was written as a gradientPerDepth of {value:G6}, " +
                                 "which carried no sign convention. It has been read as a gradientZ, a " +
                                 "reinterpretation rather than a rename: positive now means the " +
                                 "temperature increases along the element's local +z, and which face is " +
                                 "the hot one decides which way the element curves. Confirm the sign.";
                }
            }

            if (_bothGradientSpellings is null)
                yield break;

            foreach (var (id, label) in _bothGradientSpellings)
            {
                yield return $"Load {id} '{label}' carries both a gradientPerDepth and a gradientZ; the " +
                             "gradientZ is used and the gradientPerDepth ignored.";
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
                // Label! for the netstandard annotation gap; see FormatNameKeyMessages.
                else if (!seenLabels.Add(combination.Label!) && reportedLabels.Add(combination.Label!))
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

                foreach (var m in ValidateHostBar(ctx, $"Support {support.Id}", support.BarId))
                    yield return m;

                foreach (var m in ValidatePosition($"Support {support.Id}", "position",
                                                    support.Position, support.BarId))
                    yield return m;

                foreach (var m in ValidatePosition($"Support {support.Id}", "endPosition",
                                                    support.EndPosition, support.BarId))
                    yield return m;

                if (support.BarId.HasValue)
                {
                    // A bar is a line, so it can carry a support at a point on it or
                    // along a stretch of it, and nothing else. An area support
                    // follows a plate, which is what PlateId is for.
                    if (support.Target == SupportTarget.Area)
                    {
                        yield return $"Support {support.Id} follows bar {support.BarId.Value} but its " +
                                     "target is Area; an area support follows a plate.";
                    }

                    if (support.Target != SupportTarget.Linear && support.EndPosition.HasValue)
                    {
                        yield return $"Support {support.Id} states an endPosition but its target is " +
                                     $"{support.Target}; only a Linear support occupies a stretch of a bar.";
                    }

                    if (support.PlateId.HasValue)
                        yield return $"Support {support.Id} follows both bar {support.BarId.Value} and plate {support.PlateId.Value}; use one.";
                }

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

        /// <summary>
        /// Supports that are legal FEMEX and that a receiver gets wrong: one whose
        /// spring stiffness has no readable magnitude, and one whose sense describes
        /// nothing.
        ///
        /// <b>The bedding rule is the executable half of a documentation change.</b>
        /// <see cref="BoundaryConditions.Restraint.Stiffness"/> now states what the
        /// number is measured against for each
        /// <see cref="BoundaryConditions.SupportTarget"/> — a total spring at a point,
        /// per unit length along a line, a bedding modulus per unit area over an area.
        /// Saying so fixes the ambiguity <c>FEMEX_SAF_Fit.md</c> §4 item 7 records,
        /// where two adapters could read one file and differ by a factor of the slab
        /// area. It does not fix the other half: force/length³ is a dimension whose
        /// magnitude cannot be read at all without knowing the units, and kN/m³ and
        /// kN/mm³ are nine orders of magnitude apart. So the area case, and only it,
        /// asks the model to say.
        ///
        /// Scoped to <see cref="SupportTarget.Area"/> deliberately. A point spring is
        /// also unit-dependent, but it has been legal since the first commit and
        /// nagging about every existing model's units is not what this rule is for —
        /// the same line <see cref="ValidateMaterials"/> draws around what 1.7 added.
        ///
        /// <b>The rotational rule is the price of the factoring.</b>
        /// <see cref="Support"/> applies one <see cref="Restraint"/> across all six
        /// DOFs, which is the right shape and is exactly why the type cannot forbid a
        /// compression-only moment. A moment has no compression side; the value parses
        /// and describes nothing. One message per support rather than per DOF, the
        /// discipline <see cref="ValidateMaterialCompleteness"/> follows for a thermal
        /// load — three DOFs of one mistake are one mistake.
        /// </summary>
        private IEnumerable<string> ValidateSupportCompleteness()
        {
            bool unitsAreStated = Units is not null && Units.Length is not null && Units.Force is not null;

            foreach (var support in Supports)
            {
                if (!unitsAreStated &&
                    support.Target == SupportTarget.Area &&
                    EnumerateDofs(support).Any(d => d.Restraint.Stiffness.HasValue))
                {
                    yield return $"Support {support.Id} is an area support stating a stiffness, which is a " +
                                 "bedding modulus in force per unit length cubed, and the model states no " +
                                 "units; nothing can tell that number from one nine orders of magnitude " +
                                 "away.";
                }

                var senseless = EnumerateDofs(support)
                    .Where(d => d.Dof[0] == 'r' && d.Restraint.Sense is RestraintSense.CompressionOnly
                                                                     or RestraintSense.TensionOnly)
                    .Select(d => d.Dof)
                    .ToList();

                if (senseless.Count > 0)
                {
                    yield return $"Support {support.Id} states a sense on {FormatNameList(senseless)}; a " +
                                 "moment has no compression side, so the sense of a rotational restraint " +
                                 "describes nothing.";
                }
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

                // The hinge's own element is the member the position is measured
                // along, which is why it gains no barId of its own.
                foreach (var m in ValidatePosition($"Hinge {hinge.Id}", "position", hinge.Position,
                                                    ctx.BarIds.Contains(hinge.ElementId) ? hinge.ElementId : null))
                    yield return m;

                yield break;
            }

            if (hinge.Position.HasValue)
            {
                yield return $"Hinge {hinge.Id} states a position but element {hinge.ElementId} is a " +
                             "plate; a position along a member is measured along a bar.";
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

            return string.Join(", ", list.Take(list.Count - 1)) + " and " + list[list.Count - 1];
        }

        /// <summary>
        /// The same joining for names rather than numbers — "rx", "rx and ry",
        /// "rx, ry and rz". A second method rather than a generic one, so that the
        /// numeric wording above stays the thing every other message already reads.
        /// </summary>
        private static string FormatNameList(IEnumerable<string> names)
        {
            var list = names.ToList();
            if (list.Count <= 1)
                return string.Join(string.Empty, list);

            return string.Join(", ", list.Take(list.Count - 1)) + " and " + list[list.Count - 1];
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
            public readonly HashSet<int> LoadGroupIds;
            public readonly Dictionary<int, LoadGroup> LoadGroupsById;
            public readonly HashSet<int> LoadCaseNumbers;

            /// <summary>
            /// Every uid in the model, which is what a <c>ParentUid</c> is resolved
            /// against. A set and not a map: the rule is only whether the parent is
            /// in the file, and nothing traverses to it.
            /// </summary>
            public readonly HashSet<Guid> Uids;
            public readonly HashSet<int> BarIds;
            public readonly HashSet<int> ElementIds;
            public readonly HashSet<int> MeshNodeIds;
            public readonly Dictionary<int, Plate> PlatesById;
            public readonly List<int> AllElementIdsInOrder;

            private readonly Dictionary<int, Node> _nodesByNumber;
            private readonly Dictionary<int, double> _elevationsByLevel;
            private readonly Dictionary<int, int> _materialIdByElementId;

            // A bar carries no hinge: hinges are a root list pointing back at their
            // element, so the only way to ask "what is hinged on this member" is to
            // index them the other way round once.
            private readonly Dictionary<int, List<Hinge>> _hingesByElementId;

            public ValidationContext(FemexModel model)
            {
                GridIds = new HashSet<int>(model.Grids.Select(g => g.Id));
                LevelNumbers = new HashSet<int>(model.Levels.Select(l => l.LevelNumber));
                NodeNumbers = new HashSet<int>(model.Nodes.Select(n => n.NodeNumber));
                SectionIds = new HashSet<int>(model.Sections.Select(s => s.Id));
                SurfacePropertyIds = new HashSet<int>(model.SurfaceProperties.Select(s => s.Id));
                MaterialIds = new HashSet<int>(model.Materials.Select(m => m.Id));

                LoadGroupIds = new HashSet<int>(model.LoadGroups.Select(g => g.Id));
                LoadGroupsById = new Dictionary<int, LoadGroup>();
                foreach (var group in model.LoadGroups)
                    LoadGroupsById[group.Id] = group;

                LoadCaseNumbers = new HashSet<int>(model.LoadCases.Select(c => c.Number));

                Uids = new HashSet<Guid>();
                foreach (var (entity, _, _) in model.EnumerateIdentified())
                    if (entity.Uid is Guid uid)
                        Uids.Add(uid);
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

                _materialIdByElementId = new Dictionary<int, int>();

                foreach (var bar in model.Bars)
                    _materialIdByElementId[bar.Id] = bar.MaterialId;

                foreach (var plate in model.Plates)
                {
                    if (plate.Kind != PlateRegionKind.Opening && plate.MaterialId is int plateMaterialId)
                        _materialIdByElementId[plate.Id] = plateMaterialId;
                }

                if (model.Mesh is not null)
                {
                    foreach (var face in model.Mesh.Faces)
                    {
                        // The face's own resolved cache first, then the panel it came
                        // out of — a mesher that filled the cache and one that left it
                        // null must reach the same material, so this reuses the plate
                        // resolution rather than restating it.
                        if (face.MaterialId is int faceMaterialId)
                        {
                            _materialIdByElementId[face.Id] = faceMaterialId;
                            continue;
                        }

                        if (!PlatesById.TryGetValue(face.PlateId, out Plate? panel))
                            continue;

                        PlateRegion? region = face.RegionId is int regionId
                            ? panel.Regions.Find(r => r.Id == regionId)
                            : null;

                        var (kind, _, materialId) = GetEffectiveProperties(panel, region);

                        if (kind != PlateRegionKind.Opening && materialId is int resolved)
                            _materialIdByElementId[face.Id] = resolved;
                    }
                }

                _hingesByElementId = new Dictionary<int, List<Hinge>>();
                foreach (var hinge in model.Hinges)
                {
                    if (!_hingesByElementId.TryGetValue(hinge.ElementId, out List<Hinge>? onElement))
                    {
                        onElement = new List<Hinge>();
                        _hingesByElementId[hinge.ElementId] = onElement;
                    }

                    onElement.Add(hinge);
                }

                _nodesByNumber = new Dictionary<int, Node>();
                foreach (var node in model.Nodes)
                    _nodesByNumber[node.NodeNumber] = node;

                _elevationsByLevel = new Dictionary<int, double>();
                foreach (var level in model.Levels)
                    _elevationsByLevel[level.LevelNumber] = level.AbsoluteElevation;
            }

            /// <summary>
            /// The material an element is made of, across all three element kinds.
            /// Returns false when the element is unknown, when it is an opening, and
            /// when a plate or face leaves its material null — an element that
            /// resolves to no material at all is reported by
            /// <see cref="ValidatePlates"/> in its own words, and nothing that reads
            /// this has anything to add.
            /// </summary>
            public bool TryGetElementMaterialId(int elementId, out int materialId)
            {
                return _materialIdByElementId.TryGetValue(elementId, out materialId);
            }

            /// <summary>
            /// Every hinge attached to one element, in list order. Empty when
            /// nothing is, which is the common case.
            /// </summary>
            public IReadOnlyList<Hinge> HingesOn(int elementId)
            {
                return _hingesByElementId.TryGetValue(elementId, out List<Hinge>? hinges)
                    ? hinges
                    : Array.Empty<Hinge>();
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
