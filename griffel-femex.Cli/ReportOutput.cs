using System.Collections.Generic;
using System.IO;
using griffel_femex.Reporting;

namespace griffel_femex.Cli
{
    /// <summary>
    /// Where a finished report goes: a file per model under <c>--out</c>, or the
    /// summary block on the terminal when no <c>--out</c> was given.
    ///
    /// <b>The terminal is a receipt, not a deliverable.</b> C2 is that the report is
    /// one self-contained HTML file, and a run with no <c>--out</c> deliberately
    /// produces no document at all — it says what was found and where to ask for it
    /// in writing. The one exception is <c>--format json</c>, which is asked for by
    /// something that is going to read the output it was given.
    ///
    /// <b>Which is why progress and report are two writers.</b> When the report
    /// itself is going to stdout, a single line of "converted this, wrote that" in
    /// front of it makes the whole stream unparseable — and a batch driver whose
    /// JSON a consumer has to strip a preamble off is a batch driver nobody pipes
    /// twice.
    /// </summary>
    internal static class ReportOutput
    {
        /// <summary>
        /// Emits one report. Returns the index row when a file was written, and null
        /// when the report went to the terminal — a run with no files has nothing to
        /// index.
        /// </summary>
        public static ReportIndexEntry? Emit(AssuranceReport report, CommandLine line, string baseName,
                                             TextWriter output, TextWriter progress)
        {
            if (line.OutputDirectory is null)
            {
                switch (line.Format)
                {
                    case ReportFormat.Json:
                        output.WriteLine(JsonReport.Render(report));
                        break;

                    case ReportFormat.Html:
                        // Asked for HTML with nowhere to put it: the document, on
                        // stdout, so it can be piped somewhere. Silently downgrading
                        // to text would be answering a different question.
                        output.WriteLine(HtmlReport.Render(report));
                        break;

                    default:
                        output.WriteLine(TextReport.Render(report, findings: true));
                        break;
                }

                return null;
            }

            Directory.CreateDirectory(line.OutputDirectory);

            string fileName = baseName + ".report" + Extension(line.Format);
            string path = Path.Combine(line.OutputDirectory, fileName);

            switch (line.Format)
            {
                case ReportFormat.Json:
                    JsonReport.Write(report, path);
                    break;

                case ReportFormat.Text:
                    File.WriteAllText(path, TextReport.Render(report, findings: true));
                    break;

                default:
                    HtmlReport.Write(report, path);
                    break;
            }

            // One line per model as the batch proceeds, so a run over forty models
            // is legible while it is running rather than only after it.
            progress.WriteLine($"{report.SubjectName}  {Summarise(report)}  →  {fileName}");

            return ReportIndexEntry.From(report, fileName);
        }

        /// <summary>
        /// C4's summary index — written whenever more than one model was reported
        /// on. One model needs no index, and a folder containing a report and an
        /// index that points at only it is a folder with a redundant file in it.
        /// </summary>
        public static void EmitIndex(IReadOnlyList<ReportIndexEntry> entries, CommandLine line,
                                     ReportProvenance provenance, TextWriter progress)
        {
            if (line.OutputDirectory is null || entries.Count < 2)
                return;

            bool json = line.Format == ReportFormat.Json;
            string fileName = json ? "index.json" : "index.html";
            string path = Path.Combine(line.OutputDirectory, fileName);

            if (json)
                File.WriteAllText(path, ReportIndex.RenderJson(entries, provenance));
            else
                ReportIndex.Write(entries, path, provenance);

            progress.WriteLine($"{entries.Count} models  →  {fileName}");
        }

        private static string Extension(ReportFormat format)
        {
            switch (format)
            {
                case ReportFormat.Json: return ".json";
                case ReportFormat.Text: return ".txt";
                default: return ".html";
            }
        }

        private static string Summarise(AssuranceReport report)
        {
            var parts = new List<string>();
            foreach (ReportSummaryRow row in report.Summary())
                parts.Add(row.Section.ToLowerInvariant() + " " + row.Detail);

            return parts.Count == 0 ? "nothing to report" : string.Join(" · ", parts);
        }
    }
}
