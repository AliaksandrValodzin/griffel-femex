using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Interop;
using griffel_femex.Loads;
using griffel_femex.Loads.Combinations;
using SAF.DataAccess.Models.Enums;
using SAF.DataAccess.Models.Interfaces;
using SAF.DataAccess.Models.Loads;
using SAF.DataAccess.Models.StructuralElements;
using SAF.DataAccess.Models.Subtypes;

namespace griffel_femex.Adapters.Saf
{
    public sealed partial class SafExporter
    {
        // ---- Supports and hinges -----------------------------------------------

        private static void WriteSupportsAndHinges(List<IExcelModuleObject> objects,
                                                   SafExportContext context)
        {
            foreach (Support support in context.Model.Supports)
            {
                string name = context.Name("StructuralPointSupport", null, FemexEntity.Support, support.Uid);
                context.Record(support.Uid, name);

                switch (support.Target)
                {
                    case SupportTarget.Point:
                        objects.Add(PointSupport(context, support, name));
                        break;
                    case SupportTarget.Linear when support.BarId.HasValue:
                        objects.Add(CurveSupport(context, support, name));
                        break;
                    case SupportTarget.Linear:
                        WriteEdgeSupport(objects, context, support, name);
                        break;
                    default:
                        context.Log.Concept(SafLoss.InventedPasternakSubsoil);
                        objects.Add(SurfaceSupport(context, support, name));
                        break;
                }
            }

            foreach (List<Hinge> pair in context.HingeGroups())
            {
                Hinge hinge = pair[0];
                string name = context.Name("RelConnectsStructuralMember", null, FemexEntity.Hinge, hinge.Uid);
                context.Record(hinge.Uid, name);

                if (hinge.Target == HingeTarget.Point)
                {
                    // A SAF hinge at Position = Both is two FEMEX hinges, one per
                    // end, and the second points at the first. Reading those pointers
                    // back on the way out is what stops a round trip turning one row
                    // into two — the same reversibility argument the bar chains make.
                    bool both = pair.Count > 1 &&
                                pair.All(h => h.ElementId == hinge.ElementId) &&
                                pair.Select(h => h.EndOrEdgeIndex).Distinct().Count() == pair.Count;

                    objects.Add(new ExcelRelConnectsStructuralMember
                    {
                        Id = hinge.Uid ?? Guid.Empty,
                        Name = name,
                        Member = context.BarName(hinge.ElementId),
                        Position = both
                            ? ExcelPosition.Both
                            : hinge.EndOrEdgeIndex == 0 ? ExcelPosition.Begin : ExcelPosition.End,
                        TranslationXType = SafEnums.ToSaf(hinge.Ux),
                        TranslationYType = SafEnums.ToSaf(hinge.Uy),
                        TranslationZType = SafEnums.ToSaf(hinge.Uz),
                        RotationXType = SafEnums.ToSaf(hinge.Rx),
                        RotationYType = SafEnums.ToSaf(hinge.Ry),
                        RotationZType = SafEnums.ToSaf(hinge.Rz),
                        TranslationXStiffness = Residual(context, hinge.Ux),
                        TranslationYStiffness = Residual(context, hinge.Uy),
                        TranslationZStiffness = Residual(context, hinge.Uz),
                        RotationXStiffness = RotationalResidual(context, hinge.Rx),
                        RotationYStiffness = RotationalResidual(context, hinge.Ry),
                        RotationZStiffness = RotationalResidual(context, hinge.Rz),
                    });
                }
                else
                {
                    objects.Add(new ExcelRelConnectsSurfaceEdge
                    {
                        Id = hinge.Uid ?? Guid.Empty,
                        Name = name,
                        Member2D = context.PlateName(hinge.ElementId),

                        // 0-based on this sheet, 1-based on StructuralEdgeConnection.
                        // The inconsistency is SAF's; FEMEX naming an edge by its two
                        // nodes is what makes writing either of them safe.
                        Edge = hinge.EndOrEdgeIndex,
                        CoordinateDefinition = ExcelCoordinateDefinition.Relative,
                        Origin = ExcelOrigin.FromStart,
                        StartPoint = 0.0,
                        EndPoint = 1.0,
                        TranslationXType = SafEnums.ToSaf(hinge.Ux),
                        TranslationYType = SafEnums.ToSaf(hinge.Uy),
                        TranslationZType = SafEnums.ToSaf(hinge.Uz),
                        RotationXType = SafEnums.ToSaf(hinge.Rx),
                        RotationYType = SafEnums.ToSaf(hinge.Ry),
                        TranslationXStiffness = LineResidual(context, hinge.Ux),
                        TranslationYStiffness = LineResidual(context, hinge.Uy),
                        TranslationZStiffness = LineResidual(context, hinge.Uz),
                        RotationXStiffness = LineRotationalResidual(context, hinge.Rx),
                        RotationYStiffness = LineRotationalResidual(context, hinge.Ry),
                        RotationZStiffness = LineRotationalResidual(context, hinge.Rz),
                        RotationZType = SafEnums.ToSaf(hinge.Rz),
                    });
                }
            }
        }

