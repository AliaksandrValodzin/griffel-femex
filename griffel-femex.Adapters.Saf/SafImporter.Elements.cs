using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Interop;
using SAF.DataAccess.Models.Enums;
using SAF.DataAccess.Models.StructuralElements;

namespace griffel_femex.Adapters.Saf
{
    public sealed partial class SafImporter
    {
        // ---- Members and ribs --------------------------------------------------

        private static void ImportMembers(FemexModel model, SafObjects objects, SafIndex index,
                                          SafMessageLog log)
        {
            Dictionary<string, ExcelStructuralCurveMemberVarying> varyings = objects.Varyings
                .Where(v => !string.IsNullOrEmpty(v.Name))
                .GroupBy(v => v.Name!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            int id = 1;
            int nonStandardFrames = 0;

            foreach (ExcelStructuralCurveMember source in objects.Members)
            {
                List<Node> nodes = Resolve(source.Nodes, index);
                if (nodes.Count < 2)
                    continue;

                Section? section = Lookup(index.Sections, source.CrossSection);
                Guid? uid = SafIdentity.UidOf(source);

                if (StatesItsOwnFrame(source))
                    nonStandardFrames++;

                // One SAF member becomes one bar when it is a straight line between
                // two nodes, and a chain otherwise. The first bar of a chain keeps
                // the member's uid and the rest point at it, which is what makes the
                // chording reversible and what lets the diff tell that eight bars are
                // one member. The first bar does not point at itself: ParentUid is a
                // provenance pointer, and an object is not derived from itself.
                bool chained = nodes.Count > 2;
                for (int i = 0; i + 1 < nodes.Count; i++)
                {
                    var bar = new Bar
                    {
                        Id = id++,
                        Uid = i == 0 ? uid : DerivedUid(uid, i),
                        ParentUid = i == 0 ? null : uid,
                        StartNodeId = nodes[i].NodeNumber,
                        EndNodeId = nodes[i + 1].NodeNumber,
                        SectionId = SectionIdFor(model, section, log, id - 1, source.Name),
                        MaterialId = MaterialIdFor(index, source.CrossSection),
                        RotationAngle = source.LCSAdjustmentRotation.Degrees,
                        Behaviour = SafEnums.ToFemex(source.Behaviour),
                        Alignment = SafEnums.ToFemex(source.SystemLine),
                        Eccentricity = EccentricityOf(source),
                    };

                    model.Bars.Add(bar);
                    if (i == 0 && !string.IsNullOrEmpty(source.Name))
                        index.Bars[source.Name!] = bar;
                }

                if (chained)
                {
                    log.Object(SafLoss.ChordedCurve,
                               new ObjectRef(FemexEntity.Bar, id - 1, uid), source.Name,
                               $"Geometrical shape {source.GeometricalShape}, {nodes.Count - 1} bars.");
                }

                ApplyVariation(model, index, log, source, varyings, source.Name);
            }

            foreach (ExcelStructuralCurveMemberRib rib in objects.Ribs)
            {
                List<Node> nodes = Resolve(rib.Nodes, index);
                if (nodes.Count < 2)
                    continue;

                Section? section = Lookup(index.Sections, rib.CrossSection);
                var bar = new Bar
                {
                    Id = id++,
                    Uid = SafIdentity.UidOf(rib),
                    StartNodeId = nodes[0].NodeNumber,
                    EndNodeId = nodes[nodes.Count - 1].NodeNumber,
                    SectionId = SectionIdFor(model, section, log, id - 1, rib.Name),
                    MaterialId = MaterialIdFor(index, rib.CrossSection),
                    Behaviour = SafEnums.ToFemex(rib.Behaviour),
                    Alignment = RibAlignment(rib.Alignment),
                };

                model.Bars.Add(bar);
                if (!string.IsNullOrEmpty(rib.Name))
                    index.Bars[rib.Name!] = bar;

                log.Object(SafLoss.RibAsBar, new ObjectRef(FemexEntity.Bar, bar.Id, bar.Uid), rib.Name,
                           $"It lay on surface {rib.Member2D}.");
            }

            if (objects.Ribs.Count > 0)
                log.Concept(SafLoss.UnmappedCompositeAction);

            if (objects.Members.Count > 0)
            {
                log.Concept(SafLoss.DroppedMemberType);
                log.Concept(SafLoss.DroppedLayerAndColour);
            }

            if (nonStandardFrames > 0)
            {
                log.Concept(SafLoss.ResolvedMemberLcs,
                            $"{nonStandardFrames} members state their local frame by a vector or a point.");
            }
        }

        /// <summary>
        /// True where the member states a local frame that FEMEX's default rule does
        /// not already produce.
        /// </summary>
        /// <remarks>
        /// FEMEX states one roll angle about the member axis, measured from its own
        /// default frame — the ETABS/SAP rule in <c>TryGetBarLocalAxes</c>. SAF
        /// states the frame six ways, of which two name a vector and two name a
        /// point. A point or a UCS cannot be reduced to a roll angle at all. A
        /// vector can, but only when it lies in the plane FEMEX's rule produces —
        /// and the default vector for each form, which is what every member in the
        /// published corpus uses, is the case where it does.
        ///
        /// The SDK marks <c>Standard</c> obsolete and documents it as identical to
        /// <c>VectorY</c>, so this tests the vector rather than the enum member.
        /// </remarks>
        private static bool StatesItsOwnFrame(ExcelStructuralCurveMember source)
        {
            switch (source.LCSAdjustmentLCS)
            {
                case ExcelCurveLCSType.PointY:
                case ExcelCurveLCSType.PointZ:
                // FromUCS is marked obsolete by the SDK and still readable in an
                // older workbook, so it is matched by value rather than by name.
                case (ExcelCurveLCSType)5:
                    return true;
                case ExcelCurveLCSType.VectorZ:
                    return !IsAxis(source, 0.0, 0.0, 1.0);
                default:
                    return !IsAxis(source, 0.0, 1.0, 0.0);
            }
        }

        /// <summary>
        /// A rib's alignment is stated on its own three-value enum rather than on the
        /// nine-value one the member sheet uses. The three it has land exactly.
        /// </summary>
        private static BarAlignment? RibAlignment(ExcelRibAlignment? alignment)
        {
            switch (alignment)
            {
                case ExcelRibAlignment.Top: return BarAlignment.Top;
                case ExcelRibAlignment.Bottom: return BarAlignment.Bottom;
                case ExcelRibAlignment.Centre: return BarAlignment.Centre;
                default: return null;
            }
        }

        /// <summary>
        /// The uid for the nth piece of one SAF row, or null where the row itself
        /// carried none and there is nothing to derive from.
        /// </summary>
        private static Guid? DerivedUid(Guid? parent, int index)
        {
            return parent.HasValue ? SafIdentity.Derived(parent.Value, index) : (Guid?)null;
        }

        private static bool IsAxis(ExcelStructuralCurveMember source, double x, double y, double z)
        {
            const double tolerance = 1e-9;
            return Math.Abs(SafUnits.Metres(source.LCSAdjustmentX) - x) < tolerance &&
                   Math.Abs(SafUnits.Metres(source.LCSAdjustmentY) - y) < tolerance &&
                   Math.Abs(SafUnits.Metres(source.LCSAdjustmentZ) - z) < tolerance;
        }

        /// <summary>
        /// SAF splits eccentricity into a structural half — where the member is
        /// drawn — and an analysis half — where its stiffness acts. FEMEX 1.10 keeps
        /// both, which is the split SAF gets right and most programs fuse.
        /// </summary>
        private static BarEccentricity? EccentricityOf(ExcelStructuralCurveMember source)
        {
            double ayb = source.AnalysisEccentricityYBegin.Meters;
            double aye = source.AnalysisEccentricityYEnd.Meters;
            double azb = source.AnalysisEccentricityZBegin.Meters;
            double aze = source.AnalysisEccentricityZEnd.Meters;
            double syb = source.StructuralEccentricityYBegin.Meters;
            double sye = source.StructuralEccentricityYEnd.Meters;
            double szb = source.StructuralEccentricityZBegin.Meters;
            double sze = source.StructuralEccentricityZEnd.Meters;

            bool any = ayb != 0 || aye != 0 || azb != 0 || aze != 0 ||
                       syb != 0 || sye != 0 || szb != 0 || sze != 0;
            if (!any)
                return null;

            return new BarEccentricity
            {
                AnalysisYBegin = ayb,
                AnalysisYEnd = aye,
                AnalysisZBegin = azb,
                AnalysisZEnd = aze,
                StructuralYBegin = syb,
                StructuralYEnd = sye,
                StructuralZBegin = szb,
                StructuralZEnd = sze,
            };
        }

        /// <summary>
        /// A SAF varying member is a fixed-width repeating group of spans, each
        /// naming one cross-section or a comma-separated pair for a linear
        /// transition. FEMEX 1.10 carries a start section and an end section, which
        /// is exactly one span with a pair — the single-taper case — and an
        /// approximation of everything else.
        /// </summary>
        private static void ApplyVariation(FemexModel model, SafIndex index, SafMessageLog log,
                                           ExcelStructuralCurveMember source,
                                           Dictionary<string, ExcelStructuralCurveMemberVarying> varyings,
                                           string? memberName)
        {
            if (string.IsNullOrEmpty(source.ArbitraryDefinition) ||
                !varyings.TryGetValue(source.ArbitraryDefinition!, out var varying) ||
                memberName is null || !index.Bars.TryGetValue(memberName, out Bar? bar))
            {
                return;
            }

            // The separator is a comma here and a semicolon in every other list
            // column in the format. That is the detail that costs an afternoon.
            List<string> sections = (varying.CrossSections ?? new string[0])
                .SelectMany(entry => (entry ?? string.Empty).Split(','))
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .ToList();

            if (sections.Count == 0)
                return;

            Section? end = Lookup(index.Sections, sections[sections.Count - 1]);
            if (end is not null && end.Id != bar.SectionId)
                bar.EndSectionId = end.Id;

            int spans = varying.Spans?.Length ?? 0;
            bool singleTaper = spans == 1 && sections.Count == 2;
            if (!singleTaper)
            {
                log.Object(SafLoss.FlattenedVaryingMember,
                           new ObjectRef(FemexEntity.Bar, bar.Id, bar.Uid), memberName,
                           $"{source.ArbitraryDefinition} states {spans} spans over " +
                           $"{sections.Count} sections.");
            }
        }

        private static int SectionIdFor(FemexModel model, Section? section, SafMessageLog log,
                                        int barId, string? handle)
        {
            if (section is not null)
                return section.Id;

            // A FEMEX bar must name a section. Zero properties rather than nominal
            // ones, so that nothing downstream can return a confident wrong answer
            // against the placeholder.
            Section? existing = model.Sections.FirstOrDefault(
                s => string.Equals(s.Name, PlaceholderSectionName, StringComparison.Ordinal));

            if (existing is null)
            {
                existing = new GenericSection
                {
                    Id = model.Sections.Count == 0 ? 1 : model.Sections.Max(s => s.Id) + 1,
                    Name = PlaceholderSectionName,
                    Properties = new SectionProperties { Area = 0.0, Iy = 0.0, Iz = 0.0, J = 0.0 },
                };

                model.Sections.Add(existing);
                log.Object(SafLoss.PlaceholderSection,
                           new ObjectRef(FemexEntity.Section, existing.Id), handle,
                           "First needed by SAF member " + (handle ?? "?") + ".");
            }

            return existing.Id;
        }

        private const string PlaceholderSectionName = "SAF-unstated-section";

        private static int MaterialIdFor(SafIndex index, string? crossSectionName)
        {
            if (crossSectionName is not null &&
                index.SectionMaterials.TryGetValue(crossSectionName, out string? materialName) &&
                index.Materials.TryGetValue(materialName, out var material))
            {
                return material.Id;
            }

            return 0;
        }

        // ---- Surfaces ----------------------------------------------------------

        private static void ImportSurfaces(FemexModel model, SafObjects objects, SafIndex index,
                                           SafMessageLog log)
        {
            // Bars, plates and mesh faces share one element-id space in FEMEX, so
            // plate numbering continues where the bars stopped. Numbering them from
            // 1 alongside the bars is an error the validator catches and a wrong
            // model the diff would then blame on the mapping.
            int id = model.Bars.Count == 0 ? 1 : model.Bars.Max(b => b.Id) + 1;
            foreach (ExcelStructuralSurfaceMember source in objects.Surfaces)
            {
                Guid? uid = SafIdentity.UidOf(source);
                var reference = new ObjectRef(FemexEntity.Plate, id, uid);

                if (source.Shape == ExcelMember2DShape.Curved)
                {
                    log.Object(SafLoss.DroppedCurvedSurface, reference, source.Name);
                    continue;
                }

                List<Node> nodes = Resolve(source.Nodes, index);
                if (nodes.Count < 3)
                    continue;

                PlateBehaviour behaviour = SafEnums.ToFemex(source.Behaviour, out bool exactBehaviour);
                var plate = new Plate
                {
                    Id = id++,
                    Uid = uid,
                    Name = source.Name,
                    NodeIds = nodes.Select(n => n.NodeNumber).ToList(),
                    Kind = PlateRegionKind.Structural,
                    Behaviour = behaviour,
                    Alignment = SafEnums.ToFemex(source.Alignment),
                    SurfaceOffset = source.AnalysisEccentricityZ.Meters,
                    LocalAxisAngle = source.LCSAdjustmentRotation.Degrees,
                    MaterialId = Lookup(index.Materials, source.Material)?.Id,
                    SurfacePropertyId = SurfacePropertyIdFor(
                        model, index, log, source.Thickness?.ThicknessFirst, reference,
                        source.Name, source.ThicknessType),
                };

                model.Plates.Add(plate);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Plates[source.Name!] = plate;

                if (!exactBehaviour)
                    log.Object(SafLoss.ApproximatedSurfaceBehaviour, reference, source.Name);

                if (HasCurvedEdge(source.EdgeShapes))
                    log.Object(SafLoss.ChordedSurfaceEdge, reference, source.Name);
            }

            ImportRegions(model, objects, index, log);
            ImportLoadPanels(model, objects, index, log);
        }

        private static bool HasCurvedEdge(SAF.DataAccess.Models.Subtypes.ExcelCurveShape[]? shapes)
        {
            return shapes is not null &&
                   shapes.Any(s => s?.Shape is ExcelCurveGeometricalShape shape &&
                                   shape != ExcelCurveGeometricalShape.Line);
        }

        private static void ImportRegions(FemexModel model, SafObjects objects, SafIndex index,
                                          SafMessageLog log)
        {
            foreach (ExcelStructuralSurfaceMemberOpening opening in objects.Openings)
            {
                if (!TryHost(index, opening.Member2D, out Plate? plate))
                    continue;

                List<Node> nodes = Resolve(opening.Nodes, index);
                if (nodes.Count < 3)
                    continue;

                var region = new PlateRegion
                {
                    Id = plate!.Regions.Count + 1,
                    Uid = SafIdentity.UidOf(opening),
                    Name = opening.Name,
                    NodeIds = nodes.Select(n => n.NodeNumber).ToList(),
                    Kind = PlateRegionKind.Opening,
                };

                plate.Regions.Add(region);
                if (!string.IsNullOrEmpty(opening.Name))
                    index.Regions[opening.Name!] = (plate, region);

                if (HasCurvedEdge(opening.EdgeShapes))
                {
                    log.Object(SafLoss.ChordedSurfaceEdge,
                               new ObjectRef(FemexEntity.Plate, region.Id, region.Uid), opening.Name);
                }
            }

            foreach (ExcelStructuralSurfaceMemberRegion source in objects.Regions)
            {
                if (!TryHost(index, source.Member2D, out Plate? plate))
                    continue;

                List<Node> nodes = Resolve(source.Nodes, index);
                if (nodes.Count < 3)
                    continue;

                var reference = new ObjectRef(FemexEntity.Plate, plate!.Id, SafIdentity.UidOf(source));
                var region = new PlateRegion
                {
                    Id = plate.Regions.Count + 1,
                    Uid = SafIdentity.UidOf(source),
                    Name = source.Name,
                    NodeIds = nodes.Select(n => n.NodeNumber).ToList(),
                    Kind = PlateRegionKind.Structural,
                    MaterialId = Lookup(index.Materials, source.Material)?.Id,
                    Alignment = source.Alignment.HasValue
                        ? SafEnums.ToFemex(source.Alignment)
                        : (SurfaceAlignment?)null,
                    SurfaceOffset = source.EccentricityEz.Meters,
                    SurfacePropertyId = SurfacePropertyIdFor(
                        model, index, log, source.Thickness, reference, source.Name, null),
                };

                plate.Regions.Add(region);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Regions[source.Name!] = (plate, region);

                if (HasCurvedEdge(source.EdgeShapes))
                    log.Object(SafLoss.ChordedSurfaceEdge, reference, source.Name);
            }
        }

        /// <summary>
        /// A SAF load panel is a surface that carries load and no stiffness. FEMEX
        /// says the same thing with <c>PlateRegionKind.LoadOnly</c> and a
        /// <c>LoadDistribution</c>, so the panel maps and its spanning rule maps with
        /// it — which is the held bump that closed §4's third silent wrong answer.
        /// </summary>
        private static void ImportLoadPanels(FemexModel model, SafObjects objects, SafIndex index,
                                             SafMessageLog log)
        {
            int id = model.Plates.Count == 0 ? 1 : model.Plates.Max(p => p.Id) + 1;
            foreach (var source in objects.LoadPanels)
            {
                List<Node> nodes = Resolve(source.Nodes, index);
                if (nodes.Count < 3)
                    continue;

                var plate = new Plate
                {
                    Id = id++,
                    Uid = SafIdentity.UidOf(source),
                    Name = source.Name,
                    NodeIds = nodes.Select(n => n.NodeNumber).ToList(),
                    Kind = PlateRegionKind.LoadOnly,
                    LocalAxisAngle = source.LCSAdjustmentRotation.Degrees,
                    Distribution = new LoadDistribution
                    {
                        Spanning = SafEnums.ToFemex(source.DistributionTo),
                        RotationAngle = source.LCSAdjustmentRotation.Degrees,
                        BarIds = ResolveBars(source.Members1D, index),
                    },
                };

                model.Plates.Add(plate);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Plates[source.Name!] = plate;

                if (HasCurvedEdge(source.Edges))
                {
                    log.Object(SafLoss.ChordedSurfaceEdge,
                               new ObjectRef(FemexEntity.Plate, plate.Id, plate.Uid), source.Name);
                }
            }
        }

        private static int? SurfacePropertyIdFor(FemexModel model, SafIndex index, SafMessageLog log,
                                                 UnitsNet.Length? thickness, ObjectRef reference,
                                                 string? handle,
                                                 ExcelSurfaceDistributionVarying? distribution)
        {
            if (thickness is null)
                return null;

            double value = SafUnits.Metres(thickness);
            string key = value.ToString("R", CultureInfo.InvariantCulture);
            if (index.SurfaceProperties.TryGetValue(key, out SurfaceProperty? existing))
                return existing.Id;

            var property = new ConstantThickness
            {
                Id = model.SurfaceProperties.Count == 0
                    ? 1
                    : model.SurfaceProperties.Max(s => s.Id) + 1,
                Uid = SafIdentity.DerivedFrom("SurfaceProperty", value),
                Name = "T" + (value * 1000.0).ToString("0.###", CultureInfo.InvariantCulture),
                Thickness = value,
            };

            model.SurfaceProperties.Add(property);
            index.SurfaceProperties[key] = property;

            if (distribution.HasValue && distribution.Value != ExcelSurfaceDistributionVarying.Constant)
            {
                log.Object(SafLoss.NominalThickness,
                           new ObjectRef(FemexEntity.SurfaceProperty, property.Id), handle,
                           $"The workbook states {distribution.Value}.");
            }

            return property.Id;
        }

        // ---- Shared lookups ----------------------------------------------------

        private static List<Node> Resolve(string[]? names, SafIndex index)
        {
            var nodes = new List<Node>();
            if (names is null)
                return nodes;

            foreach (string? name in names)
            {
                // "N105; N106" with a space appears in the reference workbook where
                // every other list column has no space. Split and trim, always.
                if (name is null)
                    continue;

                foreach (string part in name.Split(';', ','))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0 && index.Nodes.TryGetValue(trimmed, out Node? node))
                        nodes.Add(node);
                }
            }

            return nodes;
        }

        private static List<int>? ResolveBars(string[]? names, SafIndex index)
        {
            if (names is null || names.Length == 0)
                return null;

            var bars = new List<int>();
            foreach (string? name in names)
            {
                if (name is null)
                    continue;

                foreach (string part in name.Split(';', ','))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0 && index.Bars.TryGetValue(trimmed, out Bar? bar))
                        bars.Add(bar.Id);
                }
            }

            return bars.Count == 0 ? null : bars;
        }

        private static bool TryHost(SafIndex index, string? name, out Plate? plate)
        {
            plate = null;
            return name is not null && index.Plates.TryGetValue(name, out plate);
        }

        private static T? Lookup<T>(Dictionary<string, T> map, string? key) where T : class
        {
            return key is not null && map.TryGetValue(key, out T? value) ? value : null;
        }
    }
}
