namespace griffel_femex.Loads
{
    /// <summary>
    /// The orientation an area or line load carries, defined once for both. A
    /// magnitude on its own says how much and never which way, and none of the
    /// programs FEMEX exports to can guess; these fields are the three orthogonal
    /// facts they need, factored the way SAF factors them rather than fused into
    /// one enum the way RFEM and ETABS do.
    ///
    /// Two rules a consumer would otherwise get wrong:
    ///
    /// <list type="number">
    /// <item>
    /// The <b>force</b> acts along the resolved axis; the <b>moment</b>
    /// (<see cref="LinearLoad.MomentStart"/> / <see cref="LinearLoad.MomentEnd"/>)
    /// acts <i>about that same axis</i>, right-hand rule. One direction serves
    /// both, so a torsional line load on a beam is <see cref="LoadDirection.X"/>
    /// in <see cref="LoadCoordinateSystem.Local"/> with only the moment terms set.
    /// </item>
    /// <item>
    /// <see cref="Projected"/> means the magnitude is per unit of the loaded
    /// geometry <i>projected onto the plane perpendicular to the load direction</i>
    /// — so the total force is magnitude × projected extent, not magnitude × real
    /// extent. This is the snow-versus-dead-load distinction on a pitched roof, and
    /// the two agree only when the load is perpendicular to the loaded surface.
    /// </item>
    /// </list>
    ///
    /// <c>FemexModel.TryGetLoadDirection</c> resolves all of this into one global
    /// unit vector, so a consumer and the format cannot disagree about it.
    /// </summary>
    public abstract class DistributedLoad : Load
    {
        /// <summary>
        /// Whether <see cref="Direction"/> names a global axis or an axis of the
        /// element the load sits on. Global by default.
        /// </summary>
        public LoadCoordinateSystem CoordinateSystem { get; set; } = LoadCoordinateSystem.Global;

        /// <summary>
        /// Which axis the load acts along. Global Z by default, so an unstated
        /// gravity load is a negative magnitude.
        /// </summary>
        // A property initializer, not an enum-member ordering trick:
        // System.Text.Json leaves a property untouched when its key is absent, so
        // this is what a file written before load directions existed reads back as.
        public LoadDirection Direction { get; set; } = LoadDirection.Z;

        /// <summary>
        /// The direction vector's components, in the coordinate system named by
        /// <see cref="CoordinateSystem"/>. Read only when <see cref="Direction"/> is
        /// <see cref="LoadDirection.Vector"/>, need not be normalized, and omitted
        /// from the JSON otherwise.
        /// </summary>
        public double? Dx { get; set; }

        /// <inheritdoc cref="Dx"/>
        public double? Dy { get; set; }

        /// <inheritdoc cref="Dx"/>
        public double? Dz { get; set; }

        /// <summary>
        /// True when the magnitude is per unit of <i>projected</i> extent rather
        /// than of real extent. Only meaningful for a global direction: none of the
        /// target programs has a projected local variant, and a local direction is
        /// already defined relative to the surface being projected.
        /// </summary>
        public bool Projected { get; set; }
    }
}