        private static ExcelStructuralPointSupport PointSupport(SafExportContext context,
                                                                Support support, string name)
        {
            var written = new ExcelStructuralPointSupport
            {
                Id = support.Uid ?? Guid.Empty,
                Name = name,
                Type = ExcelBoundaryNodeCondition.Custom,
                CoordinateSystem = ExcelCoordinateSystem.Global,
                TranslationXType = SafEnums.ToSaf(support.Ux),
                TranslationYType = SafEnums.ToSaf(support.Uy),
                TranslationZType = SafEnums.ToSaf(support.Uz),
                RotationXType = SafEnums.ToSaf(support.Rx),
                RotationYType = SafEnums.ToSaf(support.Ry),
                RotationZType = SafEnums.ToSaf(support.Rz),
                TranslationXStiffness = Stiffness(context, support.Ux),
                TranslationYStiffness = Stiffness(context, support.Uy),
                TranslationZStiffness = Stiffness(context, support.Uz),
                RotationXStiffness = RotationalStiffness(context, support.Rx),
                RotationYStiffness = RotationalStiffness(context, support.Ry),
                RotationZStiffness = RotationalStiffness(context, support.Rz),
            };

            if (support.BarId.HasValue)
            {
                written.BoundaryCondition = ExcelStructuralPointSupportType.OnBeam;
                written.Member = context.BarName(support.BarId.Value);
                written.CoordinateDefinition = ExcelCoordinateDefinition.Relative;
                written.Origin = ExcelOrigin.FromStart;
                written.PositionX = support.Position ?? 0.0;
            }
            else
            {
                written.BoundaryCondition = ExcelStructuralPointSupportType.InNode;
                written.Node = support.NodeIds.Count > 0 ? context.NodeName(support.NodeIds[0]) : null;
            }

            return written;
        }

        private static ExcelStructuralCurveConnection CurveSupport(SafExportContext context,
                                                                   Support support, string name)
        {
            return new ExcelStructuralCurveConnection
            {
                Id = support.Uid ?? Guid.Empty,
                Name = name,
                Member = context.BarName(support.BarId!.Value),
                Type = ExcelBoundaryNodeCondition.Custom,
                CoordinateSystem = ExcelCoordinateSystem.Global,
                CoordinateDefinition = ExcelCoordinateDefinition.Relative,
                Origin = ExcelOrigin.FromStart,
                StartPoint = support.Position ?? 0.0,
                EndPoint = support.EndPosition ?? 1.0,
                TranslationXType = Narrow(context, support, support.Ux, name),
                TranslationYType = Narrow(context, support, support.Uy, name),
                TranslationZType = Narrow(context, support, support.Uz, name),
                RotationXType = Narrow(context, support, support.Rx, name),
                RotationYType = Narrow(context, support, support.Ry, name),
                RotationZType = Narrow(context, support, support.Rz, name),

                // SAF makes the stiffness column mandatory wherever the type is
                // Flexible, and refuses the workbook without it. A line support
                // states its stiffness per unit length, so the units differ from the
                // point-support sheet even though the FEMEX property is the same one.
                TranslationXStiffness = LineStiffness(context, support.Ux),
                TranslationYStiffness = LineStiffness(context, support.Uy),
                TranslationZStiffness = LineStiffness(context, support.Uz),
                RotationXStiffness = LineRotationalStiffness(context, support.Rx),
                RotationYStiffness = LineRotationalStiffness(context, support.Ry),
                RotationZStiffness = LineRotationalStiffness(context, support.Rz),
            };
        }

        /// <summary>
        /// A FEMEX linear support on a plate edge names the edge by its two nodes.
        /// SAF names it by the surface and an index into the contour, so the pair has
        /// to be found again — and if it is not a contour edge of any plate, the
        /// support has nothing to attach to and is reported rather than written
        /// somewhere plausible.
        /// </summary>
        private static void WriteEdgeSupport(List<IExcelModuleObject> objects, SafExportContext context,
                                             Support support, string name)
        {
            foreach (Plate plate in context.Model.Plates)
            {
                int edge = EdgeIndexOf(plate.NodeIds, support.NodeIds);
                if (edge < 0)
                    continue;

                objects.Add(new ExcelStructuralEdgeConnection
                {
                    Id = support.Uid ?? Guid.Empty,
                    Name = name,
                    Type = ExcelBoundaryNodeCondition.Custom,
                    BoundaryCondition = ExcelStructuralEdgeConnectionType.OnEdge,
                    Member2D = context.PlateName(plate.Id),
                    Edge = edge + 1, // 1-based on this sheet.
                    CoordinateSystem = ExcelCoordinateSystem.Global,
                    CoordinateDefinition = ExcelCoordinateDefinition.Relative,
                    Origin = ExcelOrigin.FromStart,
                    StartPoint = 0.0,
                    EndPoint = 1.0,
                    TranslationXType = Narrow(context, support, support.Ux, name),
                    TranslationYType = Narrow(context, support, support.Uy, name),
                    TranslationZType = Narrow(context, support, support.Uz, name),
                    RotationXType = Narrow(context, support, support.Rx, name),
                    RotationYType = Narrow(context, support, support.Ry, name),
                    RotationZType = Narrow(context, support, support.Rz, name),
                    TranslationXStiffness = LineStiffness(context, support.Ux),
                    TranslationYStiffness = LineStiffness(context, support.Uy),
                    TranslationZStiffness = LineStiffness(context, support.Uz),
                    RotationXStiffness = LineRotationalStiffness(context, support.Rx),
                    RotationYStiffness = LineRotationalStiffness(context, support.Ry),
                    RotationZStiffness = LineRotationalStiffness(context, support.Rz),
                });

                return;
            }

            context.Log.Object(SafLoss.UnplaceableEdgeSupport,
                               new ObjectRef(FemexEntity.Support, support.Id, support.Uid), name);
        }

