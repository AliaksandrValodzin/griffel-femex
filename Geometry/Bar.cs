using griffel_femex.Geometry.Sections;
using griffel_femex.Materials;

namespace griffel_femex.Geometry
{
    public class Bar : Element
    {
        public Node StartNode { get; set; }
        public Node EndNode { get; set; }

        public Bar(int id, Node start, Node end, Section section, Material material, double angle = 0)
        {
            Id = id;
            StartNode = start;
            EndNode = end;
            Section = section;
            Material = material;
            RotationAngle = angle;
        }

        public override IEnumerable<Node> GetNodes()
        {
            return new List<Node> { StartNode, EndNode };
        }
    }
}
