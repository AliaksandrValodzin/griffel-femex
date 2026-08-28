using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using griffel_femex.Comparison;
using griffel_femex.Interop;
using Xunit;

namespace griffel_femex.Adapters.Saf.Tests
{
    /// <summary>
    /// Phase B's stated proof: import every workbook in the published corpus, round
    /// trip SAF → FEMEX → SAF → FEMEX, and assert under §7.2 equivalence
    /// <b>modulo declared losses</b> that every difference is named by a
    /// <see cref="TransferMessage"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The comparison is on object models, with the SDK on both sides.</b> Not on
    /// workbooks, and this is not a convenience. <c>FEMEX_SAF_Corpus_Notes.md</c> §5
    /// measured the SAF SDK round-tripping the reference file <i>to itself</i> — no
    /// FEMEX anywhere — and changing 75 cells: 42 invented GUIDs where the Id column
    /// was blank, nineteen renamed or added columns, twelve normalised values, and
    /// the specification version. FEMEX cannot name what it did not do, so a
    /// cell-by-cell assertion against the input workbook would fail on the layer
    /// beneath it. Comparing the FEMEX model in with the FEMEX model out puts the
    /// SDK on both sides of the comparison, where its own normalisation cancels.
    /// </para>
    /// <para>
    /// <b>What counts as covered</b> is the conformance harness's rule, restated
    /// here because the harness keeps it private: a message whose subject is exactly
    /// the difference's, or one anchored to the difference's entity kind with no id —
    /// §4.4's per-concept report — or, for a difference about the model itself, any
    /// message about the model itself.
    /// </para>
    /// </remarks>
    public class SafRoundTripTests
    {
        [Theory]
        [MemberData(nameof(SafCorpus.All), MemberType = typeof(SafCorpus))]
        public void EveryPublishedWorkbook_Imports(string file)
        {
            TransferResult<FemexModel> imported = Import(file);

            Assert.True(imported.Succeeded, Explain(file, imported.Messages));
            Assert.NotEmpty(imported.Value!.Nodes);
            Assert.Equal(FemexModel.CurrentSchemaVersion, imported.Value!.SchemaVersion);
        }

        [Theory]
        [MemberData(nameof(SafCorpus.All), MemberType = typeof(SafCorpus))]
        public void EveryPublishedWorkbook_Exports_AndTheSdkAcceptsWhatIsWritten(string file)
        {
            FemexModel model = Import(file).Value!;

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> exported = new SafExporter().Export(
                model, new StreamExportRequest(written) { DestinationName = file },
                null, CancellationToken.None);

            // The SDK validates on write and refuses a workbook missing a mandatory
            // column, which makes it the nearest thing to an independent oracle that
            // runs without a browser. A failure here is the invention policy of P5
            // being incomplete, not a test being fussy.
            Assert.True(exported.Succeeded, Explain(file, exported.Messages));
            Assert.True(written.Length > 0);
        }

        [Theory]
        [MemberData(nameof(SafCorpus.All), MemberType = typeof(SafCorpus))]
        public void EveryDifferenceAcrossTheRoundTrip_IsNamedByAMessage(string file)
        {
            TransferResult<FemexModel> first = Import(file);
            FemexModel source = first.Value!;

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> exported = new SafExporter().Export(
                source, new StreamExportRequest(written) { DestinationName = file },
                null, CancellationToken.None);

            Assert.True(exported.Succeeded, Explain(file, exported.Messages));

            written.Position = 0;
            TransferResult<FemexModel> second = new SafImporter().Import(
                new StreamImportRequest(written) { SourceName = file }, null, CancellationToken.None);

            Assert.True(second.Succeeded, Explain(file, second.Messages));

            var declared = new List<TransferMessage>(first.Messages);
            declared.AddRange(exported.Messages);
            declared.AddRange(second.Messages);

            var undeclared = ModelDiff.Compare(source, second.Value!)
                .Where(difference => !Covered(declared, difference))
                .Select(difference => difference.Text)
                .ToList();

            Assert.True(undeclared.Count == 0,
                        $"{file}: {undeclared.Count} undeclared differences." +
                        Environment.NewLine + string.Join(Environment.NewLine, undeclared.Take(20)));
        }

