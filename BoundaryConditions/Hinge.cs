namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// A hinge releasing degrees of freedom at an element end (point) or edge (linear).
    /// Releases can be full or partial (residual stiffness) per DOF.
    /// </summary>
    public class Hinge
    {
        public int Id { get; set; }

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
    }
}
