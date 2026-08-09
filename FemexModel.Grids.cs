using griffel_femex.Geometry;
using griffel_femex.Geometry.Grids;

namespace griffel_femex
{
    /// <summary>
    /// Grid lookup, the grid-local to model-plan transform, and the snapping
    /// primitives built on them. Grids are annotation — nothing here changes an
    /// analysis — but they are what geometry should be authored from, so that a
    /// node placed on grid A/1 lands exactly where the setting-out drawing says
    /// and, being placed through <see cref="GetOrAddNode"/>, shares whatever node
    /// is already there.
    /// </summary>
    public partial class FemexModel
    {
        /// <summary>
        /// Two directions count as parallel when the sine of the angle between
        /// them falls below this. Dimensionless, so unlike the coincidence
        /// tolerance it needs no scaling to the model's extent.
        /// </summary>
        private const double ParallelDirectionTolerance = RelativeGeometricTolerance;

        /// <summary>
        /// The grids a level is actually set out on, applying
        /// <see cref="Level.GridIds"/>'s three-way rule: null inherits
        /// <see cref="DefaultGridIds"/>, an empty list means no grid, and a
        /// non-empty one replaces the default rather than adding to it.
        ///
        /// Ids that do not resolve are skipped — <see cref="Validate()"/> reports
        /// them — so this returns the grids that exist, in the order named.
        /// </summary>
        /// <exception cref="InvalidOperationException">The level does not exist.</exception>
        public IEnumerable<Grid> GetGridsForLevel(int levelNumber)
        {
            Level? level = Levels.Find(l => l.LevelNumber == levelNumber);
            if (level is null)
                throw new InvalidOperationException($"Unknown level {levelNumber}.");

            List<int> ids = level.GridIds ?? DefaultGridIds;

            var grids = new List<Grid>();
            foreach (int id in ids)
            {
                Grid? grid = FindGrid(id);
                if (grid is not null)
                    grids.Add(grid);
            }

            return grids;
        }

        /// <summary>The grid with this id, or null if there is none.</summary>
        public Grid? FindGrid(int gridId)
        {
            return Grids.Find(g => g.Id == gridId);
        }

        /// <summary>
        /// The line with this label in this grid, or null if either is unknown.
        /// Labels are compared ordinally, so "a" and "A" are two lines.
        /// </summary>
        public Gridline? FindGridline(int gridId, string label)
        {
            Grid? grid = FindGrid(gridId);
            return grid?.Lines.Find(l => string.Equals(l.Label, label, StringComparison.Ordinal));
        }

        /// <summary>
        /// A point in a grid's local coordinates, in the model's plan
        /// coordinates. Rotation is counter-clockwise about global +Z.
        /// </summary>
        public void ToModelPoint(Grid grid, double u, double v, out double x, out double y)
        {
            double radians = grid.RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            x = grid.OriginX + u * cos - v * sin;
            y = grid.OriginY + u * sin + v * cos;
        }

        /// <summary>
        /// The inverse of <see cref="ToModelPoint"/>: a model plan point in a
        /// grid's local coordinates. Useful for asking how far a node sits off a
        /// gridline; the format stores no such relationship.
        /// </summary>
        public void ToGridLocal(Grid grid, double x, double y, out double u, out double v)
        {
            double radians = grid.RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            double ex = x - grid.OriginX;
            double ey = y - grid.OriginY;

            u = ex * cos + ey * sin;
            v = -ex * sin + ey * cos;
        }

        /// <summary>
        /// Where two of a grid's labelled lines cross, in the model's plan
        /// coordinates — the point to place a node at when snapping to the grid.
        ///
        /// False when either label is unknown, when a line is degenerate, or when
        /// the two are parallel and so never cross. Two lines of the same
        /// <see cref="GridDirection"/> are always parallel.
        /// </summary>
        public bool TryGetIntersection(int gridId, string labelA, string labelB, out double x, out double y)
        {
            x = y = 0.0;

            Grid? grid = FindGrid(gridId);
            if (grid is null)
                return false;

            Gridline? a = grid.Lines.Find(l => string.Equals(l.Label, labelA, StringComparison.Ordinal));
            Gridline? b = grid.Lines.Find(l => string.Equals(l.Label, labelB, StringComparison.Ordinal));
            if (a is null || b is null)
                return false;

            double tolerance = GetCoincidenceTolerance();
            if (!TryGetLocalRay(a, tolerance, out double ax, out double ay, out double adx, out double ady) ||
                !TryGetLocalRay(b, tolerance, out double bx, out double by, out double bdx, out double bdy))
                return false;

            // Both directions are unit length, so the cross product is the sine of
            // the angle between them and the test is a true angular one.
            double cross = adx * bdy - ady * bdx;
            if (Math.Abs(cross) < ParallelDirectionTolerance)
                return false;

            double t = ((bx - ax) * bdy - (by - ay) * bdx) / cross;
            ToModelPoint(grid, ax + t * adx, ay + t * ady, out x, out y);
            return true;
        }

        /// <summary>
        /// The node where two of a grid's lines cross on a level, adding one if
        /// it is not there yet. The authoring counterpart of
        /// <see cref="TryGetIntersection"/>: it goes through
        /// <see cref="GetOrAddNode"/>, so geometry set out from a grid shares the
        /// nodes it meets rather than stacking new ones on them.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The grid, either label or the level does not exist, or the two lines
        /// are parallel and never cross.
        /// </exception>
        public Node GetOrAddNodeAtGrid(int gridId, string labelA, string labelB, int levelNumber, double verticalOffset = 0.0)
        {
            if (FindGrid(gridId) is null)
                throw new InvalidOperationException($"Unknown grid {gridId}.");

            if (FindGridline(gridId, labelA) is null)
                throw new InvalidOperationException($"Grid {gridId} has no line labelled \"{labelA}\".");

            if (FindGridline(gridId, labelB) is null)
                throw new InvalidOperationException($"Grid {gridId} has no line labelled \"{labelB}\".");

            if (!TryGetIntersection(gridId, labelA, labelB, out double x, out double y))
                throw new InvalidOperationException(
                    $"Grid {gridId} lines \"{labelA}\" and \"{labelB}\" do not cross.");

            return GetOrAddNode(x, y, levelNumber, verticalOffset);
        }

        /// <summary>One past the highest grid id in use, or 1 for an empty model.</summary>
        public int NextGridId()
        {
            int highest = 0;
            foreach (var grid in Grids)
                if (grid.Id > highest)
                    highest = grid.Id;

            return highest + 1;
        }

        /// <summary>
        /// A gridline as a point and a unit direction in grid-local coordinates.
        /// False for a free line whose two points are coincident, which defines no
        /// direction and which <see cref="Validate()"/> reports.
        /// </summary>
        private static bool TryGetLocalRay(
            Gridline line, double tolerance, out double px, out double py, out double dx, out double dy)
        {
            px = py = dx = dy = 0.0;

            switch (line)
            {
                case OrthogonalGridline orthogonal:
                    if (orthogonal.Direction == GridDirection.Y)
                    {
                        px = orthogonal.Offset;
                        dy = 1.0;
                    }
                    else
                    {
                        py = orthogonal.Offset;
                        dx = 1.0;
                    }

                    return true;

                case FreeGridline free:
                    double ex = free.X2 - free.X1;
                    double ey = free.Y2 - free.Y1;
                    double length = Math.Sqrt(ex * ex + ey * ey);
                    if (length <= tolerance)
                        return false;

                    px = free.X1;
                    py = free.Y1;
                    dx = ex / length;
                    dy = ey / length;
                    return true;

                default:
                    return false;
            }
        }
    }
}
