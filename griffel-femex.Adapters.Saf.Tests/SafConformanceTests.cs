using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using griffel_femex.Interop;
using griffel_femex.Interop.Conformance;
using SAF.DataAccess.Models;
using SAF.DataAccess.Models.Interfaces;
using Xunit;

namespace griffel_femex.Adapters.Saf.Tests
{
    /// <summary>
    /// The Tier-1 conformance harness of <c>FEMEX_Adapters.md</c> §7.3, run against
    /// the SAF adapter.
    /// </summary>
    /// <remarks>
    /// Phase A proved the harness can tell a compliant plugin from a non-compliant
    /// one, using a reference adapter written for the purpose and seven deliberately
    /// broken ones. This is the first time it is pointed at an adapter that talks to
    /// something real — which is the only way to find out whether the rules it
    /// enforces are rules a real adapter can keep.
    /// </remarks>
    public class SafConformanceTests
    {
        [Fact]
        public void TheSafAdapter_PassesEveryTier1Check()
        {
            IReadOnlyList<ConformanceCheck> checks = new SafConformanceHarness().RunTier1();

            var failed = checks.Where(check => !check.Passed && !check.Skipped).ToList();

            Assert.True(failed.Count == 0, string.Join(Environment.NewLine, failed.Select(Describe)));
        }

        [Fact]
        public void EveryTier1Check_Ran_OrSaidWhyItDidNot()
        {
            IReadOnlyList<ConformanceCheck> checks = new SafConformanceHarness().RunTier1();

            // A skip is not a pass. If one appears it must carry a reason, so the
            // suite cannot quietly report green for what it never ran.
            foreach (ConformanceCheck check in checks.Where(check => check.Skipped))
                Assert.NotEmpty(check.Findings);

            Assert.NotEmpty(checks);
        }

        private static string Describe(ConformanceCheck check)
        {
            return check.Name + ": " + check.Rule + Environment.NewLine + "  " +
                   string.Join(Environment.NewLine + "  ", check.Findings.Take(6));
        }

        private sealed class SafConformanceHarness : ConformanceHarness
        {
            protected override IFemexAdapter CreateAdapter() => new SafAdapter();

            protected override ConformanceTransport CreateTransport() => new SafTransport();

            protected override FemexModel CreateGoldenModel()
            {
                return FemexModel.Load(Path.Combine("Examples", "Conformance1.femex"));
            }
        }

        /// <summary>
        /// The harness asks for one adapter object that does both legs; the SAF
        /// adapter is two classes because the contract is two interfaces. This is the
        /// join, and it is a test fixture rather than a shipped type because nothing
        /// in the product needs both halves in one object.
        /// </summary>
        private sealed class SafAdapter : IFemexImporter, IFemexExporter
        {
            private readonly SafImporter _importer = new SafImporter();
            private readonly SafExporter _exporter = new SafExporter();

            public AdapterInfo Info => _importer.Info;

            public AdapterCapabilities Capabilities => _importer.Capabilities;

            public TransferResult<FemexModel> Import(ImportRequest request,
                                                     IProgress<TransferProgress>? progress,
                                                     System.Threading.CancellationToken cancellationToken)
            {
                return _importer.Import(request, progress, cancellationToken);
            }

            public TransferResult<ExportReceipt> Export(FemexModel model, ExportRequest request,
                                                        IProgress<TransferProgress>? progress,
                                                        System.Threading.CancellationToken cancellationToken)
            {
                return _exporter.Export(model, request, progress, cancellationToken);
            }
        }

        /// <summary>
        /// A SAF round trip held in memory, and the reordering §6.2 needs.
        /// </summary>
        /// <remarks>
        /// <see cref="TryBeginReorderedImport"/> is the part that takes thought, and
        /// it is the point of the transport: §6.2's rule cannot be tested without a
        /// source the test can present in a different order, and only something that
        /// knows the format can reorder it. For SAF that means reading the workbook
        /// back through the SDK, reversing the object list, and writing it out again —
        /// the same structure with the sheets walked backwards, which an import
        /// whose answer depends on traversal order will read differently.
        /// </remarks>
        private sealed class SafTransport : ConformanceTransport
        {
            private readonly SafGateway _gateway = new SafGateway();
            private byte[] _written = new byte[0];

            public override ExportRequest BeginExport()
            {
                return new StreamExportRequest(new CapturingStream(this))
                {
                    DestinationName = "conformance.xlsx",
                };
            }

            public override ImportRequest BeginImport()
            {
                return new StreamImportRequest(new MemoryStream(_written, writable: false))
                {
                    SourceName = "conformance.xlsx",
                };
            }

            public override bool TryBeginReorderedImport(out ImportRequest? request)
            {
                request = null;
                if (_written.Length == 0)
                    return false;

                using var source = new MemoryStream(_written, writable: false);
                SafReadResult read = _gateway.Read(source);
                if (read.Model is null)
                    return false;

                // Reversed within each sheet, not across the workbook. Reversing the
                // whole list puts referrers before the rows they name, which the
                // SDK's writer resolves differently — so the file that came back
                // would be a different file and the check would be testing that
                // instead. Within a sheet the rows are unordered by construction, so
                // this is the same model presented backwards, which is what §6.2 asks
                // for.
                var reordered = new List<IExcelModuleObject>();
                foreach (IGrouping<Type, IExcelModuleObject> sheet in
                         read.Model.Objects.GroupBy(item => item.GetType()))
                {
                    reordered.AddRange(sheet.Reverse());
                }

                var model = new ExcelModel(reordered, new ExcelValidationResult[0],
                                           read.Model.SystemOfUnits);

                using var destination = new MemoryStream();
                if (!_gateway.Write(destination, model).Succeeded)
                    return false;

                request = new StreamImportRequest(new MemoryStream(destination.ToArray(), writable: false))
                {
                    SourceName = "conformance.xlsx (reversed)",
                };

                return true;
            }

            private sealed class CapturingStream : MemoryStream
            {
                private readonly SafTransport _owner;

                internal CapturingStream(SafTransport owner)
                {
                    _owner = owner;
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    base.Write(buffer, offset, count);
                    _owner._written = ToArray();
                }

                public override void Flush()
                {
                    base.Flush();
                    _owner._written = ToArray();
                }
            }
        }
    }
}
