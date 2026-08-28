using System;
using System.Collections.Generic;
using System.Linq;
using griffel_femex.BoundaryConditions;
using griffel_femex.Geometry;
using griffel_femex.Interop;
using SAF.DataAccess.Models.Enums;
using SAF.DataAccess.Models.StructuralElements;

namespace griffel_femex.Adapters.Saf
{
    public sealed partial class SafImporter
    {
        private static void ImportSupportsAndHinges(FemexModel model, SafObjects objects,
                                                    SafIndex index, SafMessageLog log)
        {
            var geometry = new SafGeometry(model);

            ImportPointSupports(model, objects, index, log, geometry);
            ImportCurveSupports(model, objects, index, log, geometry);
            ImportEdgeSupports(model, objects, index, log);
            ImportSurfaceSupports(model, objects, index, log);
            ImportMemberHinges(model, objects, index, log);
            ImportEdgeHinges(model, objects, index, log);
        }

        // ---- Supports ----------------------------------------------------------

        private static void ImportPointSupports(FemexModel model, SafObjects objects, SafIndex index,
                                                SafMessageLog log, SafGeometry geometry)
        {
            foreach (ExcelStructuralPointSupport source in objects.PointSupports)
            {
                var support = new Support
                {
                    Id = NextSupportId(model),
                    Uid = SafIdentity.UidOf(source),
                    Target = SupportTarget.Point,
                };

                var reference = new ObjectRef(FemexEntity.Support, support.Id, support.Uid);

                if (source.BoundaryCondition == ExcelStructuralPointSupportType.OnBeam)
                {
                    // 2.2's on-beam support. The position is data about the support,
                    // not a fact about the topology: storing it leaves the model's
                    // node table alone, where minting a node would change the element
                    // count of the thing being reported on.
                    Bar? bar = Lookup(index.Bars, source.Member);
                    if (bar is null)
                        continue;

                    support.BarId = bar.Id;
                    support.Position = SafPosition.Relative(
                        source.PositionX, source.CoordinateDefinition, source.Origin,
                        geometry.LengthOf(bar), out bool chorded);

                    if (chorded)
                        log.Object(SafLoss.ChordedPosition, reference, source.Name);
                }
                else
                {
                    Node? node = Lookup(index.Nodes, source.Node);
                    if (node is null)
                        continue;

                    support.NodeIds.Add(node.NodeNumber);
                }

                ApplyRestraints(support, log, reference, source.Name,
                                source.TranslationXType, source.TranslationYType, source.TranslationZType,
                                source.RotationXType, source.RotationYType, source.RotationZType,
                                SafUnits.NewtonsPerMetre(source.TranslationXStiffness),
                                SafUnits.NewtonsPerMetre(source.TranslationYStiffness),
                                SafUnits.NewtonsPerMetre(source.TranslationZStiffness),
                                SafUnits.NewtonMetresPerRadian(source.RotationXStiffness),
                                SafUnits.NewtonMetresPerRadian(source.RotationYStiffness),
                                SafUnits.NewtonMetresPerRadian(source.RotationZStiffness));

                model.Supports.Add(support);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Supports[source.Name!] = support;
            }
        }

        private static void ImportCurveSupports(FemexModel model, SafObjects objects, SafIndex index,
                                                SafMessageLog log, SafGeometry geometry)
        {
            foreach (ExcelStructuralCurveConnection source in objects.CurveSupports)
            {
                Bar? bar = Lookup(index.Bars, source.Member) ?? Lookup(index.Bars, source.MemberRib);
                if (bar is null)
                    continue;

                var support = new Support
                {
                    Id = NextSupportId(model),
                    Uid = SafIdentity.UidOf(source),
                    Target = SupportTarget.Linear,
                    BarId = bar.Id,
                };

                var reference = new ObjectRef(FemexEntity.Support, support.Id, support.Uid);
                double length = geometry.LengthOf(bar);

                support.Position = SafPosition.Relative(
                    source.StartPoint, source.CoordinateDefinition, source.Origin, length, out bool a);
                support.EndPosition = SafPosition.Relative(
                    source.EndPoint, source.CoordinateDefinition, source.Origin, length, out bool b);

                // From end means both stations are measured backwards, so the pair
                // arrives reversed. A support from 0.2 m to 1.5 m from the end runs
                // from (L-1.5) to (L-0.2) from the start.
                if (source.Origin == ExcelOrigin.FromEnd &&
                    support.Position.HasValue && support.EndPosition.HasValue &&
                    support.Position > support.EndPosition)
                {
                    (support.Position, support.EndPosition) = (support.EndPosition, support.Position);
                }

                if (a || b)
                    log.Object(SafLoss.ChordedPosition, reference, source.Name);

                ApplyRestraints(support, log, reference, source.Name,
                                source.TranslationXType, source.TranslationYType, source.TranslationZType,
                                source.RotationXType, source.RotationYType, source.RotationZType,
                                SafUnits.Pascals(source.TranslationXStiffness),
                                SafUnits.Pascals(source.TranslationYStiffness),
                                SafUnits.Pascals(source.TranslationZStiffness),
                                SafUnits.NewtonMetresPerRadianPerMetre(source.RotationXStiffness),
                                SafUnits.NewtonMetresPerRadianPerMetre(source.RotationYStiffness),
                                SafUnits.NewtonMetresPerRadianPerMetre(source.RotationZStiffness));

                model.Supports.Add(support);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Supports[source.Name!] = support;
            }
        }

