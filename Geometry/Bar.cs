namespace griffel_femex.Geometry
{
    /// <summary>
    /// A straight one-dimensional member between two nodes.
    ///
    /// <b>Local axes</b>, the ETABS/SAP convention, with
    /// <see cref="RotationAngle"/> zero and the global frame right-handed and Z up:
    ///
    /// <list type="bullet">
    /// <item>local <b>x</b> = the unit vector from <see cref="StartNodeId"/> to
    /// <see cref="EndNodeId"/>.</item>
    /// <item><b>Non-vertical bar:</b> local <b>y</b> = normalize(Ẑ − (Ẑ·x̂) x̂) — the
    /// vertical direction with the axial part removed, so it lies in the vertical
    /// plane through the member and points upward. Local <b>z</b> = x̂ × ŷ, which is
    /// horizontal.</item>
    /// <item><b>Vertical bar</b>, where that construction degenerates: local
    /// <b>y</b> = global <b>+X</b>, and local <b>z</b> = x̂ × ŷ, which is global
    /// <b>+Y</b> for a bar drawn upward.</item>
    /// </list>
    ///
    /// <see cref="RotationAngle"/> then rolls y and z about local x.
    /// <c>FemexModel.TryGetBarLocalAxes</c> is this rule, executable, and is what a
    /// consumer should use rather than reimplementing it.
    /// </summary>
    public class Bar : Element
    {
        // References Node.NodeNumber
        public int StartNodeId { get; set; }
        public int EndNodeId { get; set; }

        // References Section.Id
        public int SectionId { get; set; }

        // References Material.Id
        public int MaterialId { get; set; }

        // Roll of the local y and z axes about local x, in degrees, right-hand
        // rule about local +x. Local x itself is fixed by the two nodes and no
        // angle can change it.
        public double RotationAngle { get; set; }

        // Parameterless constructor for serialization
        public Bar() { }

        // Convenience constructor
        public Bar(int id, int startNodeId, int endNodeId, int sectionId, int materialId, double rotationAngle = 0.0)
        {
            Id = id;
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SectionId = sectionId;
            MaterialId = materialId;
            RotationAngle = rotationAngle;
        }

        public override IEnumerable<int> GetNodeIds()
        {
            return new[] { StartNodeId, EndNodeId };
        }
    }
}
