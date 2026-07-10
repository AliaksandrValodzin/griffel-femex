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

        // Which end or edge of the element the hinge is at.
        // For a bar: 0 = start end, 1 = end end.
        // For a plate: the index of the edge in node order.
        public int EndOrEdgeIndex { get; set; }

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
