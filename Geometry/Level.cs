namespace griffel_femex.Geometry
{
    /// <summary>
    /// A storey of the model: an elevation, and the architectural grids the
    /// geometry at that elevation is set out on. Nodes reference a level rather
    /// than storing a Z, so a level is where the model's vertical position lives.
    ///
    /// <see cref="GridIds"/> resolves in three ways, and the distinction between
    /// the first two matters:
    ///  1. null       — inherit <see cref="FemexModel.DefaultGridIds"/>.
    ///  2. empty list — this level deliberately has no grid, whatever the default.
    ///  3. non-empty  — these grids replace the default entirely. They are not
    ///                  merged with it, so a level that wants the default plus one
    ///                  more must name both.
    /// </summary>
    public class Level : IIdentified
    {
        // A unique identifier for the level (0 for ground, 1 for first floor, etc.)
        public int LevelNumber { get; set; }

        // Optional round-trip identity. Null means this level has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional human-readable name
        public string? Name { get; set; }

        // Elevation relative to a global datum like sea level
        public double AbsoluteElevation { get; set; }

        // Elevation relative to the project internal zero point
        public double RelativeElevation { get; set; }

        // Flag indicating if this level is considered the primary ground plane
        public bool IsGround { get; set; }

        // The architectural grids this level is set out on (references Grid.Id).
        // Deliberately left null rather than initialized: null inherits the
        // model's default, an empty list overrides it with no grid at all. See
        // the class summary.
        public List<int>? GridIds { get; set; }

        // Parameterless constructor for serialization
        public Level() { }

        // Convenience constructor
        public Level(int number, string? name, double absolute, double relative, bool isGround = false)
        {
            LevelNumber = number;
            Name = name;
            AbsoluteElevation = absolute;
            RelativeElevation = relative;
            IsGround = isGround;
        }
    }
}
