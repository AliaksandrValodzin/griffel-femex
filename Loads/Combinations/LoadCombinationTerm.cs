namespace griffel_femex.Loads.Combinations
{
    /// <summary>
    /// One load case's contribution to a combination: which case, and at what
    /// factor.
    ///
    /// A term is a value, not an entity. It carries no id, it is owned by exactly
    /// one combination, and it is addressed only by its position in
    /// <see cref="LoadCombination.Terms"/>. A term can only name a load case,
    /// never another combination — the structure is deliberately flat.
    /// </summary>
    public class LoadCombinationTerm
    {
        // References LoadCase.Number
        public int LoadCaseNumber { get; set; }

        // Dimensionless multiplier applied to the whole case.
        public double Factor { get; set; }

        // Parameterless constructor for serialization
        public LoadCombinationTerm() { }

        // Convenience constructor
        public LoadCombinationTerm(int loadCaseNumber, double factor)
        {
            LoadCaseNumber = loadCaseNumber;
            Factor = factor;
        }

        public override string ToString()
        {
            return $"{Factor} x case {LoadCaseNumber}";
        }
    }
}
