namespace griffel_femex.Geometry
{

    public class Level
    {
        // A unique identifier for the level
        public int LevelNumber { get; set; }

        public string Name { get; set; }

        // Elevation relative to Sea Level (often represented in meters or feet)
        public decimal AbsoluteElevation { get; set; }

        // Elevation relative to a project internal datum (e.g., Project Zero)
        public decimal RelativeElevation { get; set; }

        // Flag to determine if this level represents the primary ground plane
        public bool IsGround { get; set; }

        // Basic constructor
        public Level(int number, string name, decimal absolute, decimal relative, bool isGround = false)
        {
            LevelNumber = number;
            Name = name;
            AbsoluteElevation = absolute;
            RelativeElevation = relative;
            IsGround = isGround;
        }
    }
}
