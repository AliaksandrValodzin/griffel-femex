namespace griffel_femex.Comparison
{
    /// <summary>
    /// What <see cref="ModelDiff"/> is allowed to call the same.
    ///
    /// The defaults are §7.2's equivalence exactly: geometry within the coincidence
    /// tolerance, everything else identical. The knobs exist because §7.2 was
    /// written about a round trip through a native program, and a program that
    /// rounds a modulus of elasticity to seven figures has produced a difference the
    /// definition does not name — a caller comparing across such a boundary loosens
    /// this deliberately rather than by editing the comparison.
    /// </summary>
    public sealed class ModelDiffOptions
    {
        /// <summary>
        /// How far apart two coordinates may be and still be the same place. Null
        /// derives it as the larger of the two models'
        /// <see cref="FemexModel.GetCoincidenceTolerance"/>, which is the value
        /// §7.2 names.
        /// </summary>
        public double? GeometricTolerance { get; set; }

        /// <summary>
        /// The relative slack allowed on every other number — magnitudes, moduli,
        /// factors, angles. <b>Zero by default, meaning identical</b>, because §7.2
        /// grants tolerance to geometry and to nothing else, and a diff that quietly
        /// rounds load magnitudes together is the confidently-wrong answer the
        /// product exists to catch.
        /// </summary>
        public double RelativeTolerance { get; set; }

        /// <summary>
        /// Whether a member this build has no property for — <c>IExtensible</c>'s
        /// preserved payload — counts. It does by default: a member that survived
        /// one crossing and not the other is precisely the
        /// <see cref="Interop.LossCategory.Stale"/> loss extension data exists to
        /// make visible.
        /// </summary>
        public bool CompareUnknownMembers { get; set; } = true;

        /// <summary>
        /// Whether <see cref="FemexModel.Metadata"/> counts. It does not by default:
        /// it is who wrote the file and when, so a round trip is <i>supposed</i> to
        /// change it, and comparing it would make every round trip differ for a
        /// reason no adapter should have to declare.
        /// </summary>
        public bool CompareMetadata { get; set; }

        /// <summary>
        /// Whether <see cref="FemexModel.Mesh"/> counts. It does not by default, for
        /// the same reason <c>EnumerateIdentified</c> leaves it out: it is
        /// regenerated wholesale, so it has no identity to compare and its absence
        /// after a crossing is not a loss.
        /// </summary>
        public bool CompareMesh { get; set; }
    }
}
