namespace griffel_femex.Geometry
{
    public class Bar : Element
    {
        // References Node.NodeNumber
        public int StartNodeId { get; set; }
        public int EndNodeId { get; set; }

        // References Section.Id
        public int SectionId { get; set; }

        // Rotation of local X-axis relative to global X-axis (degrees)
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