        private static int EdgeIndexOf(List<int> contour, List<int> nodes)
        {
            if (nodes.Count < 2 || contour.Count < 2)
                return -1;

            for (int i = 0; i < contour.Count; i++)
            {
                int a = contour[i];
                int b = contour[(i + 1) % contour.Count];
                if ((a == nodes[0] && b == nodes[1]) || (a == nodes[1] && b == nodes[0]))
                    return i;
            }

            return -1;
        }

        private static ExcelStructuralSurfaceConnection SurfaceSupport(SafExportContext context,
                                                                       Support support, string name)
        {
            return new ExcelStructuralSurfaceConnection
            {
                Id = support.Uid ?? Guid.Empty,
                Name = name,
                Member2D = support.PlateId.HasValue ? context.PlateName(support.PlateId.Value) : null,
                Member2DRegion = support.PlateId.HasValue && support.RegionId.HasValue &&
                                 context.RegionNames.TryGetValue(
                                     (support.PlateId.Value, support.RegionId.Value), out string? region)
                    ? region
                    : null,
                SubsoilName = name,
                SubsoilC1X = context.Units.SubgradeModulus(support.Ux.Stiffness ?? 0.0),
                SubsoilC1Y = context.Units.SubgradeModulus(support.Uy.Stiffness ?? 0.0),
                SubsoilStiffness = context.Units.SubgradeModulus(support.Uz.Stiffness ?? 0.0),
                SubsoilC1Z = support.Uz.Stiffness.HasValue ? "Flexible" : "Linear",

                // SAF requires the Pasternak shear terms; FEMEX has no property for
                // them. Zero is a statement the model did not make, and is declared.
                SubsoilC2X = context.Units.ForcePerLength(0.0),
                SubsoilC2Y = context.Units.ForcePerLength(0.0),
            };
        }

        private static UnitsNet.ForcePerLength? Stiffness(SafExportContext context, Restraint restraint)
        {
            return restraint.Stiffness.HasValue
                ? context.Units.ForcePerLength(restraint.Stiffness.Value)
                : (UnitsNet.ForcePerLength?)null;
        }

        private static UnitsNet.RotationalStiffness? RotationalStiffness(SafExportContext context,
                                                                         Restraint restraint)
        {
            return restraint.Stiffness.HasValue
                ? context.Units.RotationalStiffness(restraint.Stiffness.Value)
                : (UnitsNet.RotationalStiffness?)null;
        }

        /// <summary>
        /// A restraint for a sheet that accepts only five of SAF's eight constraint
        /// types, reporting the narrowing once per support that needs it.
        /// </summary>
        private static ExcelConstraintType Narrow(SafExportContext context, Support support,
                                                  Restraint restraint, string name)
        {
            ExcelConstraintType type = SafEnums.ToSafNarrow(restraint, out bool exact);
            if (!exact)
            {
                context.Log.Object(SafLoss.NarrowedLineRestraint,
                                   new ObjectRef(FemexEntity.Support, support.Id, support.Uid), name);
            }

            return type;
        }

        // The line and edge sheets state stiffness per unit length — pressure for a
        // translation, moment per radian per metre for a rotation — where the point
        // sheet states it per support. The FEMEX property is the same one; only the
        // sheet's units differ, which is one more reason Restraint.Stiffness having
        // no declared unit is a real gap rather than a tidiness complaint.
        private static UnitsNet.Pressure? LineStiffness(SafExportContext context, Restraint restraint)
        {
            return restraint.Stiffness.HasValue
                ? context.Units.Pressure(restraint.Stiffness.Value)
                : (UnitsNet.Pressure?)null;
        }

        private static UnitsNet.RotationalStiffnessPerLength? LineRotationalStiffness(
            SafExportContext context, Restraint restraint)
        {
            return restraint.Stiffness.HasValue
                ? context.Units.RotationalStiffnessPerLength(restraint.Stiffness.Value)
                : (UnitsNet.RotationalStiffnessPerLength?)null;
        }