        private static void ImportEdgeSupports(FemexModel model, SafObjects objects, SafIndex index,
                                               SafMessageLog log)
        {
            foreach (ExcelStructuralEdgeConnection source in objects.EdgeSupports)
            {
                if (source.BoundaryCondition == ExcelStructuralEdgeConnectionType.OnInternalEdge)
                    continue; // Reported once, per concept, as UnmappedInternalEdge.

                List<int>? edge = EdgeNodes(index, source.Member2D, source.Member2DRegion,
                                            source.Member2DOpening, source.Edge, oneBased: true);
                if (edge is null)
                    continue;

                var support = new Support
                {
                    Id = NextSupportId(model),
                    Uid = SafIdentity.UidOf(source),
                    Target = SupportTarget.Linear,
                    NodeIds = edge,
                };

                var reference = new ObjectRef(FemexEntity.Support, support.Id, support.Uid);

                ApplyRestraints(support, log, reference, source.Name,
                                source.TranslationXType, source.TranslationYType, source.TranslationZType,
                                source.RotationXType, source.RotationYType, source.RotationZType,
                                SafUnits.Pascals(source.TranslationXStiffness),
                                SafUnits.Pascals(source.TranslationYStiffness),
                                SafUnits.Pascals(source.TranslationZStiffness),
                                SafUnits.NewtonMetresPerRadianPerMetre(source.RotationXStiffness),
                                SafUnits.NewtonMetresPerRadianPerMetre(source.RotationYStiffness),
                                SafUnits.NewtonMetresPerRadianPerMetre(source.RotationZStiffness));

                model.Supports.Add(support);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Supports[source.Name!] = support;
            }
        }

        private static void ImportSurfaceSupports(FemexModel model, SafObjects objects, SafIndex index,
                                                  SafMessageLog log)
        {
            foreach (ExcelStructuralSurfaceConnection source in objects.SurfaceSupports)
            {
                Plate? plate = Lookup(index.Plates, source.Member2D);
                if (plate is null)
                    continue;

                var support = new Support
                {
                    Id = NextSupportId(model),
                    Uid = SafIdentity.UidOf(source),
                    Target = SupportTarget.Area,
                    PlateId = plate.Id,
                };

                if (source.Member2DRegion is not null &&
                    index.Regions.TryGetValue(source.Member2DRegion, out var region))
                {
                    support.RegionId = region.Region.Id;
                }

                // Winkler C1 lands in a stiffness whose units FEMEX does not define;
                // the value is carried in SI, N/m3, and said so here rather than
                // silently rescaled. The Pasternak shear layer has no home at all.
                Fix(support.Ux, SafUnits.NewtonsPerCubicMetre(source.SubsoilC1X));
                Fix(support.Uy, SafUnits.NewtonsPerCubicMetre(source.SubsoilC1Y));
                Fix(support.Uz, SafUnits.NewtonsPerCubicMetre(source.SubsoilStiffness));

                if (source.SubsoilC2X.HasValue || source.SubsoilC2Y.HasValue)
                {
                    log.Object(SafLoss.DroppedPasternakSubsoil,
                               new ObjectRef(FemexEntity.Support, support.Id, support.Uid), source.Name,
                               $"Subsoil \"{source.SubsoilName}\".");
                }

                model.Supports.Add(support);
                if (!string.IsNullOrEmpty(source.Name))
                    index.Supports[source.Name!] = support;
            }
        }

        private static void Fix(Restraint restraint, double stiffness)
        {
            if (stiffness == 0.0)
                return;

            restraint.Fixed = false;
            restraint.Stiffness = stiffness;
        }

