namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents a distinct group of loads that act together under a specific environmental or usage condition.
    /// </summary>
    public class LoadCase
    {
        public int Number { get; set; }
        public string Label { get; set; }
        public LoadNature Nature { get; set; }

        public LoadCase(int number, string label, LoadNature nature)
        {
            Number = number;
            Label = label;
            Nature = nature;
        }

        // Default constructor for serialization or simple instantiation
        public LoadCase() { }

        public override string ToString()
        {
            return $"[{Number}] {Label} ({Nature})";
        }
    }
}
