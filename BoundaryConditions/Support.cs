using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// A support (boundary condition) restraining degrees of freedom at a point,
    /// along a line, or over an area, each DOF with infinite or finite stiffness and,
    /// from 1.8, a sense.
    ///
    /// <b><see cref="Target"/> is what a <see cref="Restraint.Stiffness"/> on this
    /// support is measured against</b>, and stating that is the whole of
    /// <c>FEMEX_SAF_Fit.md</c> §4 item 7 — the cheapest item in that document and the
    /// only one closable by writing a sentence:
    ///
    /// <list type="bullet">
    /// <item><description><see cref="SupportTarget.Point"/> — a <b>total spring</b>,
    /// force/length (moment/radian rotationally).</description></item>
    /// <item><description><see cref="SupportTarget.Linear"/> — <b>per unit length</b>
    /// of the supported line, force/length².</description></item>
    /// <item><description><see cref="SupportTarget.Area"/> — a <b>bedding modulus per
    /// unit area</b>, force/length³: SAF's Winkler <c>C1</c>. SAF's Pasternak
    /// <c>C2</c> is deliberately unmapped.</description></item>
    /// </list>
    ///
    /// In the model's own units, and <see cref="FemexModel.Validate()"/> warns when an
    /// area support states a stiffness in a model that states none. Stated twice —
    /// here and on <see cref="Restraint.Stiffness"/>, which argues it at length —
    /// because a reader reaching either type first must find it, and the number lives
    /// on one while its meaning is set by the other.
    ///
    /// The six restraints are <b>one type applied uniformly</b>, which is the
    /// factoring <c>FEMEX_Interop_Review.md</c> §3.5 calls the universal one and is
    /// right to. It also means a rotational DOF can carry a
    /// <see cref="Restraint.Sense"/>, which describes nothing; that too is a warning
    /// rather than a schema rule.
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