        // ---- Hinges ------------------------------------------------------------

        private static void ImportMemberHinges(FemexModel model, SafObjects objects, SafIndex index,
                                               SafMessageLog log)
        {
            foreach (ExcelRelConnectsStructuralMember source in objects.MemberHinges)
            {
                Bar? bar = Lookup(index.Bars, source.Member);
                if (bar is null)
                    continue;

                // Position = Both becomes two hinges, which is the one place a SAF
                // object legitimately becomes two FEMEX objects: the two ends are
                // independently released and FEMEX states a release per end.
                var ends = new List<int>();
                switch (source.Position)
                {
                    case ExcelPosition.Begin: ends.Add(0); break;
                    case ExcelPosition.End: ends.Add(1); break;
                    default: ends.Add(0); ends.Add(1); break;
                }

                for (int i = 0; i < ends.Count; i++)
                {
                    int end = ends[i];
                    var hinge = new Hinge
                    {
                        Id = NextHingeId(model),
                        // The first hinge keeps the SAF row's uid; a second points
                        // back at it. Giving both the same uid would break identity,
                        // pointing the first at itself would claim it was derived
                        // from itself, and pointing at a uid no object carries would
                        // dangle.
                        Uid = i == 0 ? SafIdentity.UidOf(source) : DerivedUid(SafIdentity.UidOf(source), i),
                        ParentUid = i == 0 ? null : SafIdentity.UidOf(source),
                        Target = HingeTarget.Point,
                        ElementId = bar.Id,
                        EndOrEdgeIndex = end,
                    };

                    hinge.NodeIds.Add(end == 0 ? bar.StartNodeId : bar.EndNodeId);

                    var reference = new ObjectRef(FemexEntity.Hinge, hinge.Id, hinge.Uid);
                    ApplyReleases(hinge, log, reference, source.Name,
                                  source.TranslationXType, source.TranslationYType, source.TranslationZType,
                                  source.RotationXType, source.RotationYType, source.RotationZType,
                                  SafUnits.NewtonsPerMetre(source.TranslationXStiffness),
                                  SafUnits.NewtonsPerMetre(source.TranslationYStiffness),
                                  SafUnits.NewtonsPerMetre(source.TranslationZStiffness),
                                  SafUnits.NewtonMetresPerRadian(source.RotationXStiffness),
                                  SafUnits.NewtonMetresPerRadian(source.RotationYStiffness),
                                  SafUnits.NewtonMetresPerRadian(source.RotationZStiffness));

                    model.Hinges.Add(hinge);
                }
            }
        }

        private static void ImportEdgeHinges(FemexModel model, SafObjects objects, SafIndex index,
                                             SafMessageLog log)
        {
            foreach (ExcelRelConnectsSurfaceEdge source in objects.EdgeHinges)
            {
                // SAF's own edge indexing is inconsistent: this sheet is 0-based
                // where StructuralEdgeConnection is 1-based. FEMEX naming an edge by
                // its two nodes is what makes that survivable.
                List<int>? edge = EdgeNodes(index, source.Member2D, null, null, source.Edge,
                                            oneBased: false);
                if (edge is null || edge.Count < 2)
                    continue;

                Plate? plate = Lookup(index.Plates, source.Member2D);
                var hinge = new Hinge
                {
                    Id = NextHingeId(model),
                    Uid = SafIdentity.UidOf(source),
                    Target = HingeTarget.Linear,
                    ElementId = plate?.Id ?? 0,
                    EndOrEdgeIndex = source.Edge ?? 0,
                    EdgeStartNodeId = edge[0],
                    EdgeEndNodeId = edge[1],
                    NodeIds = edge,
                };

                var reference = new ObjectRef(FemexEntity.Hinge, hinge.Id, hinge.Uid);

                if (SafPosition.IsPartial(source.StartPoint, source.EndPoint, source.CoordinateDefinition))
                    log.Object(SafLoss.DroppedPartialEdgeHinge, reference, source.Name);

                ApplyReleases(hinge, log, reference, source.Name,
                              source.TranslationXType, source.TranslationYType, source.TranslationZType,
                              source.RotationXType, source.RotationYType, source.RotationZType,
                              SafUnits.Pascals(source.TranslationXStiffness),
                              SafUnits.Pascals(source.TranslationYStiffness),
                              SafUnits.Pascals(source.TranslationZStiffness),
                              SafUnits.NewtonMetresPerRadianPerMetre(source.RotationXStiffness),
                              SafUnits.NewtonMetresPerRadianPerMetre(source.RotationYStiffness),
                              SafUnits.NewtonMetresPerRadianPerMetre(source.RotationZStiffness));

                model.Hinges.Add(hinge);
            }
        }

