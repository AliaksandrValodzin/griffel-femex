using griffel_femex.Geometry.Sections;
using griffel_femex.Materials;

namespace griffel_femex.Geometry
{
    public abstract class Element
    {
        public int Id { get; set; }
        public Material Material { get; set; }
        public Section Section { get; set; }

        // Rotation of local X-axis relative to global X-axis (in degrees or radians)
        public double RotationAngle { get; set; }

        // Abstract property to ensure all elements provide access to their nodes
        public abstract IEnumerable<Node> GetNodes();
    }
}
