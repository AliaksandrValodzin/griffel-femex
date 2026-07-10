namespace griffel_femex.Geometry
{
    public class Plate : Element
    {
        // Sequence of node ids defining the plate (order matters).
        // Plates can have 3 (triangular), 4 (quad), or more nodes.
        public List<int> NodeIds { get; set; } = new List<int>();

        // Plate thickness is a property of the plate — a single number (per spec).
        public double Thickness { get; set; }

        // Parameterless constructor for serialization
        public Plate() { }

        // Convenience constructor
        public Plate(int id, List<int> nodeIds, double thickness, int materialId)
        {
            if (nodeIds.Count < 3)
                throw new ArgumentException("A plate must have at least 3 nodes.");

            Id = id;
            NodeIds = nodeIds;
            Thickness = thickness;
            MaterialId = materialId;
        }

        public override IEnumerable<int> GetNodeIds()
        {
            return NodeIds;
        }
    }
}
