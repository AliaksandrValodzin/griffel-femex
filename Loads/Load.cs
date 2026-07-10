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
    public abstract class Load
    {
        public string? Label { get; set; }

        // References LoadCase.Number
        public int LoadCaseNumber { get; set; }
    }
}
