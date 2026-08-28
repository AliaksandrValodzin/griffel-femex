using System.Collections.Generic;
using System.IO;
using griffel_femex.Interop;
using griffel_femex.Reporting;

namespace griffel_femex.Cli
{
    /// <summary>
    /// <c>femex check model.femex</c> — and <c>femex check *.femex --out reports/</c>,
    /// which is C4 and is what a migration engagement actually runs.
    ///
    /// The verb reads a model and says what is wrong with it. A SAF workbook is a
    /// legitimate input: it is imported first, and the report then carries both what
    /// the crossing cost and what the resulting model says about itself — which is
    /// the only way to tell a finding about the structure from a finding about what
    /// SAF could not carry.
    /// </summary>
    internal static class CheckCommand
    {
        public static int Run(CommandLine line, IReadOnlyList<string> inputs, TextWriter output,
                              TextWriter progress)
        {
            // One timestamp for the whole batch. Forty reports produced by one run
            // are one act, and forty timestamps a few milliseconds apart would
            // suggest forty.
            string generatedAt = ReportProvenance.Now();

            var entries = new List<ReportIndexEntry>();
            int worst = ExitCode.Clean;

            foreach (string path in inputs)
            {
                AssuranceReport report = Build(path, generatedAt);

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
        /// One model's report. A file that could not be read still produces one —
        /// P4 and C4 between them require that an unreadable <c>.femex</c> is a
        /// finding rather than a crash, and a finding with no report to put it in
        /// would be a finding nobody sees.
        /// </summary>
        internal static AssuranceReport Build(string path, string generatedAt)
        {
            ReadResult read = ModelReader.Read(path);

            CheckSection check = read.Model is null
                ? CheckSection.Unreadable(read.Source.Name, read.Failure ?? "no reason was given")
                : new CheckSection(read.Model.Validate());

            TransferSection? transfer = read.ImportLeg is null
                ? null
                : new TransferSection(Routes.Import(read.ImportLeg), import: read.ImportLeg);

            var provenance = new ReportProvenance(new[] { read.Source }, generatedAt);

            return new AssuranceReport(provenance, check, null, transfer);
        }
    }

    /// <summary>
    /// How a crossing is named in the report — <c>SAF → FEMEX</c>, and its mirror.
    /// Built from the adapter's own <see cref="AdapterInfo.Name"/> rather than from
    /// a constant, so the day there is a second adapter the route line does not
    /// still say SAF. The name rather than the target program, because
    /// <c>Structural Analysis Format → FEMEX</c> is the same sentence at three times
    /// the width, and the full name is in the provenance section already.
    /// </summary>
    internal static class Routes
    {
        public static string Import(TransferLeg leg) => leg.Adapter.Name + " → FEMEX";

        public static string Export(TransferLeg leg) => "FEMEX → " + leg.Adapter.Name;
    }
}
