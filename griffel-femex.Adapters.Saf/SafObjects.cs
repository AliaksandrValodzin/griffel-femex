using System;
using System.Collections.Generic;
using SAF.DataAccess.Models;
using SAF.DataAccess.Models.Interfaces;
using SAF.DataAccess.Models.Libraries;
using SAF.DataAccess.Models.Loads;
using SAF.DataAccess.Models.Results;
using SAF.DataAccess.Models.StructuralElements;
using SAF.DataAccess.Models.Subtypes.CrossSectionShape;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// The SDK returns <c>ExcelModel.Objects</c> as a flat heterogeneous bag. This
    /// is the one place that sorts it.
    /// </summary>
    /// <remarks>
    /// Grouping once, up front, rather than filtering the bag at each of thirty call
    /// sites, is what makes the importer readable and what makes the two-phase
    /// synthesis rule of §6.2 achievable: you cannot collect every candidate
    /// coordinate before creating any node if you are streaming a single pass over
    /// an untyped list.
    ///
    /// A type this class does not know about is counted rather than dropped, so a
    /// later SAF version adding a sheet shows up as a number in the report instead
    /// of as silence.
    /// </remarks>
    public sealed class SafObjects
    {
        private readonly Dictionary<string, int> _unrecognised = new Dictionary<string, int>();

        private SafObjects()
        {
        }

        public ExcelModelInformation? ModelInformation { get; private set; }

        public ExcelProjectInformation? ProjectInformation { get; private set; }

        public List<ExcelStructuralMaterial> Materials { get; } = new List<ExcelStructuralMaterial>();

        public List<ExcelStructuralCrossSection> CrossSections { get; } = new List<ExcelStructuralCrossSection>();

        public List<ExcelCompositeShapeDef> CompositeShapes { get; } = new List<ExcelCompositeShapeDef>();

        public List<ExcelStructuralStorey> Storeys { get; } = new List<ExcelStructuralStorey>();

        public List<ExcelStructuralPointConnection> Points { get; } = new List<ExcelStructuralPointConnection>();

        public List<ExcelStructuralCurveMember> Members { get; } = new List<ExcelStructuralCurveMember>();

        public List<ExcelStructuralCurveMemberVarying> Varyings { get; } = new List<ExcelStructuralCurveMemberVarying>();

        public List<ExcelStructuralCurveMemberRib> Ribs { get; } = new List<ExcelStructuralCurveMemberRib>();

        public List<ExcelStructuralCurveEdge> InternalEdges { get; } = new List<ExcelStructuralCurveEdge>();

        public List<ExcelStructuralSurfaceMember> Surfaces { get; } = new List<ExcelStructuralSurfaceMember>();

        public List<ExcelStructuralSurfaceMemberOpening> Openings { get; } = new List<ExcelStructuralSurfaceMemberOpening>();

        public List<ExcelStructuralSurfaceMemberRegion> Regions { get; } = new List<ExcelStructuralSurfaceMemberRegion>();

        public List<ExcelStructuralPointSupport> PointSupports { get; } = new List<ExcelStructuralPointSupport>();

        public List<ExcelStructuralCurveConnection> CurveSupports { get; } = new List<ExcelStructuralCurveConnection>();

        public List<ExcelStructuralEdgeConnection> EdgeSupports { get; } = new List<ExcelStructuralEdgeConnection>();

        public List<ExcelStructuralSurfaceConnection> SurfaceSupports { get; } = new List<ExcelStructuralSurfaceConnection>();

        public List<ExcelRelConnectsStructuralMember> MemberHinges { get; } = new List<ExcelRelConnectsStructuralMember>();

        public List<ExcelRelConnectsSurfaceEdge> EdgeHinges { get; } = new List<ExcelRelConnectsSurfaceEdge>();

        public List<ExcelRelConnectsRigidLink> RigidLinks { get; } = new List<ExcelRelConnectsRigidLink>();

        public List<ExcelRelConnectsRigidCross> RigidCrosses { get; } = new List<ExcelRelConnectsRigidCross>();

        public List<ExcelRelConnectsRigidMember> RigidMembers { get; } = new List<ExcelRelConnectsRigidMember>();

        public List<ExcelStructuralLoadGroup> LoadGroups { get; } = new List<ExcelStructuralLoadGroup>();

        public List<ExcelStructuralLoadCase> LoadCases { get; } = new List<ExcelStructuralLoadCase>();

        public List<ExcelStructuralLoadCombination> LoadCombinations { get; } = new List<ExcelStructuralLoadCombination>();

        public List<ExcelStructuralPointAction> PointActions { get; } = new List<ExcelStructuralPointAction>();

        public List<ExcelStructuralPointMoment> PointMoments { get; } = new List<ExcelStructuralPointMoment>();

        public List<ExcelStructuralCurveAction> CurveActions { get; } = new List<ExcelStructuralCurveAction>();

        public List<ExcelStructuralCurveMoment> CurveMoments { get; } = new List<ExcelStructuralCurveMoment>();

        public List<ExcelStructuralSurfaceAction> SurfaceActions { get; } = new List<ExcelStructuralSurfaceAction>();

        public List<ExcelStructuralSurfaceActionDistribution> LoadPanels { get; } =
            new List<ExcelStructuralSurfaceActionDistribution>();

        public List<ExcelStructuralCurveActionThermal> CurveThermals { get; } = new List<ExcelStructuralCurveActionThermal>();

        public List<ExcelStructuralSurfaceActionThermal> SurfaceThermals { get; } =
            new List<ExcelStructuralSurfaceActionThermal>();

        public List<ExcelStructuralPointActionFree> FreePointActions { get; } = new List<ExcelStructuralPointActionFree>();

        public List<ExcelStructuralCurveActionFree> FreeCurveActions { get; } = new List<ExcelStructuralCurveActionFree>();

        public List<ExcelStructuralSurfaceActionFree> FreeSurfaceActions { get; } =
            new List<ExcelStructuralSurfaceActionFree>();

        public List<ExcelStructuralPointSupportDeformation> SupportDeformations { get; } =
            new List<ExcelStructuralPointSupportDeformation>();

        public List<ExcelStructuralProxyElement> ProxyElements { get; } = new List<ExcelStructuralProxyElement>();

        public List<ExcelResultInternalForce1D> Results1D { get; } = new List<ExcelResultInternalForce1D>();

        public List<ExcelResultInternalForce2D> Results2D { get; } = new List<ExcelResultInternalForce2D>();

        /// <summary>SAF object types this adapter has no case for, by type name and count.</summary>
        public IReadOnlyDictionary<string, int> Unrecognised => _unrecognised;

        public static SafObjects Group(ExcelModel model)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            var grouped = new SafObjects();
            foreach (IExcelModuleObject item in model.Objects)
                grouped.Dispatch(item);

            return grouped;
        }

        private void Dispatch(IExcelModuleObject item)
        {
            switch (item)
            {
                case ExcelModelInformation x: ModelInformation = x; break;
                case ExcelProjectInformation x: ProjectInformation = x; break;
                case ExcelStructuralMaterial x: Materials.Add(x); break;
                case ExcelStructuralCrossSection x: CrossSections.Add(x); break;
                case ExcelCompositeShapeDef x: CompositeShapes.Add(x); break;
                case ExcelStructuralStorey x: Storeys.Add(x); break;
                case ExcelStructuralPointConnection x: Points.Add(x); break;
                case ExcelStructuralCurveMember x: Members.Add(x); break;
                case ExcelStructuralCurveMemberVarying x: Varyings.Add(x); break;
                case ExcelStructuralCurveMemberRib x: Ribs.Add(x); break;
                case ExcelStructuralCurveEdge x: InternalEdges.Add(x); break;
                case ExcelStructuralSurfaceMember x: Surfaces.Add(x); break;
                case ExcelStructuralSurfaceMemberOpening x: Openings.Add(x); break;
                case ExcelStructuralSurfaceMemberRegion x: Regions.Add(x); break;
                case ExcelStructuralPointSupport x: PointSupports.Add(x); break;
                case ExcelStructuralCurveConnection x: CurveSupports.Add(x); break;
                case ExcelStructuralEdgeConnection x: EdgeSupports.Add(x); break;
                case ExcelStructuralSurfaceConnection x: SurfaceSupports.Add(x); break;
                case ExcelRelConnectsStructuralMember x: MemberHinges.Add(x); break;
                case ExcelRelConnectsSurfaceEdge x: EdgeHinges.Add(x); break;
                case ExcelRelConnectsRigidLink x: RigidLinks.Add(x); break;
                case ExcelRelConnectsRigidCross x: RigidCrosses.Add(x); break;
                case ExcelRelConnectsRigidMember x: RigidMembers.Add(x); break;
                case ExcelStructuralLoadGroup x: LoadGroups.Add(x); break;
                case ExcelStructuralLoadCase x: LoadCases.Add(x); break;
                case ExcelStructuralLoadCombination x: LoadCombinations.Add(x); break;
                case ExcelStructuralPointAction x: PointActions.Add(x); break;
                case ExcelStructuralPointMoment x: PointMoments.Add(x); break;
                case ExcelStructuralCurveAction x: CurveActions.Add(x); break;
                case ExcelStructuralCurveMoment x: CurveMoments.Add(x); break;
                case ExcelStructuralSurfaceAction x: SurfaceActions.Add(x); break;
                case ExcelStructuralSurfaceActionDistribution x: LoadPanels.Add(x); break;
                case ExcelStructuralCurveActionThermal x: CurveThermals.Add(x); break;
                case ExcelStructuralSurfaceActionThermal x: SurfaceThermals.Add(x); break;
                case ExcelStructuralPointActionFree x: FreePointActions.Add(x); break;
                case ExcelStructuralCurveActionFree x: FreeCurveActions.Add(x); break;
                case ExcelStructuralSurfaceActionFree x: FreeSurfaceActions.Add(x); break;
                case ExcelStructuralPointSupportDeformation x: SupportDeformations.Add(x); break;
                case ExcelStructuralProxyElement x: ProxyElements.Add(x); break;
                case ExcelResultInternalForce1D x: Results1D.Add(x); break;
                case ExcelResultInternalForce2D x: Results2D.Add(x); break;
                default:
                    // The proxy element's vertex and face rows, the workbook
                    // identifier, and anything a later SAF version adds. Counted so
                    // it can be reported, never silently swallowed.
                    string name = item.GetType().Name;
                    _unrecognised.TryGetValue(name, out int count);
                    _unrecognised[name] = count + 1;
                    break;
            }
        }
    }
}
