using griffel_femex.Geometry;
using griffel_femex.Geometry.Sections;
using griffel_femex.Interop;
using griffel_femex.Loads;
using griffel_femex.Materials;
using Xunit;

namespace griffel_femex.Tests
{
    /// <summary>
    /// The contract types of <c>FEMEX_Adapters.md</c> §3, and the invariants they
    /// exist to make unbreakable rather than merely stated.
    ///
    /// Each test below removes one failure that a contract expressed as prose cannot:
    /// a caller reading a model without its loss report, an adapter choosing the
    /// severity of its own losses, a capability declaration in free text that no test
    /// could check, and a synthesised name that changes between exports.
    /// </summary>
    public class InteropContractTests
    {
        private static FemexModel Golden() => FemexModel.Load(Path.Combine("Examples", "Conformance1.femex"));

        // ----- §3.2: the messages are not on the side -----

        [Fact]
        public void Succeeded_IsDefinedByTheValue_NotBySeverity()
        {
            // The invariant the whole taxonomy rests on: a transfer that reports
            // fifty losses and produces a model succeeded, and one that reports none
            // and produces nothing did not.
            var lossy = TransferResult<FemexModel>.Ok(new FemexModel(), new[]
            {
                TransferMessage.Loss(LossCategory.Dropped, new ObjectRef(FemexEntity.Bar, 1), "gone"),
                TransferMessage.Loss(LossCategory.Approximated, new ObjectRef(FemexEntity.Bar, 2), "near"),
            });

            Assert.True(lossy.Succeeded);
            Assert.Equal(2, lossy.Messages.Count);

            TransferResult<FemexModel> failed = TransferResult<FemexModel>.Failed("the program is not running");
            Assert.False(failed.Succeeded);
            Assert.Null(failed.Value);
        }

        [Fact]
        public void AFailureMustSayWhy()
        {
            Assert.Throws<ArgumentException>(
                () => TransferResult<FemexModel>.Failed(new TransferMessage[0]));
        }

        // ----- §3.4: the invariant that makes §2.1 machine-checkable -----

        [Fact]
        public void EveryLossIsAWarning_AndOnlyAFailureIsAnError()
        {
            foreach (LossCategory category in Enum.GetValues(typeof(LossCategory)).Cast<LossCategory>())
            {
                TransferMessage loss = TransferMessage.Loss(category, new ObjectRef(FemexEntity.Node, 1), "x");
                Assert.Equal(ValidationSeverity.Warning, loss.Severity);
                Assert.Equal(category, loss.Category);
            }

            TransferMessage failure = TransferMessage.Failure("ETABS is not installed");
            Assert.Equal(ValidationSeverity.Error, failure.Severity);
            Assert.Null(failure.Category);
            Assert.Null(failure.Subject);
        }

        [Fact]
        public void AModelLevelLoss_IsTheOnlyDoorToAnUnanchoredLoss()
        {
            // Units and gravity are deliberately not entities (§3.3), and §6.5 and
            // §6.6 then require an adapter to report exactly those. Loss cannot
            // express it; ModelLoss is named for what it is.
            TransferMessage assumed = TransferMessage.ModelLoss(
                LossCategory.Invented, "metres and kilonewtons were assumed");

            Assert.Null(assumed.Subject);
            Assert.Equal(ValidationSeverity.Warning, assumed.Severity);
            Assert.Contains("model", assumed.ToString());
        }

        // ----- §3.5: a message names its object, on both keys -----

        [Fact]
        public void AnObjectRef_CarriesBothKeys_BecauseEachHasOneConsumer()
        {
            Guid uid = Guid.NewGuid();
            var reference = new ObjectRef(FemexEntity.Bar, 41, uid);

            Assert.Equal(41, reference.Id);
            Assert.Equal(uid, reference.Uid);
            Assert.Equal("Bar 41", reference.ToString());
            Assert.Equal(reference, new ObjectRef(FemexEntity.Bar, 41, uid));
            Assert.NotEqual(reference, new ObjectRef(FemexEntity.Bar, 42, uid));
        }

        [Fact]
        public void EnumerateIdentified_YieldsTheAnchorForEveryUidCarryingObject()
        {
            // §5.2 called this the one walk both name synthesis and uid-keyed
            // equivalence need, and noted that a private one means thirteen
            // declaration sites hand-listed elsewhere, going stale independently.
            FemexModel model = Golden();
            var kinds = new HashSet<FemexEntity>();

            foreach (var (entity, reference, owner) in model.EnumerateIdentified())
            {
                Assert.Equal(entity.Uid, reference.Uid);
                Assert.NotNull(owner);
                kinds.Add(reference.Entity);
            }

            // The mesh is absent by design — it is regenerated wholesale, so a stable
            // identity for a mesh face means nothing.
            Assert.DoesNotContain(FemexEntity.Mesh, kinds);
            Assert.Contains(FemexEntity.Bar, kinds);
            Assert.Contains(FemexEntity.LoadCombination, kinds);
        }

        // ----- §3.3: a vocabulary a test can check -----

        [Fact]
        public void AnUndeclaredEntity_IsNone_NotUnknown()
        {
            AdapterCapabilities capabilities = AdapterCapabilities.Both(FemexEntity.Node, FemexEntity.Bar);

            Assert.True(capabilities.Supports(FemexEntity.Bar, TransferDirection.Import));
            Assert.True(capabilities.Supports(FemexEntity.Bar, TransferDirection.Export));
            Assert.False(capabilities.Supports(FemexEntity.Plate, TransferDirection.Import));
            Assert.Equal(TransferDirection.None, capabilities.For(FemexEntity.Plate));
        }

