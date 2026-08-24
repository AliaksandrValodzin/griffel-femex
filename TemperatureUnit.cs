namespace griffel_femex
{
    /// <summary>
    /// The unit a <see cref="Loads.TemperatureLoad"/>'s numbers are in.
    ///
    /// Closed for the reason <see cref="LengthUnit"/> gives, and the shortest of the
    /// five: three scales exist and no fourth is coming.
    ///
    /// <b>A temperature load is a <i>change</i>, not a reading</b>, and the two do
    /// not share a unit even though they share a name. A ΔT of 20 is the same change
    /// in Celsius and in Kelvin and a different one in Fahrenheit, so this enum only
    /// ever distinguishes <see cref="Fahrenheit"/> from the other two — which is
    /// precisely why it is worth stating, and why <see cref="Celsius"/> and
    /// <see cref="Kelvin"/> are both here rather than collapsed: a file that says
    /// <c>Kelvin</c> is saying something true about its author's convention, and
    /// discarding that to save an enum member would be inventing on their behalf.
    /// The material's α is in 1/K regardless, as its own doc states.
    /// </summary>
    public enum TemperatureUnit
    {
        /// <summary>Degrees Celsius.</summary>
        Celsius,

        /// <summary>Degrees Fahrenheit.</summary>
        Fahrenheit,

        /// <summary>Kelvin.</summary>
        Kelvin,
    }
}
