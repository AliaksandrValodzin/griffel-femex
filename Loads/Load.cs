using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Loads
{
    /// <summary>
    /// Abstract base class for all structural loads.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(PointLoad), "point")]
    [JsonDerivedType(typeof(LinearLoad), "linear")]
    [JsonDerivedType(typeof(AreaLoad), "area")]
    [JsonDerivedType(typeof(TemperatureLoad), "temperature")]
    public abstract class Load : IIdentified, IExtensible
    {
        /// <summary>
        /// The load's key, in its own id space beside <see cref="BoundaryConditions.Support.Id"/>
        /// and <see cref="BoundaryConditions.Hinge.Id"/> rather than in the shared
        /// element space — a load is not an element.
        ///
        /// Added in schema 1.3. Nothing in FEMEX references a load, so this is not
        /// a foreign key: it exists so that a load can be named in a message and
        /// told apart from another carrying the same label, which is what every
        /// other authored entity could already do. A pre-1.3 file has none, and
        /// reading one numbers its loads 1..N in list order — the only identity a
        /// load ever had before this.
        /// </summary>
        public int Id { get; set; }

        // Optional round-trip identity. Null means this load has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this load was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        public string? Label { get; set; }

        // References LoadCase.Number
        public int LoadCaseNumber { get; set; }

        // Members this build does not know; see IExtensible. Declared here and
        // inherited by every load type, DistributedLoad's two included.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
