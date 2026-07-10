namespace griffel_femex
{
    /// <summary>
    /// Optional metadata describing the unit convention used by the model.
    /// </summary>
    public class Units
    {
        // Length unit, e.g. "m", "mm", "ft"
        public string? Length { get; set; }

        // Force unit, e.g. "kN", "N", "kip"
        public string? Force { get; set; }

        public Units() { }

        public Units(string? length, string? force)
        {
            Length = length;
            Force = force;
        }
    }
}
