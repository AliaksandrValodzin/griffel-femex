namespace griffel_femex.Loads
{
    /// <summary>
    /// Which axis of the load's <see cref="LoadCoordinateSystem"/> a distributed
    /// load acts along. The sign lives in the magnitude, not here: with the
    /// default <see cref="Z"/> in global coordinates, a gravity load is a negative
    /// magnitude. There is deliberately no <c>Gravity</c> value — it would give the
    /// format two ways to say "down" and make the sign of a magnitude depend on
    /// which one the author picked.
    /// </summary>
    public enum LoadDirection
    {
        X,
        Y,

        /// <summary>The default. Global Z is up, so gravity is a negative magnitude.</summary>
        Z,

        /// <summary>
        /// An arbitrary direction, given by <see cref="DistributedLoad.Dx"/>,
        /// <see cref="DistributedLoad.Dy"/> and <see cref="DistributedLoad.Dz"/>.
        /// Those three are read only for this value and must not be set for any
        /// other, which <c>Validate()</c> reports.
        /// </summary>
        Vector,
    }
}
