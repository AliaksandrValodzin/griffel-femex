using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Surfaces
{
    /// <summary>
    /// Abstract base for surface (plate and wall) properties, stored separately on
    /// the model and referenced by id — the plate counterpart of
    /// <see cref="Sections.Section"/>.
    ///
    /// Material is deliberately NOT part of a surface property: a plate references a
    /// material and a surface property independently, exactly as a bar references a
    /// material and a section. That lets one thickness be reused across grades.
    ///
    /// Reserved future discriminators (not implemented):
    ///  - "variable" — thickness interpolated linearly between three nodes.
    ///  - "layered"  — a stack of layers with individual thickness and material.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(ConstantThickness), "constant")]
    public abstract class SurfaceProperty : IIdentified, IExtensible
    {
        // Referenced by Plate.SurfacePropertyId, PlateRegion.SurfacePropertyId
        // and MeshFace.SurfacePropertyId.
        public int Id { get; set; }

        // Optional round-trip identity. Null means this property has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Robot and ETABS key properties by name, so a blank or repeated one is
        // reported by FemexModel.Validate() as a warning.
        public string? Name { get; set; }

        /// <summary>
        /// Representative thickness for reporting, and for consumers that cannot
        /// handle a varying thickness. Exact for a constant property.
        /// </summary>
        public abstract double GetNominalThickness();

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
