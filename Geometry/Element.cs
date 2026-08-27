using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry
{
    /// <summary>
    /// Abstract base for geometric elements (bars and plates).
    /// Only shared, serializable properties live here.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Bar), "bar")]
    [JsonDerivedType(typeof(Plate), "plate")]
    public abstract class Element : IIdentified, IExtensible
    {
        // Shared element-id space: a value is unique across bars, plates and
        // generated mesh faces, so Hinge.ElementId and TemperatureLoad.ElementIds
        // can address any of them.
        public int Id { get; set; }

        // Optional round-trip identity. Null means this element has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this element was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        // Material is declared by the derived types: it is required on a bar but
        // absent on a plate that is an opening.

        // Node ids that define this element (order matters for plates)
        public abstract IEnumerable<int> GetNodeIds();

        // Members this build does not know; see IExtensible. Declared here and
        // inherited by Bar and Plate alike.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
