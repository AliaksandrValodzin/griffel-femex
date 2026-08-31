using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using griffel_femex.Geometry;
using griffel_femex.Interop;
using Xunit;

namespace griffel_femex.Adapters.Saf.Tests
{
    /// <summary>
    /// The rules of <c>FEMEX_Adapters.md</c> §3.6 and §5, checked against the SAF
    /// adapter rather than against a fixture.
    /// </summary>
    public class SafAdapterTests
    {
        [Fact]
        public void AWorkbookThatIsNotAWorkbook_Returns_ItDoesNotThrow()
        {
            using var rubbish = new MemoryStream(Encoding.UTF8.GetBytes("this is not a spreadsheet"));

            TransferResult<FemexModel> result = new SafImporter().Import(
                new StreamImportRequest(rubbish) { SourceName = "rubbish.xlsx" },
                null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Messages, message => message.Severity == ValidationSeverity.Error);
        }

        [Fact]
        public void AnEmptyStream_Returns_ItDoesNotThrow()
        {
            using var empty = new MemoryStream();

            TransferResult<FemexModel> result = new SafImporter().Import(
                new StreamImportRequest(empty), null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.NotEmpty(result.Messages);
        }

        [Fact]
        public void ARequestTheAdapterCannotServe_Returns_ItDoesNotThrow()
        {
            TransferResult<FemexModel> result = new SafImporter().Import(
                new LiveSessionRequest(), null, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains("LiveSessionRequest", result.Messages[0].Text);
        }

        /// <summary>
        /// §2.1's motivating case: three levels, nothing else. An adapter that
        /// dereferences its way through a model is one that fails on the first
        /// half-built one a user hands it, which is most of them.
        /// </summary>
        [Fact]
        public void AModelWithAlmostNothingInIt_Exports_ItDoesNotThrow()
        {
            var model = new FemexModel();
            model.Levels.Add(new Level { LevelNumber = 1, AbsoluteElevation = 0.0 });
            model.Levels.Add(new Level { LevelNumber = 2, AbsoluteElevation = 3.0 });
            model.Nodes.Add(new Node { NodeNumber = 1, X = 0.0, Y = 0.0, LevelNumber = 1 });

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> result = new SafExporter().Export(
                model, new StreamExportRequest(written), null, CancellationToken.None);

            Assert.True(result.Succeeded, string.Join(Environment.NewLine,
                result.Messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Text)));
        }

        /// <summary>
        /// §6.5 and §6.6, which the contract deliberately keeps out of
        /// <c>FemexEntity</c> so that no adapter can declare its way out of them: a
        /// model stating no units is a model whose numbers mean nothing, and SAF
        /// rescales the whole file from that flag.
        /// </summary>
        [Fact]
        public void AModelStatingNoUnits_IsReportedAtErrorSeverity()
        {
            var model = new FemexModel { Units = null };
            model.Levels.Add(new Level { LevelNumber = 1 });

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> result = new SafExporter().Export(
                model, new StreamExportRequest(written), null, CancellationToken.None);

            Assert.Contains(result.Messages,
                            message => message.Severity == ValidationSeverity.Error &&
                                       message.Text.Contains("states no units"));
        }

        [Fact]
        public void AModelMixingMetricAndImperial_IsReportedAtErrorSeverity()
        {
            // Metre with kip is a legal FEMEX statement that SAF has no flag for.
            var model = new FemexModel
            {
                Units = new Units { Length = LengthUnit.Metre, Force = ForceUnit.Kip },
            };

            model.Levels.Add(new Level { LevelNumber = 1 });

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> result = new SafExporter().Export(
                model, new StreamExportRequest(written), null, CancellationToken.None);

            Assert.Contains(result.Messages,
                            message => message.Severity == ValidationSeverity.Error &&
                                       message.Text.Contains("one system"));
        }

        /// <summary>
        /// §5.3: the uid to native-handle mapping is carried as data, because a batch
        /// run over forty models has nowhere sensible to scatter forty sidecars.
        /// </summary>
        [Fact]
        public void TheExportReceipt_MapsUidsToTheSafNamesTheyWereWrittenUnder()
        {
            FemexModel model = new SafImporter()
                .Import(new StreamImportRequest(SafCorpus.Open(SafCorpus.Reference)), null,
                        CancellationToken.None).Value!;

            using var written = new MemoryStream();
            ExportReceipt receipt = new SafExporter().Export(
                model, new StreamExportRequest(written) { DestinationName = "out.xlsx" },
                null, CancellationToken.None).Value!;

            Assert.Equal("out.xlsx", receipt.DestinationName);
            Assert.NotEmpty(receipt.NativeHandles);

            Guid barUid = model.Bars[0].Uid!.Value;
            Assert.True(receipt.NativeHandles.ContainsKey(barUid),
                        "A bar the exporter wrote is missing from the receipt.");
        }

        /// <summary>
        /// §7.3's name-stability rule, applied to a model whose bars and nodes have
        /// no names at all: the same model exported twice produces the same names.
        /// </summary>
        [Fact]
        public void SynthesisedNames_AreStableAcrossTwoExportsOfTheSameModel()
        {
            FemexModel model = new SafImporter()
                .Import(new StreamImportRequest(SafCorpus.Open(SafCorpus.Reference)), null,
                        CancellationToken.None).Value!;

            using var first = new MemoryStream();
            ExportReceipt one = new SafExporter()
                .Export(model, new StreamExportRequest(first), null, CancellationToken.None).Value!;

            using var second = new MemoryStream();
            ExportReceipt two = new SafExporter()
                .Export(model, new StreamExportRequest(second), null, CancellationToken.None).Value!;

            Assert.Equal(one.NativeHandles.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value),
                         two.NativeHandles.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value));
        }

