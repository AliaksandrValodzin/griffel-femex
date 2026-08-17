using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Mesh
{
    /// <summary>
    /// A node of the generated finite-element mesh.
    ///
    /// Unlike an authored <see cref="Geometry.Node"/>, a mesh node carries an
    /// absolute Z rather than a level number plus an offset: a generated interior
    /// node on a warped or vertical panel has no natural level. Z uses the same
    /// datum as Level.AbsoluteElevation.
    /// </summary>
    public class MeshNode : IExtensible
    {
        // Unique within the mesh. Its own id space, not Node.NodeNumber.
        public int Id { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        // References Node.NodeNumber when this mesh node coincides with an authored
        // node; null for nodes the mesher generated.
        public int? SourceNodeId { get; set; }

        // Parameterless constructor for serialization
        public MeshNode() { }

        // Convenience constructor
        public MeshNode(int id, double x, double y, double z, int? sourceNodeId = null)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
            SourceNodeId = sourceNodeId;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
