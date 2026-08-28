using System;
using System.Collections.Generic;
using System.Globalization;
using griffel_femex.Geometry;
using SAF.DataAccess.Models.Enums;

namespace griffel_femex.Adapters.Saf
{
    /// <summary>
    /// P2, at the boundary: a station along a member, converted to FEMEX's canonical
    /// form — relative, from the start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAF states a station four ways: <c>Coordinate definition</c> is relative or
    /// absolute and <c>Origin</c> is from the start or from the end, and the
    /// reference workbook uses all four combinations. FEMEX 1.9 and 1.10 chose one —
    /// relative from the start — and this is where the other three become it.
    /// </para>
    /// <para>
    /// The SDK types the cell as <c>object</c> because its meaning depends on the
    /// column beside it: a bare <see cref="double"/> when the definition is
    /// relative, a UnitsNet <c>Length</c> when it is absolute. Reading it as one or
    /// the other without checking is the shape of bug that puts a load at 1.5 of the
    /// way along a 1.5-metre member.
    /// </para>
    /// <para>
    /// The absolute conversion is exact on a straight member and approximate on a
    /// chorded arc, because the chord length is not the arc length. That is the one
    /// class of position error this adapter cannot remove, and the <c>chorded</c>
    /// flag is how it gets reported instead.
    /// </para>
    /// </remarks>
    internal static class SafPosition
    {
        public static double? Relative(object? station, ExcelCoordinateDefinition? definition,
                                       ExcelOrigin? origin, double length, out bool chorded)
        {
            chorded = false;
            if (station is null)
                return null;

            double value;
            if (station is UnitsNet.Length absolute)
            {
                if (length <= 0.0)
                    return null;

                value = absolute.Meters / length;
                chorded = true;
            }
            else if (definition == ExcelCoordinateDefinition.Absolute)
            {
                if (length <= 0.0 || !TryNumber(station, out double metres))
                    return null;

                value = metres / length;
                chorded = true;
            }
            else if (!TryNumber(station, out value))
            {
                return null;
            }

            if (origin == ExcelOrigin.FromEnd)
                value = 1.0 - value;

            return value;
        }

        /// <summary>
        /// True where the station pair covers less than the whole member or edge —
        /// the case FEMEX's whole-edge hinge cannot hold.
        /// </summary>
        public static bool IsPartial(object? start, object? end, ExcelCoordinateDefinition? definition)
        {
            if (definition == ExcelCoordinateDefinition.Absolute || start is UnitsNet.Length ||
                end is UnitsNet.Length)
            {
                return true;
            }

            bool fromZero = start is null || (TryNumber(start, out double s) && Math.Abs(s) < 1e-12);
            bool toOne = end is null || (TryNumber(end, out double e) && Math.Abs(e - 1.0) < 1e-12);
            return !(fromZero && toOne);
        }

        private static bool TryNumber(object value, out double number)
        {
            switch (value)
            {
                case double d: number = d; return true;
                case float f: number = f; return true;
                case decimal m: number = (double)m; return true;
                case int i: number = i; return true;
                case UnitsNet.Length length: number = length.Meters; return true;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture,
                                                   out double parsed):
                    number = parsed;
                    return true;
                default:
                    number = 0.0;
                    return false;
            }
        }
    }

    /// <summary>
    /// Absolute point geometry for a model whose nodes state a level and an offset
    /// rather than a Z.
    /// </summary>
    /// <remarks>
    /// §1.1's structural difference, met at the point where it costs something: to
    /// turn an absolute station along a member into a relative one, the adapter needs
    /// the member's length, and to get that it needs each node's Z — which FEMEX
    /// holds as its level's elevation plus the node's own offset.
    /// </remarks>
    internal sealed class SafGeometry
    {
        private readonly Dictionary<int, double> _elevations = new Dictionary<int, double>();
        private readonly Dictionary<int, Node> _nodes = new Dictionary<int, Node>();

        public SafGeometry(FemexModel model)
        {
            foreach (Level level in model.Levels)
                _elevations[level.LevelNumber] = level.AbsoluteElevation;

            foreach (Node node in model.Nodes)
                _nodes[node.NodeNumber] = node;
        }

        public double LengthOf(Bar bar)
        {
            if (!_nodes.TryGetValue(bar.StartNodeId, out Node? start) ||
                !_nodes.TryGetValue(bar.EndNodeId, out Node? end))
            {
                return 0.0;
            }

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dz = ZOf(end) - ZOf(start);
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double ZOf(Node node)
        {
            _elevations.TryGetValue(node.LevelNumber, out double elevation);
            return elevation + node.VerticalOffset;
        }
    }
}
