namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// A support (boundary condition) restraining degrees of freedom at a point,
    /// along a line, or over an area, each DOF with infinite or finite stiffness.
    /// </summary>
    public class Support
    {
        public int Id { get; set; }

        // Whether the support acts on a point, a line, or an area
        public SupportTarget Target { get; set; }

        // Node ids the support applies to (references Node.NodeNumber)
        public List<int> NodeIds { get; set; } = new List<int>();

        // Six degrees of freedom
        public Restraint Ux { get; set; } = new Restraint();
        public Restraint Uy { get; set; } = new Restraint();
        public Restraint Uz { get; set; } = new Restraint();
        public Restraint Rx { get; set; } = new Restraint();
        public Restraint Ry { get; set; } = new Restraint();
        public Restraint Rz { get; set; } = new Restraint();

        public Support() { }

        public Support(int id, SupportTarget target, List<int> nodeIds)
        {
            Id = id;
            Target = target;
            NodeIds = nodeIds;
        }
    }
}
