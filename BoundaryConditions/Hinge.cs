using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// A hinge releasing degrees of freedom at an element end (point) or edge (linear).
    /// Releases can be full or partial (residual stiffness) per DOF.
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

        // Plate targets only. The two ends of the hinged edge, which must be
        // adjacent in the referenced contour (references Node.NodeNumber).
        // Both null for a bar target.
        public int? EdgeStartNodeId { get; set; }
        public int? EdgeEndNodeId { get; set; }

        // Six degrees of freedom releases
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
