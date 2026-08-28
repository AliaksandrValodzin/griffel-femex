using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using griffel_femex.Adapters.Saf;
using griffel_femex.Interop;
using griffel_femex.Reporting;

namespace griffel_femex.Cli
{
    /// <summary>
    /// <c>femex convert model.xlsx</c> and <c>femex convert model.femex</c> — C5's
    /// third verb, and the reason the same binary is how a conversion is done at all.
    ///
    /// <b>Conversion is not gated</b>, by decision 2: it is the giveaway, and gating
    /// it would gate the free tier the business model wants as the top of the funnel.
    /// There is no licence check here and there is not meant to be one.
    ///
    /// <b>Every conversion produces a report as well as a file.</b> A converted model
    /// with no loss report is precisely the artefact <c>FEMEX_Adapters.md</c> §4.3
    /// warns about — from inside the adapter an invention does not feel like a loss,
    /// it feels like success — so the report is not an option the caller can forget
    /// to ask for.
    ///
    /// <b>The converted model is not stamped with a producer.</b> Tempting, and
    /// wrong: <see cref="FileMetadata"/> says why <c>ToJson</c> refuses to stamp a
    /// timestamp — the same model converted twice would produce two different files,
    /// and the round-trip determinism the whole SAF phase was spent establishing
    /// would be undone by the driver rather than by the adapter. The provenance of
    /// the conversion is in the report, where it can carry a hash of both files.
    /// </summary>
    internal static class ConvertCommand
    {
        public static int Run(CommandLine line, IReadOnlyList<string> inputs, TextWriter output,
                              TextWriter progress, TextWriter error)
        {
            if (line.To is not null && inputs.Count > 1)
            {
                error.WriteLine("--to names one file, and this run has " + inputs.Count +
                                " inputs. Use --out to convert several at once.");
                return ExitCode.ToolFailure;
            }

            string generatedAt = ReportProvenance.Now();
            var entries = new List<ReportIndexEntry>();
            int worst = ExitCode.Clean;

            foreach (string path in inputs)
            {
                string destination = Destination(path, line);

                if (string.Equals(Path.GetFullPath(destination), Path.GetFullPath(path),
                                  StringComparison.OrdinalIgnoreCase))
                {
                    error.WriteLine($"{Path.GetFileName(path)} would be converted over itself.");
                    return ExitCode.ToolFailure;
                }

                AssuranceReport report = Convert(path, destination, generatedAt, progress);

                ReportIndexEntry? entry = ReportOutput.Emit(report, line, Path.GetFileNameWithoutExtension(path),
                                                            output, progress);
                if (entry is not null)
                    entries.Add(entry);

                if (!report.IsClean)
                    worst = ExitCode.Findings;
            }

            ReportOutput.EmitIndex(entries, line, new ReportProvenance(null, generatedAt), progress);

            return worst;
        }

        /// <summary>
        /// Where the converted model goes: <c>--to</c> if it was given, otherwise
        /// beside the reports under <c>--out</c>, otherwise beside the input. The
        /// extension is the other format's, which is the whole of the naming rule.
        /// </summary>
        internal static string Destination(string path, CommandLine line)
        {
            if (line.To is not null)
                return line.To;

            string name = Path.GetFileNameWithoutExtension(path) +
                          (ModelReader.IsWorkbook(path) ? ".femex" : ".xlsx");

            string directory = line.OutputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(path))!;
            return Path.Combine(directory, name);
        }

        private static AssuranceReport Convert(string path, string destination, string generatedAt,
                                               TextWriter progress)
        {
            ReadResult read = ModelReader.Read(path);

            if (read.Model is null)
            {
                // Nothing crossed, so there is nothing to write. The report says
                // what stopped it, and the exit code is 1 rather than 2.
                TransferSection? failedLeg = read.ImportLeg is null
                    ? null
                    : new TransferSection(Routes.Import(read.ImportLeg), import: read.ImportLeg);

                return new AssuranceReport(
                    new ReportProvenance(new[] { read.Source }, generatedAt),
                    CheckSection.Unreadable(read.Source.Name, read.Failure ?? "no reason was given"),
                    null,
                    failedLeg);
            }

            return ModelReader.IsWorkbook(path)
                ? WriteFemex(read, destination, generatedAt, progress)
                : WriteWorkbook(read, destination, generatedAt, progress);
        }

        /// <summary>
        /// SAF in, FEMEX out. The import already happened in
        /// <see cref="ModelReader"/> — reading a workbook <i>is</i> importing it —
        /// so all that is left is to write the model down.
        /// </summary>
        private static AssuranceReport WriteFemex(ReadResult read, string destination, string generatedAt,
                                                  TextWriter progress)
        {
            FemexModel model = read.Model!;

            EnsureDirectory(destination);

            string json = model.ToJson();
            File.WriteAllText(destination, json, new UTF8Encoding(false));
            progress.WriteLine($"{read.Source.Name}  →  {Path.GetFileName(destination)}");

            var written = SourceFile.FromBytes(Path.GetFileName(destination),
                                               File.ReadAllBytes(destination), model);

            var transfer = read.ImportLeg is null
                ? null
                : new TransferSection(Routes.Import(read.ImportLeg), import: read.ImportLeg);

            return new AssuranceReport(
                new ReportProvenance(new[] { read.Source, written }, generatedAt),
                new CheckSection(model.Validate()),
                null,
                transfer);
        }

        /// <summary>
        /// FEMEX in, SAF out. The export happens here, because nothing else in the
        /// tool needed it.
        /// </summary>
        private static AssuranceReport WriteWorkbook(ReadResult read, string destination, string generatedAt,
                                                     TextWriter progress)
        {
            FemexModel model = read.Model!;
            var exporter = new SafExporter();
            string name = Path.GetFileName(destination);

            TransferResult<ExportReceipt> exported;
            byte[] bytes;

            using (var stream = new MemoryStream())
            {
                exported = exporter.Export(model, new StreamExportRequest(stream) { DestinationName = name },
                                           null, CancellationToken.None);
                bytes = stream.ToArray();
            }

            var leg = new TransferLeg(TransferDirection.Export, exporter.Info, read.Source.Name, name,
                                      exported.Succeeded, exported.Messages);

            var sources = new List<SourceFile> { read.Source };

            if (exported.Succeeded)
            {
                EnsureDirectory(destination);
                File.WriteAllBytes(destination, bytes);
                progress.WriteLine($"{read.Source.Name}  →  {name}");
                sources.Add(SourceFile.FromBytes(name, bytes));
            }

            return new AssuranceReport(
                new ReportProvenance(sources, generatedAt),
                new CheckSection(model.Validate()),
                null,
                new TransferSection(Routes.Export(leg), export: leg));
        }

        private static void EnsureDirectory(string path)
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory!);
        }
    }
}
