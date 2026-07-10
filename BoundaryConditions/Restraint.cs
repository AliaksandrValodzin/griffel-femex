namespace griffel_femex.BoundaryConditions
{
    /// <summary>
    /// The restraint of a single degree of freedom.
    /// Captures "infinite or finite stiffness":
    ///  - Fixed = true  -> infinite stiffness (fully restrained)
    ///  - Fixed = false + Stiffness == null -> free
    ///  - Fixed = false + Stiffness has value -> finite (spring) stiffness
    /// </summary>
    public class Restraint
    {
        // Infinite stiffness (fully restrained)
        public bool Fixed { get; set; }

        // Finite spring stiffness; null = free (only meaningful when Fixed is false)
        public double? Stiffness { get; set; }

        public Restraint() { }

        public Restraint(bool @fixed, double? stiffness = null)
        {
            Fixed = @fixed;
            Stiffness = stiffness;
        }

        // Convenience factories
        public static Restraint FixedDof() => new Restraint(true);
        public static Restraint Free() => new Restraint(false);
        public static Restraint Spring(double stiffness) => new Restraint(false, stiffness);
    }
}
