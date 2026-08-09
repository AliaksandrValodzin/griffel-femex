using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Grids
{
    /// <summary>
    /// One architectural setting-out line of a <see cref="Grid"/>, infinite in
    /// plan and expressed in the grid's own local coordinates.
    ///
    /// A gridline is not structure. It carries no material, no section and no
    /// stiffness, it generates no element and no degree of freedom, and removing
    /// every grid from a model leaves the analysis unchanged. It exists so that
    /// authoring code can snap geometry to it — see
    /// <see cref="FemexModel.TryGetIntersection"/> — and so that a consumer can
    /// draw and label it the way a setting-out drawing does.
    ///
    /// <see cref="Label"/> is the line's identity within its grid: the first
    /// string key in FEMEX, compared ordinally and case-sensitively. Unlike every
    /// integer id in the format it is authored, never generated.
    ///
    /// Reserved future discriminators (not implemented):
    ///  - "radial"   — a line at an angle through a centre point.
    ///  - "circular" — a ring at a radius about a centre point.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(OrthogonalGridline), "orthogonal")]
    [JsonDerivedType(typeof(FreeGridline), "free")]
    public abstract class Gridline
    {
        // The line's identity within its grid: "A", "B", "1", "2", "A.1".
        // Must not be blank, and must not repeat within one grid.
        public string Label { get; set; } = string.Empty;
    }
}