        [Fact]
        public void AsymmetryIsOrdinary_AndDeclarable()
        {
            // A program you can read plates out of but not write plates into.
            var capabilities = new AdapterCapabilities(new[]
            {
                new KeyValuePair<FemexEntity, TransferDirection>(FemexEntity.Plate, TransferDirection.Import),
            });

            Assert.True(capabilities.Supports(FemexEntity.Plate, TransferDirection.Import));
            Assert.False(capabilities.Supports(FemexEntity.Plate, TransferDirection.Export));
        }

        [Fact]
        public void UnitsAndGravity_AreNotEntities_SoNoAdapterCanDeclareItsWayOutOfThem()
        {
            string[] names = Enum.GetNames(typeof(FemexEntity));

            Assert.DoesNotContain("Units", names);
            Assert.DoesNotContain("Gravity", names);
        }

        // ----- §4.5: the loss no per-program document would catch -----

        [Fact]
        public void AnAdapterBuiltAgainstAnOlderSchema_ReportsStale()
        {
            var info = new AdapterInfo("Test", "Nothing", null, "1.6");
            TransferMessage? message = info.CompareSchema(Golden());

            Assert.NotNull(message);
            Assert.Equal(LossCategory.Stale, message!.Category);
        }

        [Fact]
        public void AMatchingSchema_ReportsNothing_AndSoDoesAnUnversionedFile()
        {
            var info = new AdapterInfo("Test", "Nothing", null, FemexModel.CurrentSchemaVersion);

            Assert.Null(info.CompareSchema(Golden()));
            Assert.Null(info.CompareSchema(new FemexModel()));
        }

        [Fact]
        public void AnAdapterMustStateWhatItWasBuiltAgainst()
        {
            Assert.Throws<ArgumentException>(() => new AdapterInfo("Test", "Nothing", null, "  "));
            Assert.Throws<ArgumentException>(() => new AdapterInfo("  ", "Nothing", null, "1.8"));
        }

        // ----- §5.4 and §5.5: name synthesis -----

        [Fact]
        public void ASynthesisedName_IsDerivedFromTheUid_AndIsObviouslySynthetic()
        {
            var uid = new Guid("3f9a2c14-0000-0000-0000-000000000000");

            Assert.Equal("Section-3f9a2c14", NameSynthesis.For(FemexEntity.Section, uid));
        }

        [Fact]
        public void NameSynthesis_CoversSixFamilies_NotTheValidatorsFour()
        {
            // §5.5: a storey is name-keyed in ETABS and Robot every bit as much as a
            // section is, so Level and Plate are here even though ValidateNameKeys
            // has not caught up.
            FemexModel model = Golden();
            foreach (Section section in model.Sections)
                section.Name = null;
            foreach (Material material in model.Materials)
                material.Name = null;
            foreach (LoadCase loadCase in model.LoadCases)
                loadCase.Label = null;
            foreach (Level level in model.Levels)
                level.Name = null;
            foreach (Plate plate in model.Plates)
                plate.Name = null;

            IReadOnlyList<TransferMessage> messages = NameSynthesis.Apply(model);

            Assert.All(messages, m => Assert.Equal(LossCategory.Invented, m.Category));
            Assert.StartsWith("Level-", model.Levels[0].Name);
            Assert.StartsWith("Plate-", model.Plates[0].Name);
            Assert.StartsWith("Section-", model.Sections[0].Name);
            Assert.StartsWith("Material-", model.Materials[0].Name);
            Assert.StartsWith("LoadCase-", model.LoadCases[0].Label);
        }

        [Fact]
        public void AnAuthoredName_IsNeverOverwritten()
        {
            FemexModel model = Golden();
            model.Sections[0].Name = "610 UB 125";

            NameSynthesis.Apply(model);

            Assert.Equal("610 UB 125", model.Sections[0].Name);
        }

        [Fact]
        public void NameSynthesis_StampsUidsFirst_BecauseTheNameIsDerivedFromOne()
        {
            var model = new FemexModel();
            model.Sections.Add(new Rectangle { Id = 1, Width = 0.3, Depth = 0.6 });

            NameSynthesis.Apply(model);

            Assert.NotNull(model.Sections[0].Uid);
            Assert.Equal(NameSynthesis.For(FemexEntity.Section, model.Sections[0].Uid!.Value),
                         model.Sections[0].Name);
        }

        // ----- §3.1 and A4: the request seam -----

        [Fact]
        public void ARequest_CarriesNoVendorType_AndTheStreamIsTheFileCase()
        {
            using var buffer = new MemoryStream();
            var request = new StreamExportRequest(buffer)
            {
                DestinationName = "model.saf",
                Options = new Dictionary<string, string> { ["version"] = "2.3.0" },
            };

            Assert.Same(buffer, request.Destination);
            Assert.Equal("model.saf", request.DestinationName);
            Assert.Equal("2.3.0", request.Options!["version"]);
            Assert.IsAssignableFrom<ExportRequest>(request);
        }

        [Fact]
        public void AReceipt_HoldsTheMappingAsData()
        {
            Guid uid = Guid.NewGuid();
            var receipt = new ExportReceipt("model.saf", new[]
            {
                new KeyValuePair<Guid, string>(uid, "B41"),
            });

            Assert.Equal("B41", receipt.NativeHandles[uid]);
            Assert.Equal("model.saf", receipt.DestinationName);
            Assert.Empty(new ExportReceipt().NativeHandles);
        }

        [Fact]
        public void Progress_IsAValueThatCostsNothingToIgnore()
        {
            var progress = new TransferProgress(FemexEntity.Bar, 12, 40, "members");

            Assert.Equal(FemexEntity.Bar, progress.Entity);
            Assert.Contains("12/40", progress.ToString());
            Assert.Contains("model", new TransferProgress(null, 1, 0).ToString());
        }
    }
}