        private static UnitsNet.Pressure? LineResidual(SafExportContext context, Release release)
        {
            return release.ResidualStiffness.HasValue
                ? context.Units.Pressure(release.ResidualStiffness.Value)
                : (UnitsNet.Pressure?)null;
        }

        private static UnitsNet.RotationalStiffnessPerLength? LineRotationalResidual(
            SafExportContext context, Release release)
        {
            return release.ResidualStiffness.HasValue
                ? context.Units.RotationalStiffnessPerLength(release.ResidualStiffness.Value)
                : (UnitsNet.RotationalStiffnessPerLength?)null;
        }

        private static UnitsNet.ForcePerLength? Residual(SafExportContext context, Release release)
        {
            return release.ResidualStiffness.HasValue
                ? context.Units.ForcePerLength(release.ResidualStiffness.Value)
                : (UnitsNet.ForcePerLength?)null;
        }

        private static UnitsNet.RotationalStiffness? RotationalResidual(SafExportContext context,
                                                                        Release release)
        {
            return release.ResidualStiffness.HasValue
                ? context.Units.RotationalStiffness(release.ResidualStiffness.Value)
                : (UnitsNet.RotationalStiffness?)null;
        }

        // ---- Load groups, cases, combinations ----------------------------------

        private static void WriteLoads(List<IExcelModuleObject> objects, SafExportContext context)
        {
            WriteLoadGroups(objects, context);
            WriteLoadCases(objects, context);
            WriteLoadCombinations(objects, context);
            WriteLoadObjects(objects, context);
        }

        /// <summary>
        /// SAF makes <c>StructuralLoadCase.Load group</c> mandatory. A FEMEX model
        /// that carries load groups passes them through; one that does not gets one
        /// group per load nature, invented — and the invented part is the relation,
        /// because the two published reference workbooks disagree about which
        /// relation wind and snow take, which is the proof that choosing is guessing.
        /// </summary>
        private static void WriteLoadGroups(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (LoadGroup group in context.Model.LoadGroups)
            {
                string name = context.Name("StructuralLoadGroup", group.Name, FemexEntity.LoadGroup,
                                           group.Uid);
                context.LoadGroupNames[group.Id] = name;
                context.Record(group.Uid, name);

                objects.Add(new ExcelStructuralLoadGroup
                {
                    Id = group.Uid ?? Guid.Empty,
                    Name = name,
                    LoadGroupType = SafEnums.ToSaf(group.Type),
                    Relation = SafEnums.ToSaf(group.Relation),
                });
            }

            var synthesised = new Dictionary<LoadNature, string>();
            foreach (LoadCase loadCase in context.Model.LoadCases)
            {
                if (loadCase.LoadGroupId.HasValue &&
                    context.LoadGroupNames.ContainsKey(loadCase.LoadGroupId.Value))
                {
                    continue;
                }

                if (synthesised.ContainsKey(loadCase.Nature))
                    continue;

                string name = context.Name("StructuralLoadGroup", "LG-" + loadCase.Nature, null, null);
                synthesised[loadCase.Nature] = name;

                objects.Add(new ExcelStructuralLoadGroup
                {
                    Name = name,
                    LoadGroupType = GroupTypeFor(loadCase.Nature),
                    Relation = ExcelRelation.Standard,
                    Load = CategoryFor(loadCase.Nature),
                });

                context.Log.Object(SafLoss.InventedLoadGroup,
                                   new ObjectRef(FemexEntity.LoadGroup), name,
                                   $"For load cases of nature {loadCase.Nature}.");
            }

            context.SynthesisedGroups = synthesised;
        }

        private static ExcelLoadGroupType GroupTypeFor(LoadNature nature)
        {
            switch (nature)
            {
                case LoadNature.Dead: return ExcelLoadGroupType.Permanent;
                case LoadNature.Accidental: return ExcelLoadGroupType.Accidental;
                case LoadNature.Seismic: return ExcelLoadGroupType.Seismic;
                default: return ExcelLoadGroupType.Variable;
            }
        }

        private static ExcelFlexibleEnum<ExcelLoad>? CategoryFor(LoadNature nature)
        {
            switch (nature)
            {
                case LoadNature.Wind: return new ExcelFlexibleEnum<ExcelLoad>(ExcelLoad.Wind);
                case LoadNature.Snow: return new ExcelFlexibleEnum<ExcelLoad>(ExcelLoad.Snow);
                case LoadNature.Live: return new ExcelFlexibleEnum<ExcelLoad>(ExcelLoad.Domestic);
                case LoadNature.Temperature: return new ExcelFlexibleEnum<ExcelLoad>(ExcelLoad.Temperature);
                default: return null;
            }
        }

