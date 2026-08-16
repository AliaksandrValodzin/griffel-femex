namespace griffel_femex.Loads
{
    /// <summary>
    /// A distinct group of loads that act together under one environmental or usage
    /// condition, referenced by <see cref="Load.LoadCaseNumber"/> and factored by
    /// <see cref="Combinations.LoadCombination"/>.
    ///
    /// A case is more than its entries in the model's load list: it also says, in
    /// <see cref="SelfWeightFactor"/>, how much of the structure's own weight acts
    /// in it. A case with no loads of its own is therefore not an empty case.
    /// </summary>
    public class LoadCase
    {
        public int Number { get; set; }

        public string? Label { get; set; }

        public LoadNature Nature { get; set; }

        /// <summary>
        /// How much of the structure's own weight acts in this case. <c>1.0</c> is
        /// normal gravity along the model's <see cref="FemexModel.Gravity"/> vector;
        /// <c>0</c> — the default — is none. Dimensionless, and multiplied into a
        /// weight the model computes for itself from each material's density and
        /// each element's section or thickness.
        ///
        /// Written on every load case in every file, never omitted:
        /// <c>"selfWeightFactor": 0</c> is the positive statement "no self-weight
        /// here", and a case that simply left it out would be silent about the one
        /// thing this field exists to settle.
        ///
        /// FEMEX reserves no self-weight case and gives <see cref="LoadNature.Dead"/>
        /// no special meaning: any case may carry the factor, and more than one doing
        /// so is legal but warned about, since a combination naming two of them
        /// counts the weight twice.
        ///
        /// A case's own loads are <b>additional to</b> its self-weight, never a
        /// substitute for it. An author-computed area load standing in for the slab's
        /// weight in a case that also carries a factor is applied on top of it.
        /// </summary>
        public double SelfWeightFactor { get; set; }

        public LoadCase(int number, string? label, LoadNature nature)
        {
            Number = number;
            Label = label;
            Nature = nature;
        }

        public LoadCase(int number, string? label, LoadNature nature, double selfWeightFactor)
            : this(number, label, nature)
        {
            SelfWeightFactor = selfWeightFactor;
        }

        // Default constructor for serialization or simple instantiation
        public LoadCase() { }

        public override string ToString()
        {
            return $"[{Number}] {Label} ({Nature})";
        }
    }
}
