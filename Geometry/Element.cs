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
    public abstract class Element
    {
        public int Id { get; set; }

        // References Material.Id
        public int MaterialId { get; set; }

        // Node ids that define this element (order matters for plates)
        public abstract IEnumerable<int> GetNodeIds();
    }
}