        private static void WriteLoadCases(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (LoadCase loadCase in context.Model.LoadCases)
            {
                string name = context.Name("StructuralLoadCase", loadCase.Label, FemexEntity.LoadCase,
                                           loadCase.Uid);
                context.LoadCaseNames[loadCase.Number] = name;
                context.Record(loadCase.Uid, name);

                bool carriesSelfWeight = loadCase.SelfWeightFactor != 0.0;

                string? group = loadCase.LoadGroupId.HasValue &&
                                context.LoadGroupNames.TryGetValue(loadCase.LoadGroupId.Value,
                                                                   out string? named)
                    ? named
                    : context.SynthesisedGroups.TryGetValue(loadCase.Nature, out string? invented)
                        ? invented
                        : null;

                objects.Add(new ExcelStructuralLoadCase
                {
                    Id = loadCase.Uid ?? Guid.Empty,
                    Name = name,
                    ActionType = SafEnums.ToSafAction(loadCase.Nature),
                    LoadType = SafEnums.ToSafLoadType(loadCase.Nature, carriesSelfWeight),
                    LoadGroup = group,
                });

                // A self-weight factor other than 0 or 1 has no SAF home on the case:
                // self weight is generated by the receiver and scaled through each
                // combination's multiplier. Pushing it there is equivalent only where
                // the case appears in combinations at all.
                if (carriesSelfWeight && Math.Abs(loadCase.SelfWeightFactor - 1.0) > 1e-12)
                {
                    context.Log.Object(SafLoss.SelfWeightFactor,
                                       new ObjectRef(FemexEntity.LoadCase, loadCase.Number, loadCase.Uid),
                                       name,
                                       $"The factor is {loadCase.SelfWeightFactor.ToString("R", CultureInfo.InvariantCulture)}.");
                }
            }
        }

        private static void WriteLoadCombinations(List<IExcelModuleObject> objects,
                                                  SafExportContext context)
        {
            foreach (LoadCombination combination in context.Model.LoadCombinations)
            {
                string name = context.Name("StructuralLoadCombination", combination.Label,
                                           FemexEntity.LoadCombination, combination.Uid);
                context.Record(combination.Uid, name);

                ExcelLoadCaseCombinationType type = SafEnums.ToSaf(combination.CombinationType,
                                                                   out bool exact);
                if (!exact)
                {
                    context.Log.Object(SafLoss.DroppedCombinationType,
                                       new ObjectRef(FemexEntity.LoadCombination, combination.Number,
                                                     combination.Uid),
                                       name,
                                       $"The model states {combination.CombinationType}.");
                }

                var cases = new List<string>();
                var factors = new List<double?>();
                var multipliers = new List<double?>();

                foreach (LoadCombinationTerm term in combination.Terms)
                {
                    if (!context.LoadCaseNames.TryGetValue(term.LoadCaseNumber, out string? named))
                        continue;

                    cases.Add(named);
                    factors.Add(term.Factor * SelfWeightMultiplierFor(context, term.LoadCaseNumber));
                    multipliers.Add(1.0);
                }

                objects.Add(new ExcelStructuralLoadCombination
                {
                    Id = combination.Uid ?? Guid.Empty,
                    Name = name,
                    Category = SafEnums.ToSaf(combination.LimitState),
                    Type = type,
                    LoadCases = cases.ToArray(),
                    LoadFactors = factors.ToArray(),
                    LoadMultipliers = multipliers.ToArray(),
                });
            }
        }

        private static double SelfWeightMultiplierFor(SafExportContext context, int loadCaseNumber)
        {
            LoadCase? loadCase = context.Model.LoadCases.FirstOrDefault(c => c.Number == loadCaseNumber);
            if (loadCase is null || loadCase.SelfWeightFactor == 0.0)
                return 1.0;

            return loadCase.SelfWeightFactor;
        }

        // ---- Load objects ------------------------------------------------------

        private static void WriteLoadObjects(List<IExcelModuleObject> objects, SafExportContext context)
        {
            foreach (Load load in context.Model.Loads)
            {
                if (!context.LoadCaseNames.TryGetValue(load.LoadCaseNumber, out string? loadCase))
                    continue;

                switch (load)
                {
                    case PointLoad point:
                        WritePointLoad(objects, context, point, loadCase);
                        break;
                    case LinearLoad linear:
                        WriteLinearLoad(objects, context, linear, loadCase);
                        break;
                    case AreaLoad area:
                        WriteAreaLoad(objects, context, area, loadCase);
                        break;
                    case TemperatureLoad temperature:
                        WriteTemperatureLoad(objects, context, temperature, loadCase);
                        break;
                }
            }
        }

