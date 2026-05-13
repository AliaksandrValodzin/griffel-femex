namespace griffel_femex.Loads
{
    /// <summary>
    /// Abstract base class for all structural loads.
    /// </summary>
    public abstract class Load
    {
        public string Label { get; set; }
        public LoadCase Case { get; set; } // e.g., Dead, Live, Wind

    }
}
