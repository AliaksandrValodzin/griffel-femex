using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// A hinge releasing degrees of freedom at an element end (point) or edge (linear).
    /// Releases can be full or partial (residual stiffness) per DOF.
    ///
    /// <b>The six releases are stated in <i>local</i> axes, never global</b>, and
    /// which local axes depends on what the hinge sits on. Until this was written the
    /// format did not say, and the only executable statement of it anywhere was a
    /// validation rule — <c>FemexModel.Validate()</c> reports a tension-only bar whose
    /// hinge releases <see cref="Ux"/> as carrying nothing, which is true of the
    /// <i>axial</i> DOF and of no other. One rule inferring a convention is not a
    /// convention; it is <c>FEMEX_SAF_Fit.md</c> §4 item 7 again, where two adapters
    /// read the same stiffness and differed by the area of a slab, neither wrong
    /// against a spec that did not exist.
    ///
    /// <list type="bullet">
    /// <item><b>A hinge on a bar</b> — the bar's own local axes, roll included,
    /// exactly as <see cref="Geometry.Bar"/> defines them and
    /// <c>FemexModel.TryGetBarLocalAxes</c> produces them. So <see cref="Ux"/> is the
    /// <b>axial</b> release, <see cref="Rx"/> torsion, <see cref="Rz"/> the
    /// major-axis bending release that makes a beam end pinned, and <see cref="Ry"/>
    /// the minor-axis one. This is what every program FEMEX targets already
    /// means — SAF's <c>RelConnectsStructuralMember</c>, RFEM's <c>memberHinge</c>,
    /// ETABS' <c>SetReleases</c>, Robot's <c>I_LT_BAR_RELEASE</c> are all in the
    /// member frame — so the rule is a statement of the existing agreement rather
    /// than a choice among candidates.</item>
    /// <item><b>A hinge on a plate edge</b> — the <b>edge's</b> frame, not the
    /// panel's: local <b>x</b> along the edge, from <see cref="EdgeStartNodeId"/> to
    /// <see cref="EdgeEndNodeId"/> with its out-of-plane part removed; local <b>z</b>
    /// the panel's normal; local <b>y</b> = ẑ × x̂, which for an edge taken in its
    /// contour's own order points into the panel. So <see cref="Rx"/> is the
    /// rotation about the edge — the release that makes a slab edge simply
    /// supported — <see cref="Uz"/> the out-of-plane slip, and <see cref="Ux"/> the
    /// slide along the edge itself.</item>
    /// <item><b>A hinge on a mesh face</b> — the same edge rule over the face's own
    /// nodes and normal, for the edge <see cref="EndOrEdgeIndex"/> names. A generated
    /// face has no named edge, which is why the index is the only address it
    /// has.</item>
    /// </list>
    ///
    /// <b>The edge frame rather than the panel's is the load-bearing choice here</b>,
    /// and SAF makes the same one: <c>RelConnectsSurfaceEdge</c> is in the edge's LCS.
    /// The two differ for every edge not parallel to the panel's local x, and the
    /// panel's <see cref="Geometry.Plate.LocalAxisAngle"/> would otherwise turn the
    /// meaning of a release that has nothing to do with it. It also makes the common
    /// statement the simple one: "this edge is hinged about itself" is
    /// <c>rx</c>, on every edge of every panel, whichever way the panel is set out.
    ///
    /// <b>A region does not change z.</b> The normal is the panel's whichever contour
    /// the edge belongs to, so a hinge on a void's edge and one on the outer contour
    /// agree about which side is up — where each contour's own winding deciding would
    /// have them disagree, an opening being wound against its panel.
    ///
    /// <c>FemexModel.TryGetHingeLocalAxes</c> is this whole rule, executable, and
    /// <c>FemexModel.TryGetEdgeLocalAxes</c> the edge half of it on its own. A
    /// receiver should ask those rather than reimplement either.
    ///
    /// <b>What this does not add is a choice.</b> There is no coordinate-system flag
    /// on a hinge and this does not create one: the frame is a function of what the
    /// hinge sits on, which is the reading every target program already takes. The
    /// gap <c>FEMEX_Interop_Review.md</c> §5.6 records is a different one — a
    /// <see cref="Support"/> wants a frame it can <i>choose</i>, because an inclined
    /// bearing is unrepresentable without one — and closing that one leaves this rule
    /// untouched.
    /// </summary>
    public class Hinge : IIdentified, IExtensible
    {
        public int Id { get; set; }

        // Optional round-trip identity. Null means this hinge has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this hinge was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        // Whether the hinge acts at a point (element end) or along a line (element edge)
        public HingeTarget Target { get; set; }

        // Node ids the hinge applies to (references Node.NodeNumber)
        public List<int> NodeIds { get; set; } = new List<int>();

        // The element this hinge belongs to (references Element.Id)
        public int ElementId { get; set; }

        // Which end of a BAR the hinge is at: 0 = start end, 1 = end end.
        // Not used when ElementId refers to a plate — a plate edge is named by its
        // two nodes below, which survives inserting a vertex into the contour.
        public int EndOrEdgeIndex { get; set; }

        /// <summary>
        /// Where along the bar the hinge is, when it is not at either end: relative,
        /// 0 at the start node and 1 at the end node. Null — every hinge written
        /// before 1.10 — means <see cref="EndOrEdgeIndex"/> decides, which is the
        /// only thing a hinge could say before this.
        ///
        /// <b>No <c>BarId</c> beside it, deliberately</b>, where
        /// <c>PointLoad</c> and <see cref="Support"/> both gained one:
        /// <see cref="ElementId"/> is already the member, so a second reference to it
        /// would be two sources of truth about the same fact and the two could
        /// disagree. See <c>PointLoad.Position</c> for why the station is stored
        /// rather than resolved into a node.
        ///
        /// Bar targets only, and <c>FemexModel.Validate()</c> reports one stated
        /// against a plate.
        /// </summary>
        public double? Position { get; set; }

        // Plate targets only. References PlateRegion.Id within the plate;
        // null = the plate's outer contour.
        public int? RegionId { get; set; }

        /// <summary>
        /// Plate targets only. The two ends of the hinged edge, which must be adjacent
        /// in the referenced contour (references <c>Node.NodeNumber</c>). Both null for
        /// a bar target.
        ///
        /// <b>Their order is not cosmetic</b>: it is what local x runs along, so
        /// writing the same edge the other way round reverses x and y and turns a
        /// release stated in y into its own opposite. Validation accepts either order
        /// — an edge is adjacent in a contour whichever way it is named — so this is
        /// the one place the pair says something the adjacency check does not.
        /// </summary>
        public int? EdgeStartNodeId { get; set; }
        public int? EdgeEndNodeId { get; set; }

        // Six degrees of freedom releases, in the local frame this class documents:
        // the bar's own axes on a member, the edge's on a plate or mesh-face edge.
        // FemexModel.TryGetHingeLocalAxes is which of the two, executable.
        public Release Ux { get; set; } = new Release();
        public Release Uy { get; set; } = new Release();
        public Release Uz { get; set; } = new Release();
        public Release Rx { get; set; } = new Release();
        public Release Ry { get; set; } = new Release();
        public Release Rz { get; set; } = new Release();

        public Hinge() { }

        public Hinge(int id, HingeTarget target, int elementId, int endOrEdgeIndex, List<int> nodeIds)
        {
            Id = id;
            Target = target;
            ElementId = elementId;
            EndOrEdgeIndex = endOrEdgeIndex;
            NodeIds = nodeIds;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
