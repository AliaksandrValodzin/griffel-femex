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
using SAF.DataAccess.Models;
using SAF.DataAccess.Models.Enums;
using SAF.DataAccess.Models.Interfaces;
using SAF.DataAccess.Models.Libraries;
using SAF.DataAccess.Models.StructuralElements;
using SAF.DataAccess.Models.Subtypes;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// Writes a <see cref="FemexModel"/> as a SAF workbook.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>FEMEX cannot write a workbook SAF's own validator will accept without
    /// inventing something,</b> and this class is where every one of those
    /// inventions is made and declared. Decision 12 of the plan is the rule: an
    /// invented mandatory column is an <i>Invented</i> message, never a silent
    /// default — because from inside an adapter an invention does not feel like a
    /// loss, it feels like the workbook finally validating.
    /// </para>
    /// <para>
    /// Four mandatory columns FEMEX has no answer for — the system of units, the
    /// national code, the cross-section LCS and the SAF version — plus two
    /// conditional ones, the form code and the analysis eccentricity, and the load
    /// group every case must reference. The policy for each is P5 of
    /// <c>FEMEX_SAF_Corpus_Notes.md</c> §9, decided against two real workbooks rather
    /// than guessed.
    /// </para>
    /// <para>
    /// <b>The version written is 2.3.0.</b> Not 2.2.0, and not the caller's choice:
    /// the SDK stamps its own.
    /// </para>
    /// </remarks>
    public sealed partial class SafExporter : IFemexExporter
    {
        private readonly ISafGateway _gateway;

        public SafExporter(ISafGateway? gateway = null)
        {
            _gateway = gateway ?? new SafGateway();
        }

        public AdapterInfo Info { get; } = new AdapterInfo(
            "SAF",
            "Structural Analysis Format",
            SafGateway.WrittenSpecVersion,
            FemexModel.CurrentSchemaVersion);

        public AdapterCapabilities Capabilities { get; } = new SafImporter().Capabilities;

        public TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                    IProgress<TransferProgress>? progress,
                                                    CancellationToken cancellationToken)
        {
            if (model is null)
                return TransferResult<ExportReceipt>.Failed("No model was given to export.");

            if (request is null)
                return TransferResult<ExportReceipt>.Failed("No export request was given.");

            if (!(request is StreamExportRequest stream))
            {
                return TransferResult<ExportReceipt>.Failed(
                    "The SAF adapter writes a workbook to a stream. " +
                    $"{request.GetType().Name} is not a request it can serve.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var log = new SafMessageLog();

            TransferMessage? stale = Info.CompareSchema(model);
            if (stale is not null)
                log.Add(stale);

            // §5.4's rule, and it mutates the model on purpose: an exporter that
            // synthesises names without stamping uids is deriving a name from
            // nothing, and the same model would export differently twice.
            log.AddRange(NameSynthesis.Apply(model));

            var context = new SafExportContext(model, log);

            var objects = new List<IExcelModuleObject>();
            WriteGeneral(objects, context);
            WriteMaterials(objects, context);
            WriteSections(objects, context);
            WriteStoreysAndNodes(objects, context);

            cancellationToken.ThrowIfCancellationRequested();
            WriteMembers(objects, context);
            WriteSurfaces(objects, context);

            cancellationToken.ThrowIfCancellationRequested();
            WriteSupportsAndHinges(objects, context);
            WriteLoads(objects, context);

            ReportModelLevelLosses(context);

            var excel = new ExcelModel(objects, new ExcelValidationResult[0], context.SystemOfUnits);
            SafWriteResult written = _gateway.Write(stream.Destination, excel);

            log.AddSdkLog(written.Log);

            if (!written.Succeeded)
            {
                var failures = new List<TransferMessage>(log.Messages);
                failures.Add(TransferMessage.Failure(
                    written.Failure ?? "The SAF SDK refused to write this workbook."));

                foreach (string error in written.ValidationErrors)
                    failures.Add(TransferMessage.Failure("SAF validation: " + error));

                return TransferResult<ExportReceipt>.Failed(failures);
            }

            foreach (string error in written.ValidationErrors)
                log.Add(TransferMessage.Failure("SAF validation: " + error));

            progress?.Report(new TransferProgress(null, 1, 1, "SAF export complete."));

            return TransferResult<ExportReceipt>.Ok(
                new ExportReceipt(request.DestinationName, context.Handles), log.Messages);
        }

        // ---- General -----------------------------------------------------------

        private static void WriteGeneral(List<IExcelModuleObject> objects, SafExportContext context)
        {
            SafMessageLog log = context.Log;

            // P5, all four of the mandatory columns FEMEX cannot fill, each with a
            // policy and each declared.
            log.Concept(SafLoss.InventedSystemOfUnits,
                        $"Written as {context.SystemOfUnits}.");
            log.Concept(SafLoss.InventedNationalCode);
            log.Concept(SafLoss.InventedCrossSectionLcs);
            log.Concept(SafLoss.InventedSafVersion);
            log.Concept(SafLoss.InventedLocalFrame);

            if (context.Units.IsMixed)
            {
                // Metre with kip is a legal FEMEX statement and SAF has no flag for
                // it. Choosing quietly would rescale the whole file, because the SDK
                // reads this before it reads a sheet.
                log.Add(TransferMessage.Failure(
                    "This model states its lengths in one system and its forces in the other. SAF " +
                    "has a single Metric/Imperial flag and drives every conversion in the file from " +
                    $"it, so one half of the model was written as {context.SystemOfUnits} regardless. " +
                    "Restate the model in one system before relying on this workbook."));
            }
            else if (!context.Units.IsStated)
            {
                log.Add(TransferMessage.Failure(
                    "This model states no units. Metric SI was assumed. SAF reads its unit flag " +
                    "before any sheet and rescales the whole file from it, so an assumption here is " +
                    "not a labelling choice."));
            }

            objects.Add(new ExcelModelInformation
            {
                Name = context.Model.Metadata?.ProjectName ?? "FEMEX model",
                GlobalCoordinateSystem = ExcelGlobalCoordinateSystem.ZVertical,
                LocalCoordinateSystem = ExcelLocalCoordinateSystem.ZYX,
                SystemOfUnits = context.SystemOfUnits,
                NationalCode = ExcelNationalCode.EC_Standard_EN,
                SourceApplication = context.Model.Metadata?.Producer ?? "griffel-femex",
                SourceType = "Software",
                Description = "Exported from FEMEX " + (context.Model.SchemaVersion ?? "?"),
            });

            if (context.Model.Metadata is not null)
            {
                objects.Add(new ExcelProjectInformation
                {
                    Name = context.Model.Metadata.ProjectName ?? "FEMEX project",
                    Description = context.Model.Metadata.Producer,
                });
            }
        }

        private static void ReportModelLevelLosses(SafExportContext context)
        {
            SafMessageLog log = context.Log;
            FemexModel model = context.Model;

            if (model.Grids.Count > 0)
                log.Concept(SafLoss.DroppedGrids, $"{model.Grids.Count} grids in this model.");

            if (model.Mesh is not null)
                log.Concept(SafLoss.DroppedMesh, $"{model.Mesh.Faces.Count} faces in this model.");

            if (model.Levels.Any(level => level.IsGround || level.RelativeElevation != 0.0 ||
                                          (level.GridIds?.Count ?? 0) > 0))
            {
                log.Concept(SafLoss.DroppedLevelProperties);
            }

            if (model.Plates.Any(plate => plate.Regions.Any(region => region.Priority != 0)))
                log.Concept(SafLoss.DroppedRegionPriority);

            if (model.LoadCombinations.Any(combination => !combination.IncludeInDesignEnvelope))
                log.Concept(SafLoss.DroppedDesignEnvelopeFlag);

            if (model.SurfaceProperties.Count > 0)
                log.Concept(SafLoss.DissolvedSurfaceProperty);

            log.Concept(SafLoss.SynthesisedNames);
            log.Concept(SafLoss.InventedMemberEccentricity);
        }

        // ---- Materials and sections --------------------------------------------

        private static void WriteMaterials(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (Material material in context.Model.Materials)
            {
                string name = context.Name("StructuralMaterial", material.Name, FemexEntity.Material,
                                           material.Uid);
                context.MaterialNames[material.Id] = name;
                context.Record(material.Uid, name);

                objects.Add(new ExcelStructuralMaterial
                {
                    Id = material.Uid ?? Guid.Empty,
                    Name = name,
                    Type = SafEnums.ToSaf(material.Type),
                    Quality = material.Quality ?? name,
                    EModulus = context.Units.Pressure(material.ModulusOfElasticity),
                    GModulus = material.ShearModulus.HasValue
                        ? context.Units.Pressure(material.ShearModulus.Value)
                        : context.Units.Pressure(material.GetShearModulus()),
                    PoissonCoefficient = material.PoissonsRatio,
                    UnitMass = context.Units.Density(material.Density),
                    ThermalExpansion = material.ThermalExpansion.HasValue
                        ? context.Units.ThermalExpansion(material.ThermalExpansion.Value)
                        : (UnitsNet.CoefficientOfThermalExpansion?)null,
                });
            }
        }

        private static void WriteSections(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (Section section in context.Model.Sections)
            {
                string name = context.Name("StructuralCrossSection", section.Name, FemexEntity.Section,
                                           section.Uid);
                context.SectionNames[section.Id] = name;
                context.Record(section.Uid, name);

                ExcelProfileLibraryId? shape = SafSectionShapes.ShapeFor(section);
                int formCode = SafSectionShapes.FormCodeFor(section);

                if (formCode == 0)
                {
                    context.Log.Object(SafLoss.InventedFormCode,
                                       new ObjectRef(FemexEntity.Section, section.Id, section.Uid), name);
                }

                // SAF's own validator refuses a non-parametric section with no
                // Profile, and refuses any section with no Material. Neither is a
                // choice this adapter gets to skip: a FEMEX generic section names no
                // profile, and a FEMEX section names no material at all — the member
                // does. Both are filled and declared.
                bool manufactured = section.Catalogue?.Profile is not null;
                var written = new ExcelStructuralCrossSection
                {
                    Id = section.Uid ?? Guid.Empty,
                    Name = name,
                    Material = context.MaterialNameFor(section) ?? context.FallbackMaterialName(name),
                    CrossSectionType = manufactured
                        ? ExcelCrossSectionType.Manufactured
                        : shape.HasValue ? ExcelCrossSectionType.Parametric : ExcelCrossSectionType.General,
                    Shape = shape,
                    Profile = manufactured ? section.Catalogue!.Profile
                                           : shape.HasValue ? null : name,
                    FormCode = formCode,
                    Parameters = SafSectionShapes.ParametersFor(section, context.Units),
                };

                SectionProperties? properties = section.Properties;
                if (properties is not null)
                {
                    written.CrossSectionalPropertiesA = Optional(properties.Area, context.Units.Area);
                    written.CrossSectionalPropertiesIy = Optional(properties.Iy, context.Units.SecondMoment);
                    written.CrossSectionalPropertiesIz = Optional(properties.Iz, context.Units.SecondMoment);
                    written.CrossSectionalPropertiesIt = Optional(properties.J, context.Units.SecondMoment);
                    written.CrossSectionalPropertiesIw = Optional(properties.Iw, context.Units.WarpingConstant);
                    written.CrossSectionalPropertiesWply =
                        Optional(properties.Wply, context.Units.SectionModulus);
                    written.CrossSectionalPropertiesWplz =
                        Optional(properties.Wplz, context.Units.SectionModulus);

                    if (properties.ShearAreaY.HasValue || properties.ShearAreaZ.HasValue ||
                        properties.Wely.HasValue || properties.Welz.HasValue)
                    {
                        context.Log.Concept(SafLoss.DroppedSectionProperties);
                    }
                }

                objects.Add(written);
            }
        }

        private static T? Optional<T>(double? value, Func<double, T> convert) where T : struct
        {
            return value.HasValue ? convert(value.Value) : (T?)null;
        }

        // ---- Storeys and nodes -------------------------------------------------

        private static void WriteStoreysAndNodes(List<IExcelModuleObject> objects,
                                                 SafExportContext context)
        {
            foreach (Level level in context.Model.Levels)
            {
                string name = context.Name("StructuralStorey", level.Name, FemexEntity.Level, level.Uid);
                context.Record(level.Uid, name);

                objects.Add(new ExcelStructuralStorey
                {
                    Id = level.Uid ?? Guid.Empty,
                    Name = name,
                    HeightLevel = context.Units.Length(level.AbsoluteElevation),
                });
            }

            foreach (Node node in context.Model.Nodes)
            {
                // A FEMEX node has no name and a SAF one must have one. This is the
                // largest sheet the renaming touches, and the reason a round trip
                // through FEMEX renames most of the model.
                string name = context.Name("StructuralPointConnection", null, FemexEntity.Node, node.Uid);
                context.NodeNames[node.NodeNumber] = name;
                context.Record(node.Uid, name);

                objects.Add(new ExcelStructuralPointConnection
                {
                    Id = node.Uid ?? Guid.Empty,
                    Name = name,
                    X = context.Units.Length(node.X),
                    Y = context.Units.Length(node.Y),
                    Z = context.Units.Length(context.Geometry.ZOf(node)),
                });
            }
        }

        // ---- Members -----------------------------------------------------------

        private static void WriteMembers(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (List<Bar> chain in context.BarChains())
            {
                Bar head = chain[0];
                string name = context.Name("StructuralCurveMember", null, FemexEntity.Bar, head.Uid);
                context.BarNames[head.Id] = name;
                context.Record(head.Uid, name);

                var nodes = new List<string>();
                foreach (Bar bar in chain)
                {
                    if (nodes.Count == 0)
                        nodes.Add(context.NodeName(bar.StartNodeId));

                    nodes.Add(context.NodeName(bar.EndNodeId));
                    context.BarNames[bar.Id] = name;
                }

                var segments = new ExcelCurveShape[chain.Count];
                for (int i = 0; i < chain.Count; i++)
                    segments[i] = new ExcelCurveShape(ExcelCurveGeometricalShape.Line);

                objects.Add(new ExcelStructuralCurveMember
                {
                    Id = head.Uid ?? Guid.Empty,
                    Name = name,
                    Nodes = nodes.ToArray(),
                    Segments = segments,
                    CrossSection = context.SectionName(head.SectionId),
                    Behaviour = SafEnums.ToSaf(head.Behaviour),

                    // Mandatory and unstated in FEMEX where Alignment is null:
                    // Centre, reported once for the model rather than once per member.
                    SystemLine = SafEnums.ToSaf(head.Alignment),
                    LCSAdjustmentLCS = ExcelCurveLCSType.VectorY,
                    LCSAdjustmentX = context.Units.Length(0.0),
                    LCSAdjustmentY = context.Units.Length(1.0),
                    LCSAdjustmentZ = context.Units.Length(0.0),
                    LCSAdjustmentRotation = context.Units.Angle(head.RotationAngle),
                    Type = new ExcelFlexibleEnum<ExcelMember1DType>(ExcelMember1DType.General),
                    AnalysisEccentricityYBegin = Eccentricity(head, e => e.AnalysisYBegin, context),
                    AnalysisEccentricityYEnd = Eccentricity(head, e => e.AnalysisYEnd, context),
                    AnalysisEccentricityZBegin = Eccentricity(head, e => e.AnalysisZBegin, context),
                    AnalysisEccentricityZEnd = Eccentricity(head, e => e.AnalysisZEnd, context),
                    StructuralEccentricityYBegin = Eccentricity(head, e => e.StructuralYBegin, context),
                    StructuralEccentricityYEnd = Eccentricity(head, e => e.StructuralYEnd, context),
                    StructuralEccentricityZBegin = Eccentricity(head, e => e.StructuralZBegin, context),
                    StructuralEccentricityZEnd = Eccentricity(head, e => e.StructuralZEnd, context),
                    ArbitraryDefinition = WriteTaper(objects, context, head, name),
                });
            }
        }

        private static UnitsNet.Length Eccentricity(Bar bar, Func<BarEccentricity, double?> read,
                                                    SafExportContext context)
        {
            double? value = bar.Eccentricity is null ? null : read(bar.Eccentricity);
            return context.Units.Length(value ?? 0.0);
        }

        /// <summary>
        /// A FEMEX bar with an end section is a single linear taper, which is SAF's
        /// varying member with exactly one span and a comma-separated section pair.
        /// The separator is a comma here and a semicolon everywhere else in the
        /// format.
        /// </summary>
        private static string? WriteTaper(List<IExcelModuleObject> objects, SafExportContext context,
                                          Bar bar, string memberName)
        {
            if (bar.EndSectionId is null)
                return null;

            string name = context.Name("StructuralCurveMemberVarying", memberName + "-AD", null, null);
            objects.Add(new ExcelStructuralCurveMemberVarying
            {
                Name = name,
                CrossSections = new[]
                {
                    context.SectionName(bar.SectionId) + "," + context.SectionName(bar.EndSectionId.Value),
                },
                Spans = new double?[] { 1.0 },
                Alignments = new ExcelCurveAlignment?[] { SafEnums.ToSaf(bar.Alignment) },
            });

            return name;
        }

        // ---- Surfaces ----------------------------------------------------------

        private static void WriteSurfaces(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (Plate plate in context.Model.Plates)
            {
                if (plate.Kind == PlateRegionKind.LoadOnly)
                {
                    WriteLoadPanel(objects, context, plate);
                    continue;
                }

                string name = context.Name("StructuralSurfaceMember", plate.Name, FemexEntity.Plate,
                                           plate.Uid);
                context.PlateNames[plate.Id] = name;
                context.Record(plate.Uid, name);

                ExcelMember2DBehaviour behaviour = SafEnums.ToSaf(plate.Behaviour, out bool exact);
                if (!exact)
                {
                    context.Log.Object(SafLoss.DroppedPlateBehaviour,
                                       new ObjectRef(FemexEntity.Plate, plate.Id, plate.Uid), name);
                }

                objects.Add(new ExcelStructuralSurfaceMember
                {
                    Id = plate.Uid ?? Guid.Empty,
                    Name = name,
                    Nodes = plate.NodeIds.Select(context.NodeName).ToArray(),
                    EdgeShapes = Straight(plate.NodeIds.Count),
                    Type = new ExcelFlexibleEnum<ExcelMember2DType>(ExcelMember2DType.Plate),
                    Behaviour = behaviour,
                    Shape = ExcelMember2DShape.Flat,
                    Alignment = SafEnums.ToSaf(plate.Alignment),
                    AnalysisEccentricityZ = context.Units.Length(plate.SurfaceOffset),
                    StructuralEccentricityZ = context.Units.Length(plate.SurfaceOffset),
                    LCSAdjustmentLCS = ExcelMember2DLCSType.XByVector,
                    LCSAdjustmentX = context.Units.Length(1.0),
                    LCSAdjustmentY = context.Units.Length(0.0),
                    LCSAdjustmentZ = context.Units.Length(0.0),
                    LCSAdjustmentRotation = context.Units.Angle(plate.LocalAxisAngle),
                    Material = context.MaterialName(plate.MaterialId),
                    ThicknessType = ExcelSurfaceDistributionVarying.Constant,
                    Thickness = new ExcelMemberThickness
                    {
                        Distribution = ExcelSurfaceDistributionVarying.Constant,
                        ThicknessFirst = context.Units.Length(context.ThicknessOf(plate.SurfacePropertyId)),
                    },
                });

                foreach (PlateRegion region in plate.Regions)
                    WriteRegion(objects, context, plate, region, name);
            }
        }

        private static void WriteRegion(List<IExcelModuleObject> objects, SafExportContext context,
                                        Plate plate, PlateRegion region, string plateName)
        {
            string[] nodes = region.NodeIds.Select(context.NodeName).ToArray();

            if (region.Kind == PlateRegionKind.Opening)
            {
                string openingName = context.Name("StructuralSurfaceMemberOpening", region.Name, null,
                                                  region.Uid);
                context.Record(region.Uid, openingName);
                context.RegionNames[(plate.Id, region.Id)] = openingName;

                objects.Add(new ExcelStructuralSurfaceMemberOpening
                {
                    Id = region.Uid ?? Guid.Empty,
                    Name = openingName,
                    Member2D = plateName,
                    Nodes = nodes,
                    EdgeShapes = Straight(nodes.Length),
                });

                return;
            }

            string regionName = context.Name("StructuralSurfaceMemberRegion", region.Name, null, region.Uid);
            context.Record(region.Uid, regionName);
            context.RegionNames[(plate.Id, region.Id)] = regionName;

            objects.Add(new ExcelStructuralSurfaceMemberRegion
            {
                Id = region.Uid ?? Guid.Empty,
                Name = regionName,
                Member2D = plateName,
                Nodes = nodes,
                EdgeShapes = Straight(nodes.Length),
                Material = context.MaterialName(region.MaterialId ?? plate.MaterialId),
                Alignment = region.Alignment.HasValue
                    ? SafEnums.ToSaf(region.Alignment.Value)
                    : (ExcelMember2DAlignment?)null,
                EccentricityEz = context.Units.Length(region.SurfaceOffset ?? 0.0),
                Thickness = context.Units.Length(
                    context.ThicknessOf(region.SurfacePropertyId ?? plate.SurfacePropertyId)),
            });
        }

        private static void WriteLoadPanel(List<IExcelModuleObject> objects, SafExportContext context,
                                           Plate plate)
        {
            string name = context.Name("StructuralSurfaceActionDistribution", plate.Name,
                                       FemexEntity.Plate, plate.Uid);
            context.PlateNames[plate.Id] = name;
            context.Record(plate.Uid, name);

            objects.Add(new SAF.DataAccess.Models.Loads.ExcelStructuralSurfaceActionDistribution
            {
                Id = plate.Uid ?? Guid.Empty,
                Name = name,
                Layer = "Load panel",
                Nodes = plate.NodeIds.Select(context.NodeName).ToArray(),
                Edges = Straight(plate.NodeIds.Count),
                Type = ExcelSurfaceActionDistributionType.Nodes,
                DistributionTo = SafEnums.ToSaf(plate.Distribution?.Spanning ?? SurfaceLoadSpanning.TwoWay),
                LCSAdjustmentLCS = ExcelMember2DLCSType.XByVector,
                LCSAdjustmentX = context.Units.Length(1.0),
                LCSAdjustmentY = context.Units.Length(0.0),
                LCSAdjustmentZ = context.Units.Length(0.0),
                LCSAdjustmentRotation = context.Units.Angle(plate.Distribution?.RotationAngle ?? 0.0),
                Members1D = plate.Distribution?.BarIds?.Select(context.BarName).ToArray(),
            });
        }

        private static ExcelCurveShape[] Straight(int count)
        {
            var shapes = new ExcelCurveShape[Math.Max(count, 0)];
            for (int i = 0; i < shapes.Length; i++)
                shapes[i] = new ExcelCurveShape(ExcelCurveGeometricalShape.Line);

            return shapes;
        }
    }
}
