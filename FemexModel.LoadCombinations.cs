using griffel_femex.Loads.Combinations;

namespace griffel_femex
{
    /// <summary>
    /// Load combination lookup and the two rules the format would otherwise state
    /// only in prose: which combinations a design envelope is taken over, and what
    /// a load case's total factor in a combination is when its terms repeat.
    /// Both live here so that a consumer and the format cannot disagree about them.
    ///
    /// Every member is a lookup — none of them adds anything to the model, and
    /// there is no combination builder or code generator. Unknown ids are answered
    /// with null or nothing rather than an exception.
    /// </summary>
    public partial class FemexModel
    {
        /// <summary>
        /// The combinations a design check of this limit state should envelope:
        /// those of that limit state with <see cref="LoadCombination.IncludeInDesignEnvelope"/>
        /// set, in list order.
        ///
        /// Combinations are enveloped within their own limit state, never across
        /// two of them — an envelope of strength and deflection results together
        /// would mean nothing. A limit state nothing uses envelopes to an empty
        /// sequence rather than to everything.
        /// </summary>
        public IEnumerable<LoadCombination> GetDesignEnvelope(LimitState limitState)
        {
            var combinations = new List<LoadCombination>();
            foreach (var combination in LoadCombinations)
                if (combination.LimitState == limitState && combination.IncludeInDesignEnvelope)
                    combinations.Add(combination);

            return combinations;
        }

        /// <summary>The combination with this number, or null if there is none.</summary>
        public LoadCombination? FindLoadCombination(int number)
        {
            return LoadCombinations.Find(c => c.Number == number);
        }

        /// <summary>
        /// What a load case is factored by in a combination, all told: terms
        /// naming the same case add, so a combination carrying 0.9 and 0.5 of case
        /// 1 factors it by 1.4.
        ///
        /// Zero when the case takes no part in the combination — and equally when
        /// the combination itself is unknown, which is the cost of answering by
        /// number rather than by entity.
        /// </summary>
        public double GetTotalFactor(int combinationNumber, int loadCaseNumber)
        {
            LoadCombination? combination = FindLoadCombination(combinationNumber);
            if (combination is null)
                return 0.0;

            double total = 0.0;
            foreach (var term in combination.Terms)
                if (term.LoadCaseNumber == loadCaseNumber)
                    total += term.Factor;

            return total;
        }

        /// <summary>One past the highest combination number in use, or 1 for an empty model.</summary>
        public int NextLoadCombinationNumber()
        {
            int highest = 0;
            foreach (var combination in LoadCombinations)
                if (combination.Number > highest)
                    highest = combination.Number;

            return highest + 1;
        }
    }
}
