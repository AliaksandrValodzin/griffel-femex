namespace griffel_femex.Loads
{
    /// <summary>
    /// Represents an area load (pressure) applied over a surface defined by a sequence of nodes.
    /// </summary>
    public class AreaLoad : Load
    {
        // The sequence of nodes defining the boundary (e.g., 4 nodes for a quad)
        public List<int> NodeSequence { get; set; } = new List<int>();

        /// <summary>
        /// Magnitude of the pressure load (Force per unit area).
        /// </summary>
        public double Magnitude { get; set; }

    }
}
