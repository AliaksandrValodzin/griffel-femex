namespace griffel_femex.Geometry
{
    public class Node
    {
        // Unique identifier for the node
        public int NodeNumber { get; set; }

        // Horizontal coordinates
        public double X { get; set; }
        public double Y { get; set; }

        // Reference to the Level object this node belongs to
        public Level AssociatedLevel { get; set; }

        // Distance above or below the level's elevation
        public double VerticalOffset { get; set; }

        // Constructor
        public Node(int number, double x, double y, Level level, double verticalOffset = 0.0)
        {
            NodeNumber = number;
            X = x;
            Y = y;
            AssociatedLevel = level ?? throw new ArgumentNullException(nameof(level));
            VerticalOffset = verticalOffset;
        }

        /// <summary>
        /// Calculates the total absolute Z-coordinate based on the level and offset.
        /// </summary>
        public decimal GetTotalAbsoluteElevation()
        {
            return AssociatedLevel.AbsoluteElevation + (decimal)VerticalOffset;
        }
    }
}
