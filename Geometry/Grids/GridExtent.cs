using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Grids
{
    /// <summary>
    /// How far a viewer draws a <see cref="Grid"/>'s lines, and where it places
    /// their label bubbles. A rectangle in the grid's own local coordinates.
    ///
    /// This is a drawing hint and nothing more. Gridlines are mathematically
    /// infinite in plan, so an extent never limits snapping, never clips a line
    /// for the purposes of an intersection, and never affects validation of
    /// anything but itself. It is normally larger than the model's bounding box,
    /// because a grid is drawn running past the building it sets out.
    ///
    /// When a grid has no extent at all, a viewer falls back to the model's own
    /// bounds.
    /// </summary>
    public class GridExtent : IExtensible
    {
        // Grid-local coordinates. MinX must be less than MaxX, MinY than MaxY.
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }

        // Parameterless constructor for serialization
        public GridExtent() { }

        // Convenience constructor
        public GridExtent(double minX, double maxX, double minY, double maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