        /// <summary>
        /// One FEMEX point load becomes up to six SAF rows: SAF states one direction
        /// and one value per row, across two sheets. A re-import merges them back.
        /// </summary>
        private static void WritePointLoad(List<IExcelModuleObject> objects, SafExportContext context,
                                           PointLoad load, string loadCase)
        {
            string stem = context.Name("StructuralPointAction", load.Label, FemexEntity.Load, load.Uid);
            context.Record(load.Uid, stem);
            bool first = true;

            void Force(double value, ExcelActionDirection direction, string suffix)
            {
                if (Math.Abs(value) < 1e-12)
                    return;

                var written = new ExcelStructuralPointAction
                {
                    // Only the first row written for this load carries the uid.
                    // Six rows cannot all be the same object, and a uid repeated is
                    // worse than a uid minted.
                    Id = first ? load.Uid ?? Guid.Empty : Guid.Empty,
                    Name = first ? stem : context.Name("StructuralPointAction",
                                                       stem + suffix, null, null),
                    LoadCase = loadCase,
                    Direction = direction,
                    Value = context.Units.Force(value),
                    Type = new ExcelFlexibleEnum<ExcelActionLoadType>(ExcelActionLoadType.Standard),
                    CoordinateSystem = ExcelCoordinateSystem.Global,
                };

                Place(written, context, load);
                objects.Add(written);
                first = false;
            }

            void Moment(double value, ExcelMomentDirection direction, string suffix)
            {
                if (Math.Abs(value) < 1e-12)
                    return;

                var written = new ExcelStructuralPointMoment
                {
                    Id = first ? load.Uid ?? Guid.Empty : Guid.Empty,
                    Name = first ? context.Name("StructuralPointMoment", stem, null, null)
                                 : context.Name("StructuralPointMoment", stem + suffix, null, null),
                    LoadCase = loadCase,
                    Direction = direction,
                    Value = context.Units.Moment(value),
                    Type = new ExcelFlexibleEnum<ExcelActionLoadType>(ExcelActionLoadType.Standard),
                    CoordinateSystem = ExcelCoordinateSystem.Global,
                };

                Place(written, context, load);
                objects.Add(written);
                first = false;
            }

            Force(load.Fx, ExcelActionDirection.X, "-Fx");
            Force(load.Fy, ExcelActionDirection.Y, "-Fy");
            Force(load.Fz, ExcelActionDirection.Z, "-Fz");
            Moment(load.Mx, ExcelMomentDirection.Mx, "-Mx");
            Moment(load.My, ExcelMomentDirection.My, "-My");
            Moment(load.Mz, ExcelMomentDirection.Mz, "-Mz");
        }

        private static void Place(IExcelPointStructuralReference written, SafExportContext context,
                                  PointLoad load)
        {
            if (load.BarId.HasValue)
            {
                written.ForceAction = ExcelPointForceAction.OnBeam;
                written.ReferenceMember = context.BarName(load.BarId.Value);
                written.CoordinateDefinition = ExcelCoordinateDefinition.Relative;
                written.Origin = ExcelOrigin.FromStart;
                written.PositionX = load.Position ?? 0.0;
                written.Repeat = 1;
            }
            else
            {
                written.ForceAction = ExcelPointForceAction.InNode;
                written.ReferenceNode = context.NodeName(load.NodeNumber);
            }
        }

