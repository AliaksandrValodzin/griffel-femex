namespace griffel_femex.Mesh
{
    /// <summary>
    /// The generated finite-element mesh of a model's plates. Optional: a model that
    /// has not been meshed simply omits it.
    ///
    /// FEMEX itself does not generate meshes — this block is data written by an
    /// external mesher, with every face back-linked to the plate and region it came
    /// from.
    /// </summary>
    public class FemexMesh
    {
        // Free-text provenance, e.g. "griffel-mesher 0.1".
        public string? Generator { get; set; }

        // ISO-8601 timestamp as free text, in keeping with Units being free text.
        public string? GeneratedAt { get; set; }

        public List<MeshNode> Nodes { get; set; } = new List<MeshNode>();

        public List<MeshFace> Faces { get; set; } = new List<MeshFace>();
    }
}
