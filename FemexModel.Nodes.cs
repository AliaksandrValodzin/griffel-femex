using griffel_femex.Geometry;

namespace griffel_femex
{
    /// <summary>
    /// Node lookup and creation. Nodes are the model's connectivity: two elements
    /// are joined where they name the same node number, and only there. Code that
    /// builds geometry should therefore reuse the node that is already at a
    /// location rather than add a second one, which is what
    /// <see cref="GetOrAddNode"/> is for.
    /// </summary>
    public partial class FemexModel
    {
        /// <summary>
        /// 1e-6 of the model's bounding-box diagonal — the fraction the coincidence
        /// and planarity tolerances are both scaled by.
        /// </summary>
        private const double RelativeGeometricTolerance = 1e-6;

        /// <summary>The floor under every scaled tolerance, for degenerate models.</summary>
        private const double MinimumGeometricTolerance = 1e-9;

        /// <summary>
        /// Two nodes are at the same location when they are within this distance of
        /// each other. Scaled to the model's own extent so it means the same thing
        /// whether the model is authored in metres or millimetres, and floored so an
        /// empty or single-point model still has a usable value.
        ///
        /// It therefore grows as the model grows, but only by 1e-6 of the diagonal,
        /// so a node found by <see cref="FindNodeAt"/> while the model was being
        /// built is still coincident once it is finished — <see cref="Validate()"/>
        /// uses this same value.
        /// </summary>
        public double GetCoincidenceTolerance()
        {
            double minX = 0.0, maxX = 0.0, minY = 0.0, maxY = 0.0, minZ = 0.0, maxZ = 0.0;
            bool any = false;

            foreach (var node in Nodes)
            {
                if (!TryGetAbsolutePoint(node, out double x, out double y, out double z))
                    continue;

                if (!any)
                {
                    minX = maxX = x;
                    minY = maxY = y;
                    minZ = maxZ = z;
                    any = true;
                    continue;
                }

                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
            }

            if (!any)
                return MinimumGeometricTolerance;

            double dx = maxX - minX;
            double dy = maxY - minY;
            double dz = maxZ - minZ;
            double diagonal = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            return Math.Max(RelativeGeometricTolerance * diagonal, MinimumGeometricTolerance);
        }

        /// <summary>
        /// The node at the given location, or null if there is none. Matching is on
        /// the absolute point, so a node reached from a different level and offset
        /// still counts as being there.
        /// </summary>
        /// <exception cref="InvalidOperationException">The level does not exist.</exception>
        public Node? FindNodeAt(double x, double y, int levelNumber, double verticalOffset = 0.0)
        {
            double z = GetLevelElevation(levelNumber) + verticalOffset;
            double tolerance = GetCoincidenceTolerance();
            double toleranceSquared = tolerance * tolerance;

            foreach (var node in Nodes)
            {
                if (!TryGetAbsolutePoint(node, out double nx, out double ny, out double nz))
                    continue;

                double ex = nx - x, ey = ny - y, ez = nz - z;
                if (ex * ex + ey * ey + ez * ez <= toleranceSquared)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// The node at the given location, adding one numbered
        /// <see cref="NextNodeNumber"/> if it is not there yet. Use this rather than
        /// <c>Nodes.Add</c> so that elements meeting at a point share one node and
        /// are actually connected.
        /// </summary>
        /// <exception cref="InvalidOperationException">The level does not exist.</exception>
        public Node GetOrAddNode(double x, double y, int levelNumber, double verticalOffset = 0.0)
        {
            Node? existing = FindNodeAt(x, y, levelNumber, verticalOffset);
            if (existing is not null)
                return existing;

            var node = new Node(NextNodeNumber(), x, y, levelNumber, verticalOffset);
            Nodes.Add(node);
            return node;
        }

        /// <summary>One past the highest node number in use, or 1 for an empty model.</summary>
        public int NextNodeNumber()
        {
            int highest = 0;
            foreach (var node in Nodes)
                if (node.NodeNumber > highest)
                    highest = node.NodeNumber;

            return highest + 1;
        }

        private double GetLevelElevation(int levelNumber)
        {
            Level? level = Levels.Find(l => l.LevelNumber == levelNumber);
            if (level is null)
                throw new InvalidOperationException($"Unknown level {levelNumber}.");

            return level.AbsoluteElevation;
        }

        /// <summary>
        /// Resolves a node to absolute coordinates. False when its level is unknown,
        /// which <see cref="Validate()"/> reports separately.
        /// </summary>
        private bool TryGetAbsolutePoint(Node node, out double x, out double y, out double z)
        {
            x = y = z = 0.0;

            Level? level = Levels.Find(l => l.LevelNumber == node.LevelNumber);
            if (level is null)
                return false;

            x = node.X;
            y = node.Y;
            z = level.AbsoluteElevation + node.VerticalOffset;
            return true;
        }
    }
}
