using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Geometry.Surfaces;
using griffel_femex.Interop;
using griffel_femex.Materials;
using griffel_femex.Synthesis;
using SAF.DataAccess.Models;
using SAF.DataAccess.Models.Enums;
using SAF.DataAccess.Models.Interfaces;
using SAF.DataAccess.Models.Libraries;
using SAF.DataAccess.Models.StructuralElements;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Reads a SAF workbook into a <see cref="FemexModel"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mapping is <c>FEMEX_SAF_Fit.md</c> §2, sheet by sheet in the
    /// specification's own chapter order, and the message catalogue is its §8.2,
    /// which lives in <see cref="SafMessages"/>. Neither is restated here: a mapping
    /// table maintained in two places is a mapping table that disagrees with itself.
    /// </para>
    /// <para>
    /// Three things about the shape of this class are decisions rather than style.
    /// <b>It groups before it maps</b>, because <c>ExcelModel.Objects</c> is a flat
    /// heterogeneous bag and because §6.2's two-phase rule is unachievable in a
    /// single streaming pass. <b>It synthesises every level</b>, which is the
    /// highest-traffic invention this adapter will ever produce: nothing in SAF
    /// references a storey, <c>Node.LevelNumber</c> is a required foreign key
    /// enforced as an error, and so a truss or a ramp acquires levels its source
    /// never had. And <b>it never mints a node to hold a load</b>: a position along
    /// a member is stored on the load, which is what keeps §7.2 equivalence — and
    /// therefore this phase's own round-trip test — meaningful.
    /// </para>
    /// </remarks>
    public sealed partial class SafImporter : IFemexImporter
    {
        private readonly ISafGateway _gateway;

        public SafImporter(ISafGateway? gateway = null)
        {
            _gateway = gateway ?? new SafGateway();
        }

        public AdapterInfo Info { get; } = new AdapterInfo(
            "SAF",
            "Structural Analysis Format",
            SafGateway.OldestReadableSpecVersion + "-" + SafGateway.WrittenSpecVersion,
            FemexModel.CurrentSchemaVersion);

        /// <summary>
        /// Declared honestly, per §7.3's capability-honesty rule: an entity is listed
        /// only where objects of that kind actually cross. <c>Grid</c> and
        /// <c>Mesh</c> are absent in both directions because SAF has neither.
        /// </summary>
        public AdapterCapabilities Capabilities { get; } = new AdapterCapabilities(
            new[]
            {
                Pair(FemexEntity.Level, TransferDirection.Both),
                Pair(FemexEntity.Node, TransferDirection.Both),
                Pair(FemexEntity.Section, TransferDirection.Both),
                Pair(FemexEntity.SurfaceProperty, TransferDirection.Both),
                Pair(FemexEntity.Bar, TransferDirection.Both),
                Pair(FemexEntity.Plate, TransferDirection.Both),
                Pair(FemexEntity.Material, TransferDirection.Both),
                Pair(FemexEntity.LoadGroup, TransferDirection.Both),
                Pair(FemexEntity.LoadCase, TransferDirection.Both),
                Pair(FemexEntity.Load, TransferDirection.Both),
                Pair(FemexEntity.LoadCombination, TransferDirection.Both),
                Pair(FemexEntity.Support, TransferDirection.Both),
                Pair(FemexEntity.Hinge, TransferDirection.Both),
            });

        private static KeyValuePair<FemexEntity, TransferDirection> Pair(
            FemexEntity entity, TransferDirection direction)
        {
            return new KeyValuePair<FemexEntity, TransferDirection>(entity, direction);
        }

        /// <summary>The entity kinds whose numbers the SI normalisation restates.</summary>
        private static readonly FemexEntity[] Restated =
        {
            FemexEntity.Level,
            FemexEntity.Node,
            FemexEntity.Section,
            FemexEntity.SurfaceProperty,
            FemexEntity.Bar,
            FemexEntity.Plate,
            FemexEntity.Material,
            FemexEntity.Load,
            FemexEntity.Support,
            FemexEntity.Hinge,
        };

        public TransferResult<FemexModel> Import(ImportRequest request,
                                                 IProgress<TransferProgress>? progress,
                                                 CancellationToken cancellationToken)
        {
            if (request is null)
                return TransferResult<FemexModel>.Failed("No import request was given.");

            if (!(request is StreamImportRequest stream))
            {
                return TransferResult<FemexModel>.Failed(
                    "The SAF adapter reads a workbook from a stream. " +
                    $"{request.GetType().Name} is not a request it can serve.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            SafReadResult read = _gateway.Read(stream.Source);
            if (read.Model is null)
            {
                string where = request.SourceName is null ? string.Empty : $" ({request.SourceName})";
                return TransferResult<FemexModel>.Failed(
                    $"The workbook could not be read{where}: {read.Failure}");
            }

            var log = new SafMessageLog();
            log.AddSdkLog(read.Log);

            // Before anything is mapped, replace every uid the SDK invented with one
            // derived from the row it invented it for. The SDK fills a blank Id cell
            // with a fresh GUID on read, so without this the same workbook read twice
            // produces two models with different identities and nothing downstream —
            // not the diff, not the round-trip assertion, not the report — can match
            // them. Deriving from the sheet and row makes the read a function of the
            // file, and the count is declared as an invention rather than passed off
            // as provenance.
            foreach (IExcelModuleObject item in read.Model.Objects)
            {
                if (item is ExcelObjectBase identified &&
                    read.MintedRows.Contains(SafGateway.RowKey(item)))
                {
                    identified.Id = SafIdentity.DerivedFromKey(SafGateway.RowKey(item));
                }
            }

            SafObjects objects = SafObjects.Group(read.Model);

            if (read.Model.Objects.Count == 0)
            {
                // A stream that is not a workbook at all does not always fail to
                // open: an empty one reads as an empty package and arrives here as a
                // model with nothing in it. That is a read failure wearing a lean
                // model's clothes, and reporting it as a successful import of nothing
                // is the shape of answer that gets acted on.
                //
                // This is not the second gate §7.3 forbids. That rule is about
                // refusing a model the adapter declared it could take; this is about
                // a source that contained no model.
                string where = request.SourceName is null ? string.Empty : $" ({request.SourceName})";
                return TransferResult<FemexModel>.Failed(
                    $"This stream contains no SAF objects at all{where}. It is either empty or not " +
                    "a SAF workbook.");
            }

            ExcelGlobalCoordinateSystem vertical =
                objects.ModelInformation?.GlobalCoordinateSystem ?? ExcelGlobalCoordinateSystem.ZVertical;
            if (vertical != ExcelGlobalCoordinateSystem.ZVertical)
            {
                // FEMEX is Z-up by definition. Permuting every coordinate, every
                // load direction and every local frame into Z-up is real work with
                // no file in the published corpus to test it against, and a
                // half-done permutation is the silent wrong answer this product
                // exists to catch. Refusing says so.
                return TransferResult<FemexModel>.Failed(
                    $"This workbook declares {vertical} as its global coordinate system. FEMEX is " +
                    "Z-up by definition, and this adapter does not rotate a model at the boundary; " +
                    "re-export the workbook with Z vertical.");
            }

            var model = new FemexModel
            {
                SchemaVersion = FemexModel.CurrentSchemaVersion,
                Units = SafUnits.ImportedUnits,
            };

            log.Concept(SafLoss.StampedUnitSystem,
                $"The workbook declared {read.Model.SystemOfUnits}.");
            log.Concept(SafLoss.InventedGravity);

            // The unit statement is one fact about the model and a restatement of
            // every number on every object that carries one. Saying it only against
            // the model would leave a rescaled load or modulus looking like an
            // unexplained change of value, which is the shape of finding a report
            // must not produce.
            foreach (FemexEntity kind in Restated)
                log.Concept(SafLoss.RestatedInSiUnits, kind);

            var index = new SafIndex();

            ImportGeneral(model, objects, log);
            ImportMaterials(model, objects, index, log);
            ImportSections(model, objects, index, log);

            cancellationToken.ThrowIfCancellationRequested();
            ImportGeometry(model, objects, index, log);

            cancellationToken.ThrowIfCancellationRequested();
            ImportMembers(model, objects, index, log);
            ImportSurfaces(model, objects, index, log);

            cancellationToken.ThrowIfCancellationRequested();
            ImportSupportsAndHinges(model, objects, index, log);

            cancellationToken.ThrowIfCancellationRequested();
            ImportLoadCases(model, objects, index, log);
            ImportLoads(model, objects, index, log);

            ReportUnmapped(objects, log);

            model.AssignMissingUids();

            if (read.MintedRows.Count > 0)
            {
                log.Concept(SafLoss.MintedUids,
                            $"{read.MintedRows.Count} rows in this workbook left the Id column blank.");
            }

            progress?.Report(new TransferProgress(null, 1, 1, "SAF import complete."));

            return TransferResult<FemexModel>.Ok(model, log.Messages);
        }

        // ---- General -----------------------------------------------------------

        private static void ImportGeneral(FemexModel model, SafObjects objects, SafMessageLog log)
        {
            ExcelProjectInformation? project = objects.ProjectInformation;
            ExcelModelInformation? information = objects.ModelInformation;

            if (project is not null || information is not null)
            {
                model.Metadata = new FileMetadata
                {
                    ProjectName = project?.Name,
                    Producer = information?.SourceApplication,
                    ProducerVersion = information?.ModuleVersion,
                    CreatedAt = FormatDate(project?.Created ?? information?.Created),
                };
            }

            if (project is not null)
                log.Concept(SafLoss.DroppedProjectColumns);

            if (information?.IgnoredObjects?.Length > 0 || information?.IgnoredGroups?.Length > 0)
                log.Concept(SafLoss.UnmappedIgnoredObjects);
        }

        private static string? FormatDate(DateTime? value)
        {
            // SAF stores these as Excel serial numbers and the SDK resolves them to
            // DateTime. FileMetadata.CreatedAt is an ISO string.
            return value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        // ---- Materials ---------------------------------------------------------

        private static void ImportMaterials(FemexModel model, SafObjects objects, SafIndex index,
                                            SafMessageLog log)
        {
            int id = 1;
            foreach (ExcelStructuralMaterial source in objects.Materials)
            {
                var material = new Material
                {
                    Id = id++,
                    Uid = SafIdentity.UidOf(source),
                    Name = source.Name,
                    Type = SafEnums.ToFemex(source.Type),
                    Quality = source.Quality,
                    ModulusOfElasticity = SafUnits.Pascals(source.EModulus),
                    PoissonsRatio = source.PoissonCoefficient ?? 0.0,
                    Density = SafUnits.KilogramsPerCubicMetre(source.UnitMass),
                    ThermalExpansion = source.ThermalExpansion.HasValue
                        ? SafUnits.InverseKelvin(source.ThermalExpansion)
                        : (double?)null,
                };

                // Stated wins over derived, and this is the row that proves why: the
                // reference workbook's three timber materials state G = 690 MPa
                // against E = 11 000 and nu = 0, where E/(2(1+nu)) gives 5 500.
                if (source.GModulus.HasValue)
                    material.ShearModulus = SafUnits.Pascals(source.GModulus);

                model.Materials.Add(material);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Materials[source.Name!] = material;

                if (!string.IsNullOrWhiteSpace(source.Subtype))
                    log.Concept(SafLoss.DroppedMaterialSubtype);
            }
        }

        // ---- Cross-sections ----------------------------------------------------

        private static void ImportSections(FemexModel model, SafObjects objects, SafIndex index,
                                           SafMessageLog log)
        {
            int id = 1;
            foreach (ExcelStructuralCrossSection source in objects.CrossSections)
            {
                Section section = SafSectionShapes.ToFemex(source, log, id);
                section.Id = id++;
                section.Uid = SafIdentity.UidOf(source);
                section.Name = source.Name;

                model.Sections.Add(section);
                if (!string.IsNullOrEmpty(source.Name))
                {
                    index.Sections[source.Name!] = section;
                    if (!string.IsNullOrEmpty(source.Material))
                        index.SectionMaterials[source.Name!] = source.Material!;
                }

                if (source.CrossSectionType == ExcelCrossSectionType.General && source.Definition is not null)
                    log.Concept(SafLoss.DroppedCompositeShape);
            }
        }

        // ---- Nodes and levels --------------------------------------------------

        private static void ImportGeometry(FemexModel model, SafObjects objects, SafIndex index,
                                           SafMessageLog log)
        {
            // §6.2, the half that is easy to skip: collect every candidate
            // coordinate and elevation first, cluster once against the finished
            // extent, then create. Streaming one point at a time would make the node
            // table a function of the read order, which is fatal to §7.2 equivalence
            // and therefore to this phase's own verification.
            var synthesis = new GeometrySynthesis();

            // Declared in a canonical order rather than in the workbook's, because
            // the order candidates are declared in is the order the levels and nodes
            // are created in, and therefore the order they are numbered in. A
            // workbook whose rows are shuffled is the same model, and §6.2 says it
            // must produce the same node table — numbering included.
            var storeyTickets = new int[objects.Storeys.Count];
            foreach (int i in Ordered(objects.Storeys.Count,
                                      i => SafUnits.Metres(objects.Storeys[i].HeightLevel)))
            {
                ExcelStructuralStorey storey = objects.Storeys[i];
                storeyTickets[i] = synthesis.AddLevel(SafUnits.Metres(storey.HeightLevel), storey.Name);
            }

            var pointTickets = new int[objects.Points.Count];
            foreach (int i in OrderedPoints(objects.Points))
            {
                ExcelStructuralPointConnection point = objects.Points[i];
                pointTickets[i] = synthesis.AddPoint(
                    SafUnits.Metres(point.X), SafUnits.Metres(point.Y), SafUnits.Metres(point.Z));
            }

            SynthesisResult result = synthesis.Build(model);
            log.AddRange(result.Messages);

            // A synthesised level has no counterpart in the workbook, so it has
            // nothing to take a uid from and a minted one would differ on every read.
            // Its identity is its elevation, because its elevation is all it is.
            foreach (Level invented in result.InventedLevels)
                invented.Uid = SafIdentity.DerivedFrom("Level", invented.AbsoluteElevation);

            for (int i = 0; i < objects.Storeys.Count; i++)
            {
                Level level = result.LevelFor(storeyTickets[i]);
                ExcelStructuralStorey storey = objects.Storeys[i];
                level.Name = storey.Name;
                level.Uid = SafIdentity.UidOf(storey);
                index.Levels[storey.Name ?? level.Name ?? string.Empty] = level;
            }

            for (int i = 0; i < objects.Points.Count; i++)
            {
                Node node = result.NodeFor(pointTickets[i]);
                ExcelStructuralPointConnection point = objects.Points[i];
                if (node.Uid is null)
                    node.Uid = SafIdentity.UidOf(point);

                if (!string.IsNullOrEmpty(point.Name))
                    index.Nodes[point.Name!] = node;
            }

            if (objects.Points.Count > 0)
                log.Concept(SafLoss.DroppedObjectNames);
        }

        /// <summary>Indices sorted by one key, ties broken by the workbook's own row order.</summary>
        private static IEnumerable<int> Ordered(int count, Func<int, double> key)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++)
                indices[i] = i;

            return indices.OrderBy(key).ThenBy(i => i);
        }

        private static IEnumerable<int> OrderedPoints(List<ExcelStructuralPointConnection> points)
        {
            var indices = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
                indices[i] = i;

            return indices
                .OrderBy(i => SafUnits.Metres(points[i].X))
                .ThenBy(i => SafUnits.Metres(points[i].Y))
                .ThenBy(i => SafUnits.Metres(points[i].Z))
                .ThenBy(i => i);
        }

        // ---- Everything SAF has and FEMEX does not -----------------------------

        private static void ReportUnmapped(SafObjects objects, SafMessageLog log)
        {
            if (objects.ProxyElements.Count > 0)
                log.Concept(SafLoss.UnmappedProxyElement, $"{objects.ProxyElements.Count} in this file.");

            if (objects.RigidLinks.Count > 0)
                log.Concept(SafLoss.UnmappedRigidLink, $"{objects.RigidLinks.Count} in this file.");

            if (objects.RigidCrosses.Count > 0)
                log.Concept(SafLoss.UnmappedRigidCross, $"{objects.RigidCrosses.Count} in this file.");

            if (objects.RigidMembers.Count > 0)
                log.Concept(SafLoss.UnmappedRigidMember, $"{objects.RigidMembers.Count} in this file.");

            if (objects.InternalEdges.Count > 0)
                log.Concept(SafLoss.UnmappedInternalEdge, $"{objects.InternalEdges.Count} in this file.");

            if (objects.FreePointActions.Count > 0)
                log.Concept(SafLoss.UnmappedFreePointAction, $"{objects.FreePointActions.Count} in this file.");

            if (objects.FreeCurveActions.Count > 0)
                log.Concept(SafLoss.UnmappedFreeCurveAction, $"{objects.FreeCurveActions.Count} in this file.");

            if (objects.FreeSurfaceActions.Count > 0)
                log.Concept(SafLoss.UnmappedFreeSurfaceAction, $"{objects.FreeSurfaceActions.Count} in this file.");

            if (objects.SupportDeformations.Count > 0)
                log.Concept(SafLoss.UnmappedSupportDeformation, $"{objects.SupportDeformations.Count} in this file.");

            int results = objects.Results1D.Count + objects.Results2D.Count;
            if (results > 0)
                log.Concept(SafLoss.UnmappedResults, $"{results} result rows in this file.");

            if (objects.Unrecognised.Count > 0)
            {
                string listed = string.Join(", ", objects.Unrecognised
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key} x{pair.Value}"));

                log.Add(TransferMessage.ModelLoss(
                    LossCategory.Unmapped,
                    "This workbook contains SAF object types this adapter has no case for: " + listed +
                    ". They were read and not carried."));
            }
        }
    }

    /// <summary>
    /// Name-to-object lookups for one import. SAF keys everything by Name, so this
    /// is the whole of its reference resolution.
    /// </summary>
    internal sealed class SafIndex
    {
        public Dictionary<string, Material> Materials { get; } =
            new Dictionary<string, Material>(StringComparer.Ordinal);

        public Dictionary<string, Section> Sections { get; } =
            new Dictionary<string, Section>(StringComparer.Ordinal);

        /// <summary>Which material a cross-section names, since a FEMEX bar states both.</summary>
        public Dictionary<string, string> SectionMaterials { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public Dictionary<string, Level> Levels { get; } =
            new Dictionary<string, Level>(StringComparer.Ordinal);

        public Dictionary<string, Node> Nodes { get; } =
            new Dictionary<string, Node>(StringComparer.Ordinal);

        public Dictionary<string, Bar> Bars { get; } =
            new Dictionary<string, Bar>(StringComparer.Ordinal);

        public Dictionary<string, Plate> Plates { get; } =
            new Dictionary<string, Plate>(StringComparer.Ordinal);

        /// <summary>A region or an opening, and the plate it belongs to.</summary>
        public Dictionary<string, (Plate Plate, PlateRegion Region)> Regions { get; } =
            new Dictionary<string, (Plate, PlateRegion)>(StringComparer.Ordinal);

        public Dictionary<string, double> SurfacePropertyByThickness { get; } =
            new Dictionary<string, double>(StringComparer.Ordinal);

        public Dictionary<string, SurfaceProperty> SurfaceProperties { get; } =
            new Dictionary<string, SurfaceProperty>(StringComparer.Ordinal);

        public Dictionary<string, griffel_femex.Loads.LoadGroup> LoadGroups { get; } =
            new Dictionary<string, griffel_femex.Loads.LoadGroup>(StringComparer.Ordinal);

        public Dictionary<string, griffel_femex.Loads.LoadCase> LoadCases { get; } =
            new Dictionary<string, griffel_femex.Loads.LoadCase>(StringComparer.Ordinal);

        public Dictionary<string, griffel_femex.BoundaryConditions.Support> Supports { get; } =
            new Dictionary<string, griffel_femex.BoundaryConditions.Support>(StringComparer.Ordinal);
    }
}
