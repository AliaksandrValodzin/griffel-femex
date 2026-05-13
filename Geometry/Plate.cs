using griffel_femex.Geometry.Sections;
using griffel_femex.Materials;

namespace griffel_femex.Geometry
{
    public class Plate : Element
    {
        // Plates can have 3 (triangular), 4 (quad), or more nodes
        public List<Node> Nodes { get; set; } = new List<Node>();

        public Plate(int id, List<Node> nodes, Section section, Material material, double angle = 0)
        {
            if (nodes.Count < 3)
                throw new ArgumentException("A plate must have at least 3 nodes.");

            Id = id;
            Nodes = nodes;
            Section = section;
            Material = material;
            RotationAngle = angle;
        }

        public override IEnumerable<Node> GetNodes()
        {
            return Nodes;
        }
    }
}