        private static void WriteLinearLoad(List<IExcelModuleObject> objects, SafExportContext context,
                                            LinearLoad load, string loadCase)
        {
            // Which sheet the load's name is reserved on depends on which rows it
            // will actually produce. Reserving it on the force sheet for a load that
            // only carries a moment renames the row that does get written, and a
            // round trip then reports a label change nothing did.
            bool magnitude = Math.Abs(load.MagnitudeStart) > 1e-12 ||
                             Math.Abs(load.MagnitudeEnd) > 1e-12;

            string stem = context.Name(magnitude ? "StructuralCurveAction" : "StructuralCurveMoment",
                                       load.Label, FemexEntity.Load, load.Uid);
            context.Record(load.Uid, stem);

            bool partial = load.StartPosition.HasValue || load.EndPosition.HasValue;
            double from = load.StartPosition ?? 0.0;
            double to = load.EndPosition ?? 1.0;

            // A FEMEX linear load names a bar, or two nodes. SAF needs to know which
            // of six things the run lies on, and refuses the row without a target, so
            // a load naming two nodes has to be matched back to a plate contour edge.
            string? member = load.BarId.HasValue ? context.BarName(load.BarId.Value) : null;
            string? surface = null;
            int? edge = null;

            if (member is null)
            {
                foreach (Plate plate in context.Model.Plates)
                {
                    int index = EdgeIndexOf(plate.NodeIds, new List<int> { load.StartNode, load.EndNode });
                    if (index < 0)
                        continue;

                    surface = context.PlateName(plate.Id);
                    edge = index + 1; // 1-based on the curve-action sheet.
                    break;
                }

                if (surface is null)
                {
                    context.Log.Object(SafLoss.UnplaceableLinearLoad,
                                       new ObjectRef(FemexEntity.Load, load.Id, load.Uid), stem);
                    return;
                }
            }

            ExcelCurveForceAction action = member is null
                ? ExcelCurveForceAction.OnEdge
                : ExcelCurveForceAction.OnBeam;

            // SAF states a load direction as one axis, or as a vector in columns the
            // SDK cannot write. So a FEMEX load stated by vector is reduced to the
            // axis it leans on hardest, and the reduction is reported rather than
            // passed off as the same load.
            ExcelActionDirection direction = SafEnums.ToSaf(load.Direction);
            if (load.Direction == LoadDirection.Vector)
            {
                direction = Dominant(load.Dx, load.Dy, load.Dz);
                context.Log.Object(SafLoss.FlattenedLoadDirection,
                                   new ObjectRef(FemexEntity.Load, load.Id, load.Uid), stem);
            }

            if (magnitude)
            {
                objects.Add(new ExcelStructuralCurveAction
                {
                    Id = load.Uid ?? Guid.Empty,
                    Name = stem,
                    LoadCase = loadCase,
                    ForceAction = action,
                    Member = member,
                    Member2D = surface,
                    Edge = edge,
                    Direction = direction,
                    CoordinateSystem = SafEnums.ToSaf(load.CoordinateSystem),
                    Location = load.Projected ? ExcelLocation.Projection : ExcelLocation.Length,
                    Distribution = Math.Abs(load.MagnitudeEnd - load.MagnitudeStart) > 1e-12
                        ? ExcelCurveDistribution.Trapezoidal
                        : ExcelCurveDistribution.Uniform,
                    Value = context.Units.ForcePerLength(load.MagnitudeStart),
                    Value2 = context.Units.ForcePerLength(load.MagnitudeEnd),
                    Type = new ExcelFlexibleEnum<ExcelActionLoadType>(ExcelActionLoadType.Standard),
                    CoordinateDefinition = ExcelCoordinateDefinition.Relative,
                    Origin = ExcelOrigin.FromStart,
                    Extent = partial ? ExcelExtentOfForceOnBeam.Span : ExcelExtentOfForceOnBeam.Full,
                    StartPoint = from,
                    EndPoint = to,
                });
            }

            if (Math.Abs(load.MomentStart) > 1e-12 || Math.Abs(load.MomentEnd) > 1e-12)
            {
                objects.Add(new ExcelStructuralCurveMoment
                {
                    Id = magnitude ? Guid.Empty : load.Uid ?? Guid.Empty,
                    Name = magnitude
                        ? context.Name("StructuralCurveMoment", stem + "-M", null, null)
                        : stem,
                    LoadCase = loadCase,
                    ForceAction = action,
                    Member = member,
                    Member2D = surface,
                    Edge = edge,
                    Direction = ExcelMomentDirection.My,
                    CoordinateSystem = SafEnums.ToSaf(load.CoordinateSystem),
                    Distribution = Math.Abs(load.MomentEnd - load.MomentStart) > 1e-12
                        ? ExcelCurveDistribution.Trapezoidal
                        : ExcelCurveDistribution.Uniform,
                    Value1 = context.Units.MomentPerLength(load.MomentStart),
                    Value2 = context.Units.MomentPerLength(load.MomentEnd),
                    Type = new ExcelFlexibleEnum<ExcelActionLoadType>(ExcelActionLoadType.Standard),
                    CoordinateDefinition = ExcelCoordinateDefinition.Relative,
                    Origin = ExcelOrigin.FromStart,
                    Extent = partial ? ExcelExtentOfForceOnBeam.Span : ExcelExtentOfForceOnBeam.Full,
                    StartPoint = from,
                    EndPoint = to,
                });
            }
        }

        /// <summary>The axis a direction vector leans on hardest.</summary>
        private static ExcelActionDirection Dominant(double? dx, double? dy, double? dz)
        {
            double x = Math.Abs(dx ?? 0.0);
            double y = Math.Abs(dy ?? 0.0);
            double z = Math.Abs(dz ?? 0.0);

            if (x >= y && x >= z)
                return ExcelActionDirection.X;

            return y >= z ? ExcelActionDirection.Y : ExcelActionDirection.Z;
        }

        private static void WriteAreaLoad(List<IExcelModuleObject> objects, SafExportContext context,
                                          AreaLoad load, string loadCase)
        {
            string name = context.Name("StructuralSurfaceAction", load.Label, FemexEntity.Load, load.Uid);
            context.Record(load.Uid, name);

            if (!load.PlateId.HasValue)
            {
                // A free area load is bounded by its own node sequence rather than
                // applied to a surface. SAF's surface action must name something.
                context.Log.Object(SafLoss.UnplaceableAreaLoad,
                                   new ObjectRef(FemexEntity.Load, load.Id, load.Uid), name);
                return;
            }

            ExcelActionDirection direction = SafEnums.ToSaf(load.Direction);
            if (load.Direction == LoadDirection.Vector)
            {
                // SAF's surface action has an axis and no direction vector, so the
                // vector is reduced to the axis it leans on hardest.
                direction = Dominant(load.Dx, load.Dy, load.Dz);
                context.Log.Object(SafLoss.FlattenedLoadDirection,
                                   new ObjectRef(FemexEntity.Load, load.Id, load.Uid), name);
            }

            string? region = load.PlateId.HasValue && load.RegionId.HasValue &&
                             context.RegionNames.TryGetValue((load.PlateId.Value, load.RegionId.Value),
                                                             out string? named)
                ? named
                : null;

            // A load panel is a StructuralSurfaceActionDistribution, not a
            // StructuralSurfaceMember, and SAF has a third reference column for it.
            // Naming a panel in the surface-member column is a dangling reference the
            // receiving program resolves to nothing.
            bool onPanel = region is null && load.PlateId.HasValue &&
                           context.Model.Plates.Any(p => p.Id == load.PlateId.Value &&
                                                         p.Kind == PlateRegionKind.LoadOnly);

            objects.Add(new ExcelStructuralSurfaceAction
            {
                Id = load.Uid ?? Guid.Empty,
                Name = name,
                LoadCase = loadCase,
                ForceAction = region is not null
                    ? ExcelSurfaceForceAction.On2DMemberRegion
                    : onPanel
                        ? ExcelSurfaceForceAction.On2DMemberDistribution
                        : ExcelSurfaceForceAction.On2DMember,
                Member2DReference = region is null && !onPanel && load.PlateId.HasValue
                    ? context.PlateName(load.PlateId.Value)
                    : null,
                Member2DDistributionReference = onPanel && load.PlateId.HasValue
                    ? context.PlateName(load.PlateId.Value)
                    : null,
                Member2DRegionReference = region,
                Direction = direction,
                CoordinateSystem = SafEnums.ToSaf(load.CoordinateSystem),
                Location = load.Projected ? ExcelLocation.Projection : ExcelLocation.Length,
                Value = context.Units.Pressure(load.Magnitude),
                Type = new ExcelFlexibleEnum<ExcelActionLoadType>(ExcelActionLoadType.Standard),
            });
        }

