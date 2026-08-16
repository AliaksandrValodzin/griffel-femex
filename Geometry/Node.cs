namespace griffel_femex.Geometry
{
    /// <summary>
    /// A point of the authored geometry, and the model's unit of connectivity:
    /// two elements are joined where they name the same node number, and only
    /// there.
    ///
    /// The format permits more than one node at a single location, because that
    /// is the only way to state that a joint is deliberately disconnected — a
    /// movement joint, a slip plane, two structures that merely touch. A model is
    /// therefore not invalid for having coincident nodes; but since the intended
    /// and the accidental case look identical,
    /// <see cref="FemexModel.Validate()"/> reports them as a warning. When
    /// building geometry in code, reach for
    /// <see cref="FemexModel.GetOrAddNode"/> so that elements meeting at a point
    /// share the node that is already there.
    /// </summary>
    public class Node : IIdentified
    {
        // Unique identifier for the node
        public int NodeNumber { get; set; }

        // Optional round-trip identity. Null means this node has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

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