        // ---- Shared ------------------------------------------------------------

        private static void ApplyRestraints(Support support, SafMessageLog log, ObjectRef reference,
                                            string? handle,
                                            ExcelConstraintType? tx, ExcelConstraintType? ty,
                                            ExcelConstraintType? tz, ExcelConstraintType? rx,
                                            ExcelConstraintType? ry, ExcelConstraintType? rz,
                                            double kx, double ky, double kz,
                                            double krx, double kry, double krz)
        {
            bool nonLinear = false;
            nonLinear |= !Apply(support.Ux, tx, kx);
            nonLinear |= !Apply(support.Uy, ty, ky);
            nonLinear |= !Apply(support.Uz, tz, kz);
            nonLinear |= !Apply(support.Rx, rx, krx);
            nonLinear |= !Apply(support.Ry, ry, kry);
            nonLinear |= !Apply(support.Rz, rz, krz);

            if (nonLinear)
                log.Object(SafLoss.DroppedNonLinearRestraint, reference, handle);
        }

        private static bool Apply(Restraint restraint, ExcelConstraintType? type, double stiffness)
        {
            if (!SafEnums.TryToFemex(type, out bool isFixed, out bool usesStiffness,
                                     out griffel_femex.BoundaryConditions.RestraintSense? sense))
            {
                return false;
            }

            restraint.Fixed = isFixed;
            restraint.Sense = sense;
            if (usesStiffness)
            {
                restraint.Fixed = false;
                restraint.Stiffness = stiffness;
            }

            return true;
        }

        private static void ApplyReleases(Hinge hinge, SafMessageLog log, ObjectRef reference,
                                          string? handle,
                                          ExcelConstraintType? tx, ExcelConstraintType? ty,
                                          ExcelConstraintType? tz, ExcelConstraintType? rx,
                                          ExcelConstraintType? ry, ExcelConstraintType? rz,
                                          double kx, double ky, double kz,
                                          double krx, double kry, double krz)
        {
            SafEnums.ToFemex(tx, kx, hinge.Ux);
            SafEnums.ToFemex(ty, ky, hinge.Uy);
            SafEnums.ToFemex(tz, kz, hinge.Uz);
            SafEnums.ToFemex(rx, krx, hinge.Rx);
            SafEnums.ToFemex(ry, kry, hinge.Ry);
            SafEnums.ToFemex(rz, krz, hinge.Rz);

            if (IsNonLinear(tx) || IsNonLinear(ty) || IsNonLinear(tz) ||
                IsNonLinear(rx) || IsNonLinear(ry) || IsNonLinear(rz))
            {
                log.Object(SafLoss.DroppedNonLinearRestraint, reference, handle);
            }
        }

        private static bool IsNonLinear(ExcelConstraintType? type)
        {
            return type == ExcelConstraintType.NonLinear ||
                   type == ExcelConstraintType.CompressionOnly ||
                   type == ExcelConstraintType.TensionOnly;
        }

        /// <summary>
        /// The two nodes bounding one contour edge. SAF names the edge by an index
        /// whose base depends on the sheet; FEMEX names it by its two nodes, which is
        /// the more robust of the two and the reason the inconsistency is survivable.
        /// </summary>
        private static List<int>? EdgeNodes(SafIndex index, string? surface, string? region,
                                            string? opening, int? edge, bool oneBased)
        {
            List<int>? contour = null;

            if (region is not null && index.Regions.TryGetValue(region, out var namedRegion))
                contour = namedRegion.Region.NodeIds;
            else if (opening is not null && index.Regions.TryGetValue(opening, out var namedOpening))
                contour = namedOpening.Region.NodeIds;
            else if (surface is not null && index.Plates.TryGetValue(surface, out Plate? plate))
                contour = plate.NodeIds;

            if (contour is null || contour.Count < 2)
                return null;

            int i = (edge ?? (oneBased ? 1 : 0)) - (oneBased ? 1 : 0);
            if (i < 0 || i >= contour.Count)
                return null;

            return new List<int> { contour[i], contour[(i + 1) % contour.Count] };
        }

        private static int NextSupportId(FemexModel model)
        {
            return model.Supports.Count == 0 ? 1 : model.Supports.Max(s => s.Id) + 1;
        }

        private static int NextHingeId(FemexModel model)
        {
            return model.Hinges.Count == 0 ? 1 : model.Hinges.Max(h => h.Id) + 1;
        }
    }
}
