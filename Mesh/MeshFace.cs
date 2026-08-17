using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Mesh
{
    /// <summary>
    /// One generated finite element covering part of a plate.
    ///
    /// The back-links (<see cref="PlateId"/>, <see cref="RegionId"/>) are
    /// authoritative. The resolved property fields below are a cache written by the
    /// mesher so that a solver can consume the mesh without re-running the plate's
    /// priority resolution; validation checks only that they reference existing
    /// objects, not that they agree with what resolution would produce.
    /// </summary>
    public class MeshFace : IExtensible
    {
        // Shares the global element-id space with Bar.Id and Plate.Id, so that
        // TemperatureLoad.ElementIds and Hinge.ElementId can address a face.
        public int Id { get; set; }

        // References MeshNode.Id: 3 for a triangle, 4 for a quad.
        // Order defines the face normal.
        public List<int> NodeIds { get; set; } = new List<int>();

        // References Plate.Id — the design panel this face belongs to.
        public int PlateId { get; set; }

        // References PlateRegion.Id within PlateId; null = the base panel.
        public int? RegionId { get; set; }

        // Resolved cache. References SurfaceProperty.Id.
        public int? SurfacePropertyId { get; set; }

        // Resolved cache. References Material.Id.
        public int? MaterialId { get; set; }

        // Resolved scalar thickness at this face — the point at which a variable
        // surface property is materialised.
        public double? Thickness { get; set; }

        // Resolved offset of the reference surface, along the face normal.
        public double SurfaceOffset { get; set; }

        // Parameterless constructor for serialization
        public MeshFace() { }

        // Convenience constructor
        public MeshFace(int id, List<int> nodeIds, int plateId, int? regionId = null)
        {
            Id = id;
            NodeIds = nodeIds;
            PlateId = plateId;
            RegionId = regionId;
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
