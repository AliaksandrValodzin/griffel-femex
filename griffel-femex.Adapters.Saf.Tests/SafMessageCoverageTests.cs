using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using griffel_femex.Interop;
using Xunit;

namespace griffel_femex.Adapters.Saf.Tests
{
    /// <summary>
    /// <c>FEMEX_SAF_Fit.md</c> §8.2 is the mapping's acceptance criterion, so it is
    /// also its checklist. These are the tests that make the catalogue an assertion
    /// rather than a document.
    /// </summary>
    public class SafMessageCoverageTests
    {
        /// <summary>
        /// The reference workbook is SCIA's "model containing all supported objects",
        /// deliberately exhaustive. Every loss the import leg can declare against a
        /// file that exercises every sheet should be declared against this one.
        /// </summary>
        /// <remarks>
        /// The exceptions are named individually rather than allowed by a wildcard,
        /// because a shrinking list of exceptions is progress and a wildcard is a
        /// test that stops noticing.
        /// </remarks>
        [Fact]
        public void TheReferenceWorkbook_EmitsEveryImportLossItExercises()
        {
            var expected = new[]
            {
                SafLoss.StampedUnitSystem,
                SafLoss.MintedUids,
                SafLoss.InventedGravity,
                SafLoss.DroppedMemberType,
                SafLoss.DroppedLayerAndColour,
                SafLoss.DroppedLoadCaseDuration,
                SafLoss.DroppedLoadGroupCategory,
                SafLoss.DroppedPointLoadFrame,
                SafLoss.DroppedNonLinearCombination,
                SafLoss.DroppedProjectColumns,
                SafLoss.DroppedCompositeShape,
                SafLoss.DroppedSectionDescription,
                SafLoss.DroppedPasternakSubsoil,
                SafLoss.DroppedObjectNames,
                SafLoss.ChordedCurve,
                SafLoss.FlattenedVaryingMember,
                SafLoss.GenericSection,
                SafLoss.SimplifiedSectionShape,
                SafLoss.InferredShapeParameters,
                SafLoss.RibAsBar,
                SafLoss.NominalThickness,
                SafLoss.CollapsedCombinationFactor,
                SafLoss.ExpandedRepeatSeries,
                SafLoss.MergedForceAndMoment,
                SafLoss.CollapsedThermalVariation,
                SafLoss.ChordedPosition,
                SafLoss.ChordedSurfaceEdge,
                SafLoss.UnmappedProxyElement,
                SafLoss.UnmappedRigidLink,
                SafLoss.UnmappedRigidCross,
                SafLoss.UnmappedRigidMember,
                SafLoss.UnmappedInternalEdge,
                SafLoss.UnmappedFreePointAction,
                SafLoss.UnmappedFreeCurveAction,
                SafLoss.UnmappedFreeSurfaceAction,
                SafLoss.UnmappedSupportDeformation,
                SafLoss.UnmappedResults,
                SafLoss.UnmappedCompositeAction,
            };

            IReadOnlyList<TransferMessage> messages = ImportReference();
            var missing = expected
                .Where(loss => !messages.Any(message =>
                    message.Text.StartsWith(Head(SafMessages.For(loss).Text), StringComparison.Ordinal)))
                .ToList();

            Assert.True(missing.Count == 0,
                        "The reference workbook exercises these and the importer said nothing: " +
                        string.Join(", ", missing));
        }

        /// <summary>
        /// Levels are synthesised on every SAF file — nothing in the format
        /// references a storey and FEMEX makes the reference an error — and this is
        /// the highest-traffic invention the adapter will ever produce. The message
        /// comes from the core synthesis helper rather than from this catalogue,
        /// which is why it is asserted by shape rather than by wording.
        /// </summary>
        [Fact]
        public void EverySynthesisedLevel_IsReportedAsAnInvention()
        {
            IReadOnlyList<TransferMessage> messages = ImportReference();

            var invented = messages
                .Where(message => message.Category == LossCategory.Invented &&
                                  message.Subject?.Entity == FemexEntity.Level)
                .ToList();

            // The reference workbook declares two storeys and uses nine distinct
            // elevations, so seven levels are the adapter's and are said to be.
            Assert.Equal(7, invented.Count);
        }

