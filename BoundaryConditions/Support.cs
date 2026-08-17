using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// A support (boundary condition) restraining degrees of freedom at a point,
    /// along a line, or over an area, each DOF with infinite or finite stiffness.
    /// </summary>
    public class Support : IIdentified, IExtensible
    {
        public int Id { get; set; }

        // Optional round-trip identity. Null means this support has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Whether the support acts on a point, a line, or an area
        public SupportTarget Target { get; set; }

        // Node ids the support applies to (references Node.NodeNumber)
        public List<int> NodeIds { get; set; } = new List<int>();

        // An area support may instead follow a plate, so that it survives editing
        // and re-meshing. References Plate.Id; only valid when Target is Area.
        public int? PlateId { get; set; }

        // References PlateRegion.Id within PlateId; null = the whole plate.
        public int? RegionId { get; set; }

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

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
