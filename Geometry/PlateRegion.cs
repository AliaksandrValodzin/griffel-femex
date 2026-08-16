namespace griffel_femex.Geometry
{
    /// <summary>
    /// A subregion of a plate: a closed contour that overrides the panel's thickness
    /// and/or material over part of its area, or punches a hole in it.
    ///
    /// Regions may overlap each other and may hang over the plate's outer contour;
    /// the overhanging part is clipped away. At every point inside the outer contour
    /// the governing region is chosen deterministically:
    ///   1. the highest <see cref="Priority"/> wins — the base panel behaves as
    ///      int.MinValue, so any region beats it;
    ///   2. on a tie, Opening beats LoadOnly beats Structural;
    ///   3. on a further tie, the region later in <see cref="Plate.Regions"/> wins.
    ///
    /// A region takes the plate's surface property and material wherever it leaves
    /// its own null. <c>FemexModel.GetEffectiveProperties</c> is that inheritance
    /// rule, executable, and both validation and the self-weight helpers read it
    /// through there so they cannot disagree about it.
    /// </summary>
    public class PlateRegion : IIdentified
    {
        // Unique within the owning plate only. Loads, hinges and mesh faces address
        // a region as the pair (Plate.Id, PlateRegion.Id).
        public int Id { get; set; }

        // Optional round-trip identity, unique across the whole model even though
        // Id is not. See IIdentified.
        public Guid? Uid { get; set; }

        public string? Name { get; set; }

        // Region contour. References Node.NodeNumber; order matters, segments are
        // straight, the contour closes implicitly (first node is not repeated).
        public List<int> NodeIds { get; set; } = new List<int>();

        public PlateRegionKind Kind { get; set; } = PlateRegionKind.Structural;

        // References SurfaceProperty.Id. Null = inherit the plate's value;
        // must be null when Kind is Opening.
        public int? SurfacePropertyId { get; set; }

        // References Material.Id. Null = inherit the plate's value;
        // must be null when Kind is Opening.
        public int? MaterialId { get; set; }

        // Higher wins where regions overlap. See the resolution rule above.
        public int Priority { get; set; }

        // Null = inherit the plate's Alignment.
        public SurfaceAlignment? Alignment { get; set; }

        // Null = inherit the plate's SurfaceOffset. Measured along the plate normal,
        // so a drop panel hanging below a slab soffit is negative.
        public double? SurfaceOffset { get; set; }

        // Parameterless constructor for serialization
        public PlateRegion() { }

        // Convenience constructor
        public PlateRegion(int id, List<int> nodeIds, PlateRegionKind kind, int priority = 0)
        {
            Id = id;
            NodeIds = nodeIds;
            Kind = kind;
            Priority = priority;
        }
    }
}