        /// <summary>
        /// Every category §4 defines appears in a real import, which is the crude
        /// check that the adapter is not reporting one kind of loss and staying quiet
        /// about the rest.
        /// </summary>
        [Fact]
        public void TheReferenceWorkbook_ExercisesFourOfTheFiveLossCategories()
        {
            IReadOnlyList<TransferMessage> messages = ImportReference();
            var categories = messages
                .Where(message => message.Category.HasValue)
                .Select(message => message.Category!.Value)
                .Distinct()
                .ToList();

            Assert.Contains(LossCategory.Invented, categories);
            Assert.Contains(LossCategory.Dropped, categories);
            Assert.Contains(LossCategory.Approximated, categories);
            Assert.Contains(LossCategory.Unmapped, categories);

            // Stale is the fifth, and it is about a FEMEX schema version rather than
            // about a workbook, so no import of any SAF file can produce it.
            Assert.DoesNotContain(LossCategory.Stale, categories);
        }

        [Fact]
        public void TheExportLeg_DeclaresEveryMandatoryColumnItInvents()
        {
            var expected = new[]
            {
                SafLoss.InventedSystemOfUnits,
                SafLoss.InventedNationalCode,
                SafLoss.InventedCrossSectionLcs,
                SafLoss.InventedSafVersion,
                SafLoss.InventedLocalFrame,
                SafLoss.InventedMemberEccentricity,
                SafLoss.SynthesisedNames,
            };

            FemexModel model = new SafImporter()
                .Import(new StreamImportRequest(SafCorpus.Open(SafCorpus.Reference)), null,
                        CancellationToken.None).Value!;

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> exported = new SafExporter().Export(
                model, new StreamExportRequest(written), null, CancellationToken.None);

            var missing = expected
                .Where(loss => !exported.Messages.Any(message =>
                    message.Text.StartsWith(Head(SafMessages.For(loss).Text), StringComparison.Ordinal)))
                .ToList();

            Assert.True(missing.Count == 0,
                        "Decision 12 says every invented mandatory column is an Invented message, " +
                        "never a silent default. These were silent: " + string.Join(", ", missing));
        }

        [Fact]
        public void EveryCataloguedLoss_HasTextAndAnAnchoringRule()
        {
            foreach (SafLoss loss in Enum.GetValues(typeof(SafLoss)).Cast<SafLoss>())
            {
                SafMessages.Entry entry = SafMessages.For(loss);

                Assert.False(string.IsNullOrWhiteSpace(entry.Text), loss.ToString());
                Assert.NotEqual(TransferDirection.None, entry.Direction);

                // A per-object message must have something to anchor to; a model-level
                // one must not claim to be about an object kind it cannot name.
                if (entry.PerObject)
                    Assert.True(entry.Entity.HasValue, loss + " is per object and anchors to nothing.");
            }
        }

        [Fact]
        public void APerConceptLoss_CannotBeReportedPerObject_AndTheReverse()
        {
            var log = new SafMessageLog();

            Assert.Throws<InvalidOperationException>(
                () => log.Object(SafLoss.DroppedMemberType, new ObjectRef(FemexEntity.Bar, 1)));

            Assert.Throws<InvalidOperationException>(() => log.Concept(SafLoss.ChordedCurve));
        }

        [Fact]
        public void APerConceptLoss_IsReportedOnce_HoweverManyObjectsItTouched()
        {
            var log = new SafMessageLog();

            log.Concept(SafLoss.DroppedMemberType);
            log.Concept(SafLoss.DroppedMemberType);
            log.Concept(SafLoss.DroppedMemberType);

            Assert.Single(log.Messages);
        }

        private static IReadOnlyList<TransferMessage> ImportReference()
        {
            using Stream source = SafCorpus.Open(SafCorpus.Reference);
            return new SafImporter().Import(
                new StreamImportRequest(source) { SourceName = SafCorpus.Reference },
                null, CancellationToken.None).Messages;
        }

        /// <summary>
        /// The first clause of a catalogue entry, which is stable while the rest of
        /// the sentence is edited. Matching on the whole text would make every
        /// wording improvement a test failure.
        /// </summary>
        private static string Head(string text)
        {
            int stop = text.IndexOf('.');
            return stop > 0 ? text.Substring(0, stop) : text;
        }
    }
}
