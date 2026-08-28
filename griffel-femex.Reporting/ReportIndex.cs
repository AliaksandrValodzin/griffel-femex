using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace griffel_femex.Reporting
{
    /// <summary>One model's line in a batch index.</summary>
    public sealed class ReportIndexEntry
    {
        public ReportIndexEntry(string name, string href, bool clean, string summary,
                               string? sha256 = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An index entry names a model.", nameof(name));

            Name = name;
            Href = href;
            Clean = clean;
            Summary = summary;
            Sha256 = sha256;
        }

        public string Name { get; }

        /// <summary>
        /// The report beside the index, as a relative path. Relative rather than
        /// absolute so the whole folder can be zipped and handed over, which is what
        /// a migration engagement actually delivers.
        /// </summary>
        public string Href { get; }

        public bool Clean { get; }

        /// <summary>Every section's count on one line, already worded.</summary>
        public string Summary { get; }

        public string? Sha256 { get; }

        public string? ShortHash => Sha256 is null || Sha256.Length < 8 ? Sha256 : Sha256.Substring(0, 8);

        /// <summary>
        /// The line a finished report deserves: its own summary rows, joined, so the
        /// index says the same thing the report does.
        /// </summary>
        public static ReportIndexEntry From(AssuranceReport report, string href)
        {
            if (report is null)
                throw new ArgumentNullException(nameof(report));

            IReadOnlyList<ReportSummaryRow> rows = report.Summary();
            string summary = rows.Count == 0
                ? "nothing to report"
                : string.Join(" · ", rows.Select(row => row.Section + " " + row.Detail));

            return new ReportIndexEntry(report.SubjectName, href, report.IsClean, summary,
                                        report.Provenance.Subject?.Sha256);
        }
    }

    /// <summary>
    /// C4: <c>femex check *.femex --out reports/</c> produces N reports and one
    /// summary index.
    ///
    /// <b>This is what a migration engagement runs</b>, and it is the reason this
    /// layer is a CLI rather than a service: forty models on one machine, forty
    /// documents handed back, and one page that says which of the forty are worth
    /// opening. A tool that could only report on one file at a time would be a tool
    /// the engagement wraps in a shell script and then owns the summary of.
    ///
    /// The index is one self-contained HTML file, like the reports it points at, and
    /// links to them by relative path so the folder travels as a unit.
    /// </summary>
    public static class ReportIndex
    {
        public const string DefaultTitle = "Model Assurance Index";

        public static string Render(IEnumerable<ReportIndexEntry> entries, ReportProvenance? provenance = null,
                                    string? title = null)
        {
            if (entries is null)
                throw new ArgumentNullException(nameof(entries));

            var rows = new List<ReportIndexEntry>(entries);
            provenance ??= new ReportProvenance();
            title ??= DefaultTitle;

            var html = new StringBuilder();
            html.Append("<!DOCTYPE html>").Append('\n');
            html.Append("<html lang=\"en\">").Append('\n');
            html.Append("<head>").Append('\n');
            html.Append("<meta charset=\"utf-8\">").Append('\n');
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">").Append('\n');
            html.Append("<title>").Append(HtmlReport.Escape(title)).Append("</title>").Append('\n');
            html.Append("<style>").Append('\n').Append(Style).Append('\n').Append("</style>").Append('\n');
            html.Append("</head>").Append('\n');
            html.Append("<body>").Append('\n');

            html.Append("<h1>").Append(HtmlReport.Escape(title)).Append("</h1>").Append('\n');
            html.Append("<p class=\"headline\">");
            html.Append(rows.Count).Append(rows.Count == 1 ? " model · " : " models · ");
            html.Append(HtmlReport.Escape(provenance.Date));
            html.Append(" · ").Append(HtmlReport.Escape(provenance.ToolName + " " + provenance.ToolVersion));
            html.Append("</p>").Append('\n');

            html.Append("<table>").Append('\n');
            html.Append("<tr><th>Model</th><th>sha256</th><th>Findings</th></tr>").Append('\n');

            foreach (ReportIndexEntry entry in rows)
            {
                html.Append("<tr>");
                html.Append("<td><a href=\"").Append(HtmlReport.Escape(entry.Href)).Append("\">");
                html.Append(HtmlReport.Escape(entry.Name)).Append("</a></td>");
                html.Append("<td class=\"hash\">").Append(HtmlReport.Escape(entry.ShortHash ?? "—")).Append("</td>");
                html.Append("<td class=\"").Append(entry.Clean ? "clean" : "findings").Append("\">");
                html.Append(HtmlReport.Escape(entry.Summary)).Append("</td>");
                html.Append("</tr>").Append('\n');
            }

            html.Append("</table>").Append('\n');
            html.Append("</body>").Append('\n');
            html.Append("</html>").Append('\n');

            return html.ToString();
        }

        public static void Write(IEnumerable<ReportIndexEntry> entries, string path,
                                 ReportProvenance? provenance = null, string? title = null)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            File.WriteAllText(path, Render(entries, provenance, title), new UTF8Encoding(false));
        }

        /// <summary>The same index, for something that is not a person.</summary>
        public static string RenderJson(IEnumerable<ReportIndexEntry> entries, ReportProvenance? provenance = null)
        {
            if (entries is null)
                throw new ArgumentNullException(nameof(entries));

            provenance ??= new ReportProvenance();

            var document = new Dictionary<string, object?>
            {
                ["generatedAt"] = provenance.GeneratedAt,
                ["tool"] = provenance.ToolName,
                ["toolVersion"] = provenance.ToolVersion,
                ["schemaVersion"] = provenance.SchemaVersion,
                ["models"] = entries.Select(entry => new Dictionary<string, object?>
                {
                    ["name"] = entry.Name,
                    ["report"] = entry.Href,
                    ["clean"] = entry.Clean,
                    ["summary"] = entry.Summary,
                    ["sha256"] = entry.Sha256,
                }).ToList(),
            };

            return JsonSerializer.Serialize(document, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        private const string Style = @"
:root { --ink: #1a1a1a; --muted: #5c5c5c; --rule: #d8d8d8; --page: #ffffff; --findings: #8a1c1c; }
@media (prefers-color-scheme: dark) {
  :root { --ink: #e8e8e6; --muted: #a0a09c; --rule: #3a3a38; --page: #131312; --findings: #ef8b8b; }
}
body {
  margin: 0 auto; padding: 2.5rem 1.5rem 4rem; max-width: 60rem;
  font: 15px/1.55 -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  color: var(--ink); background: var(--page);
}
h1 { font-size: 1.5rem; margin: 0 0 .3rem; }
.headline { color: var(--muted); font-size: .875rem; margin: 0 0 1.5rem; }
table { border-collapse: collapse; width: 100%; font-size: .875rem; }
th, td { text-align: left; vertical-align: top; padding: .4rem .6rem .4rem 0; }
th { color: var(--muted); font-size: .75rem; text-transform: uppercase; letter-spacing: .04em; }
tr + tr { border-top: 1px solid var(--rule); }
a { color: inherit; }
.hash { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: .8125rem; }
.clean { color: var(--muted); }
.findings { color: var(--findings); }
@media print { body { max-width: none; padding: 0; color: #000; background: #fff; } }
";
    }
}
