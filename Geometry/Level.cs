namespace griffel_femex.Geometry
{
    public class Level
    {
        // A unique identifier for the level (0 for ground, 1 for first floor, etc.)
        public int LevelNumber { get; set; }

        // Optional human-readable name
        public string? Name { get; set; }

        // Elevation relative to a global datum like sea level
        public double AbsoluteElevation { get; set; }

        // Elevation relative to the project internal zero point
        public double RelativeElevation { get; set; }

        // Flag indicating if this level is considered the primary ground plane
        public bool IsGround { get; set; }

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