        /// <summary>
        /// §7.3's capability honesty: an entity the adapter declares it carries must
        /// actually arrive, and one it does not declare must not be claimed.
        /// </summary>
        [Fact]
        public void TheDeclaredCapabilities_MatchWhatTheAdapterActuallyCarries()
        {
            AdapterCapabilities capabilities = new SafImporter().Capabilities;

            // SAF has no grid concept and no mesh. Declaring either would be the
            // dishonesty §7.3 exists to catch.
            Assert.Equal(TransferDirection.None, capabilities.For(FemexEntity.Grid));
            Assert.Equal(TransferDirection.None, capabilities.For(FemexEntity.Mesh));

            FemexModel model = new SafImporter()
                .Import(new StreamImportRequest(SafCorpus.Open(SafCorpus.Reference)), null,
                        CancellationToken.None).Value!;

            Assert.True(capabilities.Supports(FemexEntity.Bar, TransferDirection.Import));
            Assert.NotEmpty(model.Bars);

            Assert.True(capabilities.Supports(FemexEntity.Plate, TransferDirection.Import));
            Assert.NotEmpty(model.Plates);

            Assert.True(capabilities.Supports(FemexEntity.Hinge, TransferDirection.Import));
            Assert.NotEmpty(model.Hinges);

            Assert.Empty(model.Grids);
            Assert.Null(model.Mesh);
        }

        [Fact]
        public void TheAdapterStatesWhichSpecificationVersionsItReadsAndWrites()
        {
            // The SDK reads 1.0.0 to 2.3.0 and writes 2.3.0. Not 2.2.0, and not the
            // adapter's choice.
            Assert.Equal("1.0.0-2.3.0", new SafImporter().Info.TargetProgramVersion);
            Assert.Equal("2.3.0", new SafExporter().Info.TargetProgramVersion);
            Assert.Equal(FemexModel.CurrentSchemaVersion, new SafImporter().Info.SchemaVersion);
        }

        /// <summary>
        /// A model from a later schema exports with a <i>Stale</i> message rather
        /// than being refused, per §4.5.
        /// </summary>
        [Fact]
        public void AModelFromALaterSchema_IsReportedStale_NotRefused()
        {
            var model = new FemexModel { SchemaVersion = "99.0" };
            model.Levels.Add(new Level { LevelNumber = 1 });

            using var written = new MemoryStream();
            TransferResult<ExportReceipt> result = new SafExporter().Export(
                model, new StreamExportRequest(written), null, CancellationToken.None);

            Assert.True(result.Succeeded);
            Assert.Contains(result.Messages, message => message.Category == LossCategory.Stale);
        }

        /// <summary>
        /// The eleven manufactured errors, and the assertion that they were the
        /// adapter's and not the workbook's.
        /// </summary>
        /// <remarks>
        /// Before 1.11 an edge-hosted curve action arrived with a local direction and
        /// a pair of positions and no host to measure either against, because
        /// <c>LinearLoad</c> could name a bar and nothing else. The house workbook is
        /// SCIA's own file: an engineer converting it saw twenty-five errors, eleven
        /// of which were about the tool. This is the file, and the count.
        /// </remarks>
        [Fact]
        public void EdgeHostedLineLoads_NameTheirPlate_AndManufactureNoFinding()
        {
            FemexModel model = new SafImporter()
                .Import(new StreamImportRequest(SafCorpus.Open(SafCorpus.Reference)), null,
                        CancellationToken.None).Value!;

            var hosted = model.Loads.OfType<griffel_femex.Loads.LinearLoad>()
                .Where(load => load.PlateId.HasValue)
                .ToList();

            Assert.NotEmpty(hosted);
            Assert.All(hosted, load => Assert.Null(load.BarId));

            // At least one of them sits on a region rather than on the plate's own
            // contour — the house file's opening and subregion edges — so the pair
            // is exercised and not merely present.
            Assert.Contains(hosted, load => load.RegionId.HasValue);

            Assert.DoesNotContain(model.Validate(ValidationSeverity.Error),
                                  finding => finding.Text.Contains("Linear load"));
        }

        private sealed class LiveSessionRequest : ImportRequest
        {
        }
    }
}
