namespace griffel_femex.Geometry
{
    /// <summary>
    /// Which way a panel carries the load applied to it — SAF's
    /// <c>StructuralSurfaceActionDistribution.Distribution to</c>, whose three values
    /// (<c>Two way</c>, <c>One way - X</c>, <c>One way - Y</c>) are all exercised in
    /// the reference workbook (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.10).
    ///
    /// <b>It lives on the panel, not on the load</b>, and that is the whole design.
    /// A slab spans one way for every load on it; two loads on one panel must not be
    /// able to disagree about how it spans, and if the spanning direction were a
    /// property of the load they could. See <see cref="LoadDistribution"/>.
    ///
    /// The axes are the panel's own local x and y — <c>Plate.LocalAxisAngle</c>
    /// included, and then rotated again by
    /// <see cref="LoadDistribution.RotationAngle"/>. <c>FemexModel.TryGetPlateLocalAxes</c>
    /// is that frame, executable.
    /// </summary>
    public enum SurfaceLoadSpanning
    {
        /// <summary>
        /// The panel carries load in both directions, as a plate. What a panel
        /// stating no distribution at all means.
        /// </summary>
        TwoWay,

        /// <summary>Load is carried along the panel's local x only.</summary>
        OneWayX,

        /// <summary>Load is carried along the panel's local y only.</summary>
        OneWayY,
    }
}