        private static void WriteTemperatureLoad(List<IExcelModuleObject> objects,
                                                 SafExportContext context, TemperatureLoad load,
                                                 string loadCase)
        {
            // SAF splits thermal actions across a member sheet and a surface sheet,
            // and allows the same name on both — the reference workbook has an LT1
            // on each. So the name is reserved on the sheet the load will land on,
            // not on whichever one is checked first.
            bool onBars = load.ElementIds.Any(context.BarNames.ContainsKey);
            string sheet = onBars ? "StructuralCurveActionThermal" : "StructuralSurfaceActionThermal";
            string stem = context.Name(sheet, load.Label, FemexEntity.Load, load.Uid);
            context.Record(load.Uid, stem);

            bool linear = load.GradientY.HasValue || load.GradientZ.HasValue;
            int index = 0;

            foreach (int elementId in load.ElementIds)
            {
                string name = index++ == 0
                    ? stem
                    : context.Name(sheet, stem + "-" + index.ToString(CultureInfo.InvariantCulture),
                                   null, null);

                if (context.BarNames.TryGetValue(elementId, out string? bar))
                {
                    var written = new ExcelStructuralCurveActionThermal
                    {
                        Id = index == 1 ? load.Uid ?? Guid.Empty : Guid.Empty,
                        Name = name,
                        LoadCase = loadCase,
                        ForceAction = ExcelCurveForceAction.OnBeam,
                        Member = bar,
                        Variation = linear
                            ? ExcelTemperatureVariation.Linear
                            : ExcelTemperatureVariation.Constant,
                        CoordinateDefinition = ExcelCoordinateDefinition.Relative,
                        Origin = ExcelOrigin.FromStart,
                        StartPoint = 0.0,
                        EndPoint = 1.0,
                    };

                    if (linear)
                    {
                        double gz = load.GradientZ ?? 0.0;
                        double gy = load.GradientY ?? 0.0;
                        written.TempT = context.Units.TemperatureStep(load.DeltaT + gz / 2.0);
                        written.TempB = context.Units.TemperatureStep(load.DeltaT - gz / 2.0);
                        written.TempR = context.Units.TemperatureStep(load.DeltaT + gy / 2.0);
                        written.TempL = context.Units.TemperatureStep(load.DeltaT - gy / 2.0);
                    }
                    else
                    {
                        written.DeltaT = context.Units.TemperatureStep(load.DeltaT);
                    }

                    objects.Add(written);
                }
                else if (context.PlateNames.TryGetValue(elementId, out string? plate))
                {
                    double gz = load.GradientZ ?? 0.0;
                    objects.Add(new ExcelStructuralSurfaceActionThermal
                    {
                        Id = index == 1 ? load.Uid ?? Guid.Empty : Guid.Empty,
                        Name = name,
                        LoadCase = loadCase,
                        Member2D = plate,
                        Variation = load.GradientZ.HasValue
                            ? ExcelTemperatureVariation.Linear
                            : ExcelTemperatureVariation.Constant,
                        Value1 = context.Units.TemperatureStep(load.DeltaT + gz / 2.0),
                        Value2 = load.GradientZ.HasValue
                            ? context.Units.TemperatureStep(load.DeltaT - gz / 2.0)
                            : (UnitsNet.Temperature?)null,
                    });

                    if (load.GradientY.HasValue)
                    {
                        context.Log.Object(SafLoss.DroppedSurfaceThermalGradient,
                                           new ObjectRef(FemexEntity.Load, load.Id, load.Uid), name);
                    }
                }
            }
        }
    }
}