        /// <summary>
        /// The same workbook imported twice is the same model twice — object for
        /// object, uid for uid.
        /// </summary>
        /// <remarks>
        /// §6.2's rule made executable on the real adapter rather than on a fixture.
        /// The pieces a SAF row splits into — a chorded chain of bars, the two ends
        /// of a <c>Position = Both</c> hinge, an expanded repeat series — carry
        /// derived uids rather than minted ones for exactly this reason: a read that
        /// mints is a read whose answer changes every time it runs, and two such
        /// models cannot be diffed at all.
        /// </remarks>
        [Theory]
        [MemberData(nameof(SafCorpus.All), MemberType = typeof(SafCorpus))]
        public void TheSameWorkbookReadTwice_GivesTheSameModel(string file)
        {
            FemexModel first = Import(file).Value!;
            FemexModel second = Import(file).Value!;

            IReadOnlyList<ModelDifference> differences = ModelDiff.Compare(first, second);

            Assert.True(differences.Count == 0,
                        $"{file}: reading the same workbook twice gave {differences.Count} differences." +
                        Environment.NewLine +
                        string.Join(Environment.NewLine, differences.Take(10).Select(d => d.Text)));
        }

        [Fact]
        public void TheSteelHallFrame_RoundTripsWithNoDifferenceAtAll()
        {
            // The corpus's simpler model — 47 members, 45 nodes, ten supports,
            // sixteen hinges and no surfaces — crosses both legs intact. It is the
            // case that shows the declared losses on the house are the house's, not
            // the adapter's baseline noise.
            const string file = "SAF_example_STEEL_HALL_metrix_ZYX_210.xlsx";

            FemexModel source = Import(file).Value!;

            using var written = new MemoryStream();
            new SafExporter().Export(source, new StreamExportRequest(written), null, CancellationToken.None);

            written.Position = 0;
            FemexModel returned = new SafImporter()
                .Import(new StreamImportRequest(written), null, CancellationToken.None).Value!;

            IReadOnlyList<ModelDifference> differences = ModelDiff.Compare(source, returned);

            Assert.True(differences.Count == 0,
                        string.Join(Environment.NewLine, differences.Take(10).Select(d => d.Text)));
        }

        private static TransferResult<FemexModel> Import(string file)
        {
            using Stream source = SafCorpus.Open(file);
            return new SafImporter().Import(
                new StreamImportRequest(source) { SourceName = file }, null, CancellationToken.None);
        }

        private static bool Covered(List<TransferMessage> messages, ModelDifference difference)
        {
            foreach (TransferMessage message in messages)
            {
                if (difference.Subject is not ObjectRef subject)
                {
                    if (message.Subject is null)
                        return true;

                    continue;
                }

                if (message.Subject is ObjectRef anchor && Matches(subject, anchor))
                    return true;
            }

            return false;
        }

        private static bool Matches(ObjectRef subject, ObjectRef anchor)
        {
            if (anchor.Entity != subject.Entity)
                return false;

            // §4.4's per-concept report: an anchor with no id is about the kind.
            if (anchor.Id is null && anchor.Uid is null)
                return true;

            if (anchor.Uid.HasValue && subject.Uid.HasValue)
                return anchor.Uid.Value == subject.Uid.Value;

            return anchor.Id.HasValue && subject.Id.HasValue && anchor.Id.Value == subject.Id.Value;
        }

        private static string Explain(string file, IReadOnlyList<TransferMessage> messages)
        {
            IEnumerable<string> errors = messages
                .Where(message => message.Severity == ValidationSeverity.Error)
                .Select(message => message.Text);

            return file + Environment.NewLine + string.Join(Environment.NewLine, errors.Take(10));
        }
    }
}
