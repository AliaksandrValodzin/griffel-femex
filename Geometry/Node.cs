namespace griffel_femex.Geometry
{
    public class Node
    {
        // Unique identifier for the node
        public int NodeNumber { get; set; }

        // Horizontal coordinates
        public double X { get; set; }
        public double Y { get; set; }

        // The level number this node belongs to (references Level.LevelNumber)
        public int LevelNumber { get; set; }

        // Distance above or below the level's elevation
        public double VerticalOffset { get; set; }

        // Parameterless constructor for serialization
        public Node() { }

        // Convenience constructor
        public Node(int number, double x, double y, int levelNumber, double verticalOffset = 0.0)
        {
            NodeNumber = number;
            X = x;
            Y = y;
            LevelNumber = levelNumber;
            VerticalOffset = verticalOffset;
        }

        /// <summary>
        /// Calculates the total absolute Z-coordinate based on the referenced level and offset.
        /// Not serialized — resolves the level from the model.
        /// </summary>
        public double GetTotalAbsoluteElevation(FemexModel model)
        {
            Level? level = model.Levels.Find(l => l.LevelNumber == LevelNumber);
            if (level is null)
                throw new InvalidOperationException($"Node {NodeNumber} references unknown level {LevelNumber}.");
            return level.AbsoluteElevation + VerticalOffset;
        }
    }
}
