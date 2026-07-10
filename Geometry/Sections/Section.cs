using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Abstract base for bar cross-sections, stored separately and referenced by id.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Rectangle), "rectangle")]
    [JsonDerivedType(typeof(Circle), "circle")]
    [JsonDerivedType(typeof(TSection), "tshape")]
    public abstract class Section
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        // Cross-sectional area of the section.
        public abstract double CalculateArea();
    }
}
