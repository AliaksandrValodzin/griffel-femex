using griffel_femex.Interop;
using griffel_femex.Interop.Conformance;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The Tier-1 harness of <c>FEMEX_Adapters.md</c> §7.3, run against the lossy
    /// reference adapter of §7.5 — and, deliberately, against broken copies of it.
    ///
    /// <b>The second half is the point.</b> A conformance base class with no
    /// implementation to run against is a suite that has never been shown to fail,
    /// and a suite that has never failed is a decoration. So each rule below is
    /// asserted twice: the compliant adapter passes, and an adapter broken in exactly
    /// the way the rule forbids is caught. Before any real plugin depends on the
    /// distinction, this is what shows the harness can tell one from the other.
    /// </summary>
    public class ConformanceTests
    {
        private static FemexModel Golden() => FemexModel.Load(Path.Combine("Examples", "Conformance1.femex"));

        /// <summary>The harness, wired to the reference adapter and an in-memory round trip.</summary>
        private sealed class ReferenceHarness : ConformanceHarness
        {
            private readonly Func<IFemexAdapter> _adapter;

            internal ReferenceHarness(Func<IFemexAdapter>? adapter = null)
            {
                _adapter = adapter ?? (() => new ReferenceAdapter());
            }

            protected override IFemexAdapter CreateAdapter() => _adapter();

            protected override ConformanceTransport CreateTransport() => new ReferenceTransport();

            protected override FemexModel CreateGoldenModel() => Golden();
        }

        private static ConformanceCheck Run(string name, Func<IFemexAdapter>? adapter = null)
        {
            foreach (ConformanceCheck check in new ReferenceHarness(adapter).RunTier1())
            {
                if (check.Name == name)
                    return check;
            }

            throw new InvalidOperationException($"No check named \"{name}\".");
        }

        private static void AssertPasses(ConformanceCheck check)
        {
            Assert.True(check.Passed, check.ToString());
        }

        // ----- The compliant adapter -----

        [Fact]
        public void TheReferenceAdapter_PassesEveryTier1Rule()
        {
            IReadOnlyList<ConformanceCheck> checks = new ReferenceHarness().RunTier1();

            Assert.Equal(7, checks.Count);
            foreach (ConformanceCheck check in checks)
                Assert.True(check.Passed, check.ToString());
        }

        [Fact]
        public void TheReferenceAdapter_ExercisesAllFiveLossCategories()
        {
            // §7.5: written deliberately lossy, so that each category in §4 is
            // exercised by something. A harness whose only subject loses nothing
            // proves nothing about a harness for adapters that do.
            var transport = new ReferenceTransport();
            var adapter = new ReferenceAdapter();

            TransferResult<ExportReceipt> exported =
                adapter.Export(Golden(), transport.BeginExport(), null, CancellationToken.None);
            TransferResult<FemexModel> imported =
                adapter.Import(transport.BeginImport(), null, CancellationToken.None);

            var categories = new HashSet<LossCategory>();
            foreach (TransferMessage message in exported.Messages.Concat(imported.Messages))
            {
                if (message.Category is LossCategory category)
                    categories.Add(category);
            }

            Assert.Contains(LossCategory.Dropped, categories);
            Assert.Contains(LossCategory.Approximated, categories);
            Assert.Contains(LossCategory.Invented, categories);
        }

        [Fact]
        public void ANativeConceptFemexHasNoNounFor_IsUnmapped_AndReportedOnce()
        {
            // Unmapped is the one category a round trip of our own output cannot
            // produce, because nothing FEMEX wrote carries a concept FEMEX does not
            // have. It needs a document somebody else could have written.
            var document = new ReferenceDocument { UnitSystem = ReferenceAdapter.AssumedUnitSystem };
            for (int i = 0; i < 3; i++)
            {
                document.Nodes.Add(new ReferenceNode { Uid = Guid.NewGuid(), X = i * 3.0 });
                if (i > 0)
                {
                    document.Members.Add(new ReferenceMember
                    {
                        Uid = Guid.NewGuid(),
                        StartNode = document.Nodes[i - 1].Uid,
                        EndNode = document.Nodes[i].Uid,
                        StiffnessModifier = 0.35,
                    });
                }
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(document));
            TransferResult<FemexModel> result = new ReferenceAdapter().Import(
                new StreamImportRequest(new MemoryStream(bytes)), null, CancellationToken.None);

            // §4.4: per concept, not per object. Two modified members, one message.
            TransferMessage unmapped = Assert.Single(result.Messages,
                                                     m => m.Category == LossCategory.Unmapped);
            Assert.Equal(FemexEntity.Bar, unmapped.Subject!.Value.Entity);
            Assert.Null(unmapped.Subject!.Value.Id);
            Assert.Contains("2 member(s)", unmapped.Text);
        }

        [Fact]
        public void AStaleSchema_IsReported_RatherThanProceededThrough()
        {
            // The one category no per-program mapping document would ever catch,
            // which is a good argument for it being in the shared contract.
            var adapter = new ReferenceAdapter(schemaVersion: "1.6");
            TransferResult<ExportReceipt> result =
                adapter.Export(Golden(), new ReferenceTransport().BeginExport(), null, CancellationToken.None);

            TransferMessage stale = Assert.Single(result.Messages,
                                                  m => m.Category == LossCategory.Stale);
            Assert.Contains("1.8", stale.Text);
            Assert.Contains("1.6", stale.Text);
        }

        [Fact]
        public void EveryLoss_IsAWarning_AndEveryFailure_AnError()
        {
            // The invariant the whole taxonomy rests on: a loss never blocks, because
            // losing something is what adapters are for; a failure blocks, because
            // there is no model.
            var transport = new ReferenceTransport();
            var adapter = new ReferenceAdapter();
            adapter.Export(Golden(), transport.BeginExport(), null, CancellationToken.None);

            TransferResult<FemexModel> imported =
                adapter.Import(transport.BeginImport(), null, CancellationToken.None);

            foreach (TransferMessage message in imported.Messages)
            {
                Assert.Equal(message.Category.HasValue ? ValidationSeverity.Warning : ValidationSeverity.Error,
                             message.Severity);
            }

            Assert.True(imported.Succeeded);
        }

        [Fact]
        public void ACorruptSource_Returns_ItDoesNotThrow()
        {
            // §3.6. A plugin that throws gives the host no uniform behaviour to build
            // on: every host then wraps every call in catch (Exception) and loses the
            // distinction between "the file is corrupt" and "this adapter has a bug".
            var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{ not json"));
            TransferResult<FemexModel> result = new ReferenceAdapter().Import(
                new StreamImportRequest(source) { SourceName = "broken.json" }, null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Null(result.Value);
            TransferMessage failure = Assert.Single(result.Messages);
            Assert.Equal(ValidationSeverity.Error, failure.Severity);
            Assert.Null(failure.Category);
            Assert.Contains("broken.json", failure.Text);
        }

        [Fact]
        public void TheReceipt_CarriesTheUidToHandleMapping_AsData()
        {
            // §5.3, and the reason Export does not return void. A batch run over
            // forty models has nowhere sensible to scatter forty sidecars.
            TransferResult<ExportReceipt> result = new ReferenceAdapter().Export(
                Golden(), new ReferenceTransport().BeginExport(), null, CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.NotEmpty(result.Value!.NativeHandles);
            Assert.Contains(result.Value.NativeHandles.Values, h => h.StartsWith("N"));
            Assert.Contains(result.Value.NativeHandles.Values, h => h.StartsWith("M"));
        }

        // ----- The broken adapters, one per rule -----

        [Fact]
        public void CapabilityHonesty_CatchesADeclarationThatDoesNotMatch()
        {
            ConformanceCheck check = Run("Capability honesty", () => new DishonestAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.Contains("Grid"));
        }

        [Fact]
        public void NullTolerance_CatchesAnAdapterThatAssumesCompleteness()
        {
            ConformanceCheck check = Run("Null tolerance", () => new BrittleAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.Contains("NullReferenceException"));
        }

        [Fact]
        public void NoSecondGate_CatchesAnAdapterThatInventsItsOwnNotionOfReady()
        {
            ConformanceCheck check = Run("No second gate", () => new FussyAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.Contains("half-drawn"));
        }

        [Fact]
        public void MessageAnchoring_CatchesAMessageAboutSomethingThatIsNotThere()
        {
            ConformanceCheck check = Run("Message anchoring", () => new MisanchoringAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.Contains("Bar 999999"));
        }

        [Fact]
        public void LossCoverage_CatchesALossThatWasNotDeclared()
        {
            ConformanceCheck check = Run("Loss coverage", () => new SilentAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.StartsWith("Undeclared:"));
        }

        [Fact]
        public void TwoPhaseSynthesis_CatchesAnImportThatDependsOnOrder()
        {
            ConformanceCheck check = Run("Two-phase synthesis", () => new StreamingAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.Contains("table"));
        }

        [Fact]
        public void NameStability_CatchesACounter()
        {
            ConformanceCheck check = Run("Name stability", () => new CountingAdapter());

            Assert.False(check.Passed);
            Assert.Contains(check.Findings, f => f.Contains("§5.4"));
        }

        [Fact]
        public void ASkippedRule_IsNotAPass()
        {
            // A suite that quietly reports green for what it never ran is the failure
            // the two tiers exist to prevent.
            ConformanceCheck check = ConformanceCheck.Skip("x", "y", "no transport");

            Assert.False(check.Passed);
            Assert.True(check.Skipped);
        }
    }
}
