namespace griffel_femex.Loads
{
    /// <summary>
    /// Which frame a distributed load's <see cref="LoadDirection"/> is measured in.
    /// One of the three independent facts a receiving program needs and cannot
    /// guess — on a wall, "down" and "normal to the surface" are entirely
    /// different loads.
    /// </summary>
    public enum LoadCoordinateSystem
    {
        /// <summary>
        /// The model's global frame: right-handed, Z up. The default, so a load
        /// that says nothing acts along global Z and a gravity load is negative.
        /// </summary>
        Global,

        /// <summary>
        /// The local frame of the element the load sits on — the plate for an
        /// <see cref="AreaLoad"/>, the bar named by <see cref="LinearLoad.BarId"/>
        /// for a line load. The conventions are stated on
        /// <see cref="griffel_femex.Geometry.Bar"/> and
        /// <see cref="griffel_femex.Geometry.Plate"/> and made executable by
        /// <c>FemexModel.TryGetBarLocalAxes</c> / <c>TryGetPlateLocalAxes</c>.
        /// </summary>
        Local,
    }
}
