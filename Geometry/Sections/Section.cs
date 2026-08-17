using System.Text.Json;
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
    public abstract class Section : IIdentified, IExtensible
    {
        public int Id { get; set; }

        // Optional round-trip identity. Null means this section has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Robot and ETABS key sections by name, so a blank or repeated one is
        // reported by FemexModel.Validate() as a warning.
        public string? Name { get; set; }

        // Cross-sectional area of the section.
        public abstract double CalculateArea();

        // Members this build does not know; see IExtensible. The "type"
        // discriminator above is not one of them: System.Text.Json consumes it
        // before extension data is populated.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
