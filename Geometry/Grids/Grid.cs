using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Grids
{
    /// <summary>
    /// A named set of architectural gridlines in plan — the setting out a model's
    /// geometry is snapped to and a site locates elements from.
    ///
    /// A grid is annotation, not structure: it holds no material, generates no
    /// element and contributes nothing to an analysis. It is also not a level. It
    /// has no elevation and no vertical extent; which levels use it is stated the
    /// other way round, by <see cref="Level.GridIds"/> and
    /// <see cref="FemexModel.DefaultGridIds"/>, so one grid can serve a whole
    /// building and a single level can carry several.
    ///
    /// Every line in <see cref="Lines"/> is expressed in the grid's own local
    /// coordinates; <see cref="OriginX"/>, <see cref="OriginY"/> and
    /// <see cref="RotationAngle"/> place those coordinates in the model's plan.
    /// A rotated wing or a core set out at an angle is therefore one grid with a
    /// rotation, not a set of individually angled lines.
    /// </summary>
    public class Grid : IIdentified, IExtensible
    {
        // Unique identifier for the grid, in its own id space.
        public int Id { get; set; }

        // Optional round-trip identity. Null means this grid has none; see
        // IIdentified. Its lines carry none: a Gridline's identity is its Label,
        // which is required and unique within the grid, and no program keys a
        // gridline by anything else.
        public Guid? Uid { get; set; }

        // Optional human-readable name, e.g. "Primary" or "Core"
        public string? Name { get; set; }

        // Where the grid's local origin sits in the model's plan coordinates.
        public double OriginX { get; set; }
        public double OriginY { get; set; }

        // Rotation of the grid's local axes about global +Z, in degrees
        // counter-clockwise — the same sign convention as Plate.LocalAxisAngle.
        public double RotationAngle { get; set; }

        // Where a viewer stops drawing. Null means it falls back to the model's
        // own bounds; omitted from the JSON entirely while it is.
        public GridExtent? Extent { get; set; }

        // The lines themselves, in grid-local coordinates. Labels are unique
        // within this list.
        public List<Gridline> Lines { get; set; } = new List<Gridline>();

        // Parameterless constructor for serialization
        public Grid() { }

        // Convenience constructor
        public Grid(int id, string? name, double originX = 0.0, double originY = 0.0, double rotationAngle = 0.0)
        {
            Id = id;
            Name = name;
            OriginX = originX;
            OriginY = originY;
            RotationAngle = rotationAngle;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
