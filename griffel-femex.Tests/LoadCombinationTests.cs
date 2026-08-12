using griffel_femex.Loads.Combinations;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The four helpers on <see cref="FemexModel"/>, and with them the two rules
    /// they exist to state once: what a design envelope is taken over, and how a
    /// load case's repeated terms add.
    /// </summary>
    public class LoadCombinationTests
    {
        [Fact]
        public void GetDesignEnvelope_ReturnsCombinationsOfThatLimitStateOnly()
        {
            var model = SampleModels.Build();

            var ultimate = model.GetDesignEnvelope(LimitState.Ultimate).ToList();

            Assert.Equal(
                new List<int> { SampleModels.UltimateCombinationNumber, SampleModels.EnvelopeCombinationNumber },
                ultimate.Select(c => c.Number).ToList());
        }

        [Fact]
        public void GetDesignEnvelope_SkipsExcludedCombinations()
        {
            var model = SampleModels.Build();

            // The sample's only serviceability combination is the excluded one.
            Assert.Empty(model.GetDesignEnvelope(LimitState.Serviceability));

            model.FindLoadCombination(SampleModels.ExcludedCombinationNumber)!.IncludeInDesignEnvelope = true;

            var restored = Assert.Single(model.GetDesignEnvelope(LimitState.Serviceability));
            Assert.Equal(SampleModels.ExcludedCombinationNumber, restored.Number);
        }

        [Fact]
        public void GetDesignEnvelope_IsEmptyForALimitStateNothingUses()
        {
            var model = SampleModels.Build();

            // Empty, not "everything" — a limit state with no combinations has no
            // envelope rather than the whole model's.
            Assert.Empty(model.GetDesignEnvelope(LimitState.Accidental));
        }

        [Fact]
        public void GetTotalFactor_SumsRepeatedTerms()
        {
            var model = SampleModels.Build();
            var combination = model.FindLoadCombination(SampleModels.UltimateCombinationNumber)!;
            combination.Terms.Add(new LoadCombinationTerm(1, 0.3));

            Assert.Equal(1.5, model.GetTotalFactor(SampleModels.UltimateCombinationNumber, 1), 12);
        }

        [Fact]
        public void GetTotalFactor_IsZeroForAnAbsentCase()
        {
            var model = SampleModels.Build();

            // Case 2 is in the first combination but not in the second.
            Assert.Equal(1.0, model.GetTotalFactor(SampleModels.UltimateCombinationNumber, 2));
            Assert.Equal(0.0, model.GetTotalFactor(SampleModels.EnvelopeCombinationNumber, 2));

            // An unknown combination answers the same way, which is the cost of
            // taking a number rather than the entity.
            Assert.Equal(0.0, model.GetTotalFactor(999, 1));
        }

        [Fact]
        public void FindLoadCombination_ReturnsNullForAnUnknownNumber()
        {
            var model = SampleModels.Build();

            Assert.NotNull(model.FindLoadCombination(SampleModels.UltimateCombinationNumber));
            Assert.Null(model.FindLoadCombination(999));
        }

        [Fact]
        public void NextLoadCombinationNumber_IsOnePastTheHighest()
        {
            var model = SampleModels.Build();

            Assert.Equal(SampleModels.ExcludedCombinationNumber + 1, model.NextLoadCombinationNumber());
        }

        [Fact]
        public void NextLoadCombinationNumber_IsOneForAnEmptyModel()
        {
            Assert.Equal(1, new FemexModel().NextLoadCombinationNumber());
        }
    }
}
