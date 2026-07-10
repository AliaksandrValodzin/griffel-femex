namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents a thermal load applied to one or more elements: a uniform
    /// temperature change and an optional gradient across the section depth.
    /// </summary>
    public class TemperatureLoad : Load
    {
        // Element ids this temperature load applies to (references Element.Id)
        public List<int> ElementIds { get; set; } = new List<int>();

        // Uniform temperature change (e.g., in degrees Celsius)
        public double DeltaT { get; set; }

        // Optional temperature gradient across the section depth
        // (difference per unit depth). Null = no gradient.
        public double? GradientPerDepth { get; set; }
    }
}
