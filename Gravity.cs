namespace griffel_femex
{
    /// <summary>
    /// Which way gravity acts in this model, and how strong it is. Stated once, on
    /// the root, and read only by self-weight: no <see cref="Loads.Load"/> can name
    /// it, so the one way to point a distributed load is still its own
    /// CoordinateSystem × Direction × Projected.
    ///
    /// Written on every model rather than omitted when it is the default, because a
    /// file that does not say which way down is is the problem this block exists to
    /// fix. RFEM's global Z points down by default, which is the commonest source of
    /// translator bugs against a Z-up format; a translator from it writes
    /// <c>(0, 0, -1)</c> here, and the trap is stated rather than inherited.
    ///
    /// <c>FemexModel.GetGravityDirection</c> is the one place the direction is read.
    /// </summary>
    public class Gravity
    {
        // Direction only. Its magnitude is discarded — GetGravityDirection
        // normalizes it — so putting 9.80665 here as well as in Acceleration is
        // harmless, not a gravity of 96.
        public double Dx { get; set; }

        public double Dy { get; set; }

        public double Dz { get; set; } = -1.0;

        /// <summary>
        /// In the model's own length units per second squared: 9.80665 for a metre
        /// model, <b>9806.65 for a millimetre one</b>. The default is metre-specific,
        /// and a millimetre model that accepts it is 1000x light.
        ///
        /// Only how strong gravity is; which way it acts is
        /// <see cref="Dx"/>/<see cref="Dy"/>/<see cref="Dz"/>'s job, so a negative
        /// value here is not "downward" but an error
        /// <see cref="FemexModel.Validate()"/> reports.
        /// </summary>
        public double Acceleration { get; set; } = 9.80665;

        // Parameterless constructor for serialization
        public Gravity() { }

        // Convenience constructor
        public Gravity(double dx, double dy, double dz, double acceleration)
        {
            Dx = dx;
            Dy = dy;
            Dz = dz;
            Acceleration = acceleration;
        }
    }
}
