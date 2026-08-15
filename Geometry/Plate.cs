namespace griffel_femex.Geometry
{
    /// <summary>
    /// An authored design panel: one closed outer contour plus any number of
    /// subregions that override thickness and material over part of it, or punch
    /// holes in it. A slab with voids, drop panels and thickness zones is one Plate;
    /// so is a shear wall with door and window openings.
    ///
    /// A Plate is not itself a finite element — the elements representing it live in
    /// <see cref="griffel_femex.Mesh.FemexMesh"/> and back-link here.
    ///
    /// <b>Local axes</b>, in the right-handed Z-up global frame:
    ///
    /// <list type="bullet">
    /// <item>local <b>z</b> = the outer contour's normal, by Newell's method over
    /// <see cref="NodeIds"/> in order, right-hand rule: a contour that is
    /// counter-clockwise seen from above has normal +Z.</item>
    /// <item>local <b>x</b> = the <c>NodeIds[0] → NodeIds[1]</c> chord projected
    /// into the plane, then rotated by <see cref="LocalAxisAngle"/> about local z,
    /// counter-clockwise seen from +z — the same sign convention
    /// <c>Grid.RotationAngle</c> uses.</item>
    /// <item>local <b>y</b> = ẑ × x̂.</item>
    /// </list>
    ///
    /// A free-polygon <see cref="griffel_femex.Loads.AreaLoad"/> uses the same rule
    /// over its own node sequence with an angle of zero.
    /// <c>FemexModel.TryGetPlateLocalAxes</c> is this rule, executable.
    /// </summary>
    public class Plate : Element
    {
        public string? Name { get; set; }

        // Outer contour. References Node.NodeNumber; order matters, segments are
        // straight, the contour closes implicitly (first node is not repeated).
        // Curved edges are approximated by chords.
        public List<int> NodeIds { get; set; } = new List<int>();

        // What the panel contributes outside every subregion.
        public PlateRegionKind Kind { get; set; } = PlateRegionKind.Structural;

        // References SurfaceProperty.Id. Null only when Kind is Opening.
        public int? SurfacePropertyId { get; set; }

        // References Material.Id. Null only when Kind is Opening.
        public int? MaterialId { get; set; }

        // Which degrees of freedom the panel activates in analysis.
        public PlateBehaviour Behaviour { get; set; } = PlateBehaviour.Shell;

        // Which face of the panel the contour nodes lie on.
        public SurfaceAlignment Alignment { get; set; } = SurfaceAlignment.Centre;

        // Extra offset of the analysis reference surface from the aligned face,
        // measured along the plate normal (local +Z) — never along global Z. For a
        // wall this therefore moves the panel horizontally.
        public double SurfaceOffset { get; set; }

        // Rotation of the local X-axis about the plate normal (local +Z), in
        // degrees, counter-clockwise seen from +Z. Unrotated local X runs from the
        // first contour node to the second. The plate counterpart of
        // Bar.RotationAngle, which rolls about the bar's own axis.
        public double LocalAxisAngle { get; set; }

        // Subregions, resolved against the base panel by priority. See PlateRegion.
        public List<PlateRegion> Regions { get; set; } = new List<PlateRegion>();

        // Parameterless constructor for serialization
        public Plate() { }

        // Convenience constructor
        public Plate(int id, List<int> nodeIds, int? surfacePropertyId, int? materialId,
                     PlateRegionKind kind = PlateRegionKind.Structural)
        {
            Id = id;
            NodeIds = nodeIds;
            SurfacePropertyId = surfacePropertyId;
            MaterialId = materialId;
            Kind = kind;
        }

        /// <summary>
        /// Every node the plate depends on: the outer contour followed by each
        /// region contour, in order.
        /// </summary>
        public override IEnumerable<int> GetNodeIds()
        {
            foreach (int nodeId in NodeIds)
                yield return nodeId;

            foreach (PlateRegion region in Regions)
                foreach (int nodeId in region.NodeIds)
                    yield return nodeId;
        }
    }
}
