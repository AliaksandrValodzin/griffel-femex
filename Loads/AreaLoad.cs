namespace griffel_femex.Loads
{
    /// <summary>
    /// An area load (pressure) applied either over a whole plate — optionally
    /// narrowed to one of its regions — or over a free polygon of nodes.
    /// Exactly one of those two targeting forms must be used.
    ///
    /// A plate-targeted load follows the plate when its geometry is edited or
    /// re-meshed; it is not applied over regions whose kind is Opening.
    ///
    /// Which way the pressure acts comes from <see cref="DistributedLoad"/>. The
    /// host whose local axes a <see cref="LoadCoordinateSystem.Local"/> direction
    /// resolves against is the targeted plate, or the free polygon itself — the
    /// same contour rule with a local-axis angle of zero.
    /// </summary>
    public class AreaLoad : DistributedLoad
    {
        // References Plate.Id. The whole-plate targeting form.
        public int? PlateId { get; set; }

        // References PlateRegion.Id within PlateId; null = the whole plate.
        // Only meaningful together with PlateId.
        public int? RegionId { get; set; }

        // The free-polygon targeting form: a sequence of nodes bounding the loaded
        // patch (references Node.NodeNumber). Null when PlateId is used.
        public List<int>? NodeSequence { get; set; }

        /// <summary>
        /// Magnitude of the pressure load (Force per unit area).
        /// </summary>
        public double Magnitude { get; set; }
    }
}
