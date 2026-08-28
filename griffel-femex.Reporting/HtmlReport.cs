using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using griffel_femex.Comparison;
using griffel_femex.Interop;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// An <see cref="AssuranceReport"/> as one self-contained HTML file.
    ///
    /// <b>C2: no dependencies, no build step, opens from <c>file://</c>, survives
    /// being emailed, still opens in five years.</b> That is the viewer's founding
    /// property, adopted here for the same audience and the same reason — a report a
    /// firm files against a project is read years after the tool that made it was
    /// last installed. So: no stylesheet link, no script tag, no font request, no
    /// image, no CDN. Everything is in the one file, and there is nothing in it a
    /// browser has to fetch.
    ///
    /// <b>And no JavaScript at all</b>, which is stronger than the viewer needs to
    /// be and is right here: the viewer is an application and this is a document.
    /// A document that needs a script to show its own contents is a document that
    /// can be broken by a browser setting, a mail client's sanitiser, or a PDF
    /// print. Every finding is in the markup, expanded, in reading order.
    ///
    /// <b>Decision 9 is enforced by what this file does not contain.</b> The report
    /// states findings and provenance. It draws no conclusion, offers no engineering
    /// opinion, and does not use the word <i>certify</i>.
    /// </summary>
    public static class HtmlReport
    {
        /// <summary>The whole document, as a string.</summary>
        public static string Render(AssuranceReport report)
        {
            if (report is null)
                throw new ArgumentNullException(nameof(report));

            var html = new StringBuilder();

            html.Append("<!DOCTYPE html>").Append('\n');
            html.Append("<html lang=\"en\">").Append('\n');
            RenderHead(html, report);
            html.Append("<body>").Append('\n');

            RenderHeader(html, report);

            if (report.Check is not null)
                RenderCheck(html, report.Check);

            if (report.Compare is not null)
                RenderCompare(html, report.Compare);

            if (report.Transfer is not null)
                RenderTransfer(html, report.Transfer);

            RenderProvenance(html, report);
            RenderFooter(html, report);

            html.Append("</body>").Append('\n');
            html.Append("</html>").Append('\n');

            return html.ToString();
        }

        /// <summary>
        /// The document, written to a path. UTF-8 without a byte-order mark, matching
        /// every other artefact this repository writes.
        /// </summary>
        public static void Write(AssuranceReport report, string path)
        {
            if (path is null)
                throw new ArgumentNullException(nameof(path));

            File.WriteAllText(path, Render(report), new UTF8Encoding(false));
        }

        // ----- The document -----

        private static void RenderHead(StringBuilder html, AssuranceReport report)
        {
            html.Append("<head>").Append('\n');
            html.Append("<meta charset=\"utf-8\">").Append('\n');
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">").Append('\n');
            html.Append("<title>").Append(Escape(report.Title + " · " + report.SubjectName)).Append("</title>").Append('\n');
            html.Append("<style>").Append('\n').Append(Style).Append('\n').Append("</style>").Append('\n');
            html.Append("</head>").Append('\n');
        }

        private static void RenderHeader(StringBuilder html, AssuranceReport report)
        {
            html.Append("<header>").Append('\n');
            html.Append("<h1>").Append(Escape(report.Title)).Append("</h1>").Append('\n');

            html.Append("<p class=\"headline\">");
            html.Append("<span class=\"subject\">").Append(Escape(report.SubjectName)).Append("</span>");
            html.Append(" · ").Append(Escape(report.Provenance.Date));
            html.Append(" · ").Append(Escape(report.Provenance.ToolName + " " + report.Provenance.ToolVersion));

            string? hash = report.Provenance.Subject?.ShortHash;
            if (hash is not null)
                html.Append(" · <span class=\"hash\">sha256 ").Append(Escape(hash)).Append("</span>");

            html.Append("</p>").Append('\n');

            html.Append("<table class=\"summary\">").Append('\n');
            foreach (ReportSummaryRow row in report.Summary())
            {
                html.Append("<tr>");
                html.Append("<th scope=\"row\">").Append(Escape(row.Section)).Append("</th>");
                html.Append("<td class=\"subject\">").Append(Escape(row.Subject)).Append("</td>");
                html.Append("<td class=\"detail\">").Append(Escape(row.Detail)).Append("</td>");
                html.Append("</tr>").Append('\n');
            }

            html.Append("</table>").Append('\n');
            html.Append("</header>").Append('\n');
        }

        // ----- Check -----

        private static void RenderCheck(StringBuilder html, CheckSection check)
        {
            html.Append("<section id=\"check\">").Append('\n');
            Heading(html, "Check", check.Summary());

            if (check.IsClean)
            {
                html.Append("<p class=\"empty\">Validate() reports nothing about this model.</p>").Append('\n');
                html.Append("</section>").Append('\n');
                return;
            }

            // Judgement first. §4 of FEMEX_BusinessModel.md: the referential half is
            // table stakes and the judgement half is the product, so a reader must be
            // able to see the second without the first burying it.
            foreach (ValidationCategory category in check.Categories)
            {
                IReadOnlyList<ValidationMessage> findings = check.OfCategory(category);

                html.Append("<h3>").Append(Escape(CategoryTitle(category)));
                html.Append(" <span class=\"count\">").Append(findings.Count).Append("</span></h3>").Append('\n');
                html.Append("<p class=\"note\">").Append(Escape(CategoryNote(category))).Append("</p>").Append('\n');

                html.Append("<table class=\"findings\">").Append('\n');
                foreach (ValidationMessage finding in findings)
                {
                    html.Append("<tr>");
                    html.Append("<td>").Append(Badge(finding.Severity.ToString(),
                                                     finding.Severity == ValidationSeverity.Error ? "error" : "warning"));
                    html.Append("</td>");
                    html.Append("<td>").Append(Escape(finding.Text)).Append("</td>");
                    html.Append("</tr>").Append('\n');
                }

                html.Append("</table>").Append('\n');
            }

            html.Append("</section>").Append('\n');
        }

        private static string CategoryTitle(ValidationCategory category)
        {
            switch (category)
            {
                case ValidationCategory.Judgement: return "Engineering judgement";
                case ValidationCategory.Referential: return "Referential integrity";
                default: return "The file itself";
            }
        }

        private static string CategoryNote(ValidationCategory category)
        {
            switch (category)
            {
                case ValidationCategory.Judgement:
                    return "The model is well formed and says something that is more often an oversight " +
                           "than a decision. These are the findings a solver will not raise.";
                case ValidationCategory.Referential:
                    return "References that do not resolve, and keys that are not unique. A receiving " +
                           "program cannot be expected to make sense of these.";
                default:
                    return "What this file declares itself to be, what reading it changed, and how much " +
                           "of it carries the keys another program needs to match it.";
            }
        }

        // ----- Compare -----

        private static void RenderCompare(StringBuilder html, CompareSection compare)
        {
            html.Append("<section id=\"compare\">").Append('\n');
            Heading(html, "Compare", compare.Summary());

            html.Append("<p class=\"note\">Against ").Append(Escape(compare.Baseline.Name));
            if (compare.Baseline.ShortHash is not null)
                html.Append(" (sha256 ").Append(Escape(compare.Baseline.ShortHash)).Append(')');

            html.Append(". Objects are matched by uid, never by id; lists compare as sets under that " +
                        "key, and geometry compares within the coincidence tolerance. Below, the left " +
                        "is this model and the right is the baseline.</p>").Append('\n');

            if (compare.IsIdentical)
            {
                html.Append("<p class=\"empty\">The two models are equivalent.</p>").Append('\n');
                html.Append("</section>").Append('\n');
                return;
            }

            foreach (DifferenceKind kind in compare.Kinds)
            {
                IReadOnlyList<ModelDifference> differences = compare.OfKind(kind);

                html.Append("<h3>").Append(Escape(KindTitle(kind)));
                html.Append(" <span class=\"count\">").Append(differences.Count).Append("</span></h3>").Append('\n');

                html.Append("<table class=\"findings\">").Append('\n');
                html.Append("<tr><th>Object</th><th>Member</th><th>Difference</th></tr>").Append('\n');

                foreach (ModelDifference difference in differences)
                {
                    html.Append("<tr>");
                    html.Append("<td class=\"object\">")
                        .Append(Escape(difference.Subject.HasValue ? difference.Subject.Value.ToString() : "model"))
                        .Append("</td>");
                    html.Append("<td class=\"member\">").Append(Escape(difference.Member ?? "—")).Append("</td>");
                    html.Append("<td>").Append(Escape(difference.Text)).Append("</td>");
                    html.Append("</tr>").Append('\n');
                }

                html.Append("</table>").Append('\n');
            }

            html.Append("</section>").Append('\n');
        }

        private static string KindTitle(DifferenceKind kind)
        {
            switch (kind)
            {
                case DifferenceKind.OnlyInLeft: return "Only in this model";
                case DifferenceKind.OnlyInRight: return "Only in the baseline";
                case DifferenceKind.TypeDiffers: return "Came back a different kind of object";
                case DifferenceKind.MemberDiffers: return "Changed";
                default: return "Carries no uid, so nothing can be matched to it";
            }
        }

        // ----- Transfer -----

        private static void RenderTransfer(StringBuilder html, TransferSection transfer)
        {
            html.Append("<section id=\"transfer\">").Append('\n');
            Heading(html, "Transfer", transfer.Summary());
            html.Append("<p class=\"note\">").Append(Escape(transfer.Route));
            html.Append(". A loss is this crossing being honest about what it could not carry; it is " +
                        "not a defect in the model.</p>").Append('\n');

            foreach (TransferLeg leg in transfer.Legs)
                RenderLeg(html, leg);

            html.Append("</section>").Append('\n');
        }

        private static void RenderLeg(StringBuilder html, TransferLeg leg)
        {
            html.Append("<h3>").Append(Escape(leg.Label));
            html.Append(" <span class=\"subject\">").Append(Escape(leg.Subject)).Append("</span>");
            html.Append(" <span class=\"count\">").Append(Escape(leg.Summary())).Append("</span></h3>").Append('\n');

            html.Append("<p class=\"note\">").Append(Escape(leg.Adapter.Name)).Append(" adapter, ");
            html.Append(Escape(leg.Adapter.TargetProgram));
            if (leg.Adapter.TargetProgramVersion is not null)
                html.Append(' ').Append(Escape(leg.Adapter.TargetProgramVersion));

            html.Append(", built against FEMEX ").Append(Escape(leg.Adapter.SchemaVersion)).Append(".</p>").Append('\n');

            if (leg.Failures.Count > 0)
            {
                html.Append("<table class=\"findings\">").Append('\n');
                foreach (TransferMessage failure in leg.Failures)
                {
                    html.Append("<tr>");
                    html.Append("<td>").Append(Badge("Failure", "error")).Append("</td>");
                    html.Append("<td class=\"object\">")
                        .Append(Escape(failure.Subject.HasValue ? failure.Subject.Value.ToString() : "model"))
                        .Append("</td>");
                    html.Append("<td>").Append(Escape(failure.Text)).Append("</td>");
                    html.Append("</tr>").Append('\n');
                }

                html.Append("</table>").Append('\n');
            }

            if (leg.LossCount == 0)
            {
                if (leg.Succeeded)
                    html.Append("<p class=\"empty\">Nothing was lost on this leg.</p>").Append('\n');

                return;
            }

            html.Append("<table class=\"findings\">").Append('\n');
            html.Append("<tr><th>Category</th><th>Object</th><th>Native</th><th>Loss</th></tr>").Append('\n');

            foreach (LossCategory category in leg.Categories)
            {
                foreach (TransferMessage message in leg.OfCategory(category))
                {
                    html.Append("<tr>");
                    html.Append("<td>").Append(Badge(category.ToString(), "loss")).Append("</td>");
                    html.Append("<td class=\"object\">")
                        .Append(Escape(message.Subject.HasValue ? message.Subject.Value.ToString() : "model"))
                        .Append("</td>");
                    html.Append("<td class=\"member\">").Append(Escape(message.NativeHandle ?? "—")).Append("</td>");
                    html.Append("<td>").Append(Escape(message.Text)).Append("</td>");
                    html.Append("</tr>").Append('\n');
                }
            }

            html.Append("</table>").Append('\n');
        }

        // ----- Provenance -----

        private static void RenderProvenance(StringBuilder html, AssuranceReport report)
        {
            html.Append("<section id=\"provenance\">").Append('\n');
            Heading(html, "Provenance", report.Provenance.GeneratedAt);

            html.Append("<table class=\"provenance\">").Append('\n');
            Row(html, "Produced by", report.Provenance.ToolName + " " + report.Provenance.ToolVersion);
            Row(html, "FEMEX schema", report.Provenance.SchemaVersion);
            Row(html, "Generated at", report.Provenance.GeneratedAt);

            if (report.Transfer is not null)
            {
                foreach (TransferLeg leg in report.Transfer.Legs)
                {
                    string version = leg.Adapter.TargetProgramVersion is null
                        ? leg.Adapter.TargetProgram
                        : leg.Adapter.TargetProgram + " " + leg.Adapter.TargetProgramVersion;

                    Row(html, leg.Label + " adapter",
                        leg.Adapter.Name + " · " + version + " · built against FEMEX " + leg.Adapter.SchemaVersion);
                }
            }

            html.Append("</table>").Append('\n');

            foreach (SourceFile source in report.Provenance.Sources)
                RenderSource(html, source);

            html.Append("</section>").Append('\n');
        }

        private static void RenderSource(StringBuilder html, SourceFile source)
        {
            html.Append("<h3>").Append(Escape(source.Name)).Append("</h3>").Append('\n');
            html.Append("<table class=\"provenance\">").Append('\n');

            Row(html, "sha256", source.Sha256 ?? "— (not read from a file)");

            if (source.ByteCount.HasValue)
                Row(html, "Bytes", source.ByteCount.Value.ToString("N0", CultureInfo.InvariantCulture));

            Row(html, "Declared schema", source.SchemaVersion ?? "— (none stated)");

            FileMetadata? metadata = source.Metadata;
            if (metadata is not null)
            {
                if (metadata.Producer is not null)
                {
                    string producer = metadata.ProducerVersion is null
                        ? metadata.Producer
                        : metadata.Producer + " " + metadata.ProducerVersion;

                    Row(html, "Written by", producer);
                }

                if (metadata.ProjectName is not null)
                    Row(html, "Project", metadata.ProjectName);

                if (metadata.CreatedAt is not null)
                    Row(html, "File written", metadata.CreatedAt);
            }

            html.Append("</table>").Append('\n');
        }

        private static void RenderFooter(StringBuilder html, AssuranceReport report)
        {
            html.Append("<footer>").Append('\n');
            html.Append("<p>This report states what ").Append(Escape(report.Provenance.ToolName));
            html.Append(' ').Append(Escape(report.Provenance.ToolVersion));
            html.Append(" found in the files named above, and the provenance of those files. ");
            html.Append("It is not an engineering opinion, and it is not a substitute for one.</p>").Append('\n');
            html.Append("</footer>").Append('\n');
        }

        // ----- Plumbing -----

        private static void Heading(StringBuilder html, string title, string detail)
        {
            html.Append("<h2>").Append(Escape(title));
            html.Append(" <span class=\"count\">").Append(Escape(detail)).Append("</span></h2>").Append('\n');
        }

        private static void Row(StringBuilder html, string label, string value)
        {
            html.Append("<tr><th scope=\"row\">").Append(Escape(label)).Append("</th>");
            html.Append("<td>").Append(Escape(value)).Append("</td></tr>").Append('\n');
        }

        private static string Badge(string text, string kind)
        {
            return "<span class=\"badge " + kind + "\">" + Escape(text) + "</span>";
        }

        /// <summary>
        /// Every value that reaches the document goes through here. Validation and
        /// loss messages carry model data — a label a user typed, a profile name out
        /// of a catalogue — and a report that pasted one of those into markup would
        /// be a report whose contents depend on what somebody called a load case.
        /// </summary>
        internal static string Escape(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var escaped = new StringBuilder(text!.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    case '&': escaped.Append("&amp;"); break;
                    case '<': escaped.Append("&lt;"); break;
                    case '>': escaped.Append("&gt;"); break;
                    case '"': escaped.Append("&quot;"); break;
                    case '\'': escaped.Append("&#39;"); break;
                    default: escaped.Append(c); break;
                }
            }

            return escaped.ToString();
        }

        /// <summary>
        /// The whole stylesheet, inline. System fonts only — a web font is a network
        /// request, and a network request is the thing this document does not make.
        /// The dark rule is a media query rather than a toggle, because a toggle is
        /// script.
        /// </summary>
        private const string Style = @"
:root {
  --ink: #1a1a1a; --muted: #5c5c5c; --rule: #d8d8d8; --panel: #f6f6f4;
  --error: #8a1c1c; --warning: #7a5200; --loss: #1f4f6f; --page: #ffffff;
}
@media (prefers-color-scheme: dark) {
  :root {
    --ink: #e8e8e6; --muted: #a0a09c; --rule: #3a3a38; --panel: #1d1d1b;
    --error: #ef8b8b; --warning: #d8ae53; --loss: #7fb6d6; --page: #131312;
  }
}
* { box-sizing: border-box; }
body {
  margin: 0 auto; padding: 2.5rem 1.5rem 4rem; max-width: 60rem;
  font: 15px/1.55 -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  color: var(--ink); background: var(--page);
}
h1 { font-size: 1.5rem; margin: 0 0 .3rem; letter-spacing: -.01em; }
h2 {
  font-size: 1.1rem; margin: 2.5rem 0 .4rem; padding-bottom: .35rem;
  border-bottom: 2px solid var(--ink);
}
h3 { font-size: .95rem; margin: 1.6rem 0 .3rem; font-weight: 600; }
p { margin: .35rem 0; }
.headline { color: var(--muted); font-size: .875rem; margin-bottom: 1.5rem; }
.headline .subject, .summary .subject, h3 .subject { color: var(--ink); font-weight: 600; }
.hash { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
.note { color: var(--muted); font-size: .8125rem; max-width: 48rem; }
.empty { color: var(--muted); font-style: italic; }
.count { color: var(--muted); font-weight: 400; font-size: .8125rem; margin-left: .5rem; }
table { border-collapse: collapse; width: 100%; margin: .5rem 0 1rem; font-size: .875rem; }
th, td { text-align: left; vertical-align: top; padding: .35rem .6rem .35rem 0; }
th { font-weight: 600; }
.findings tr + tr, .provenance tr + tr { border-top: 1px solid var(--rule); }
.findings th { color: var(--muted); font-size: .75rem; text-transform: uppercase; letter-spacing: .04em; }
.findings td.object, .findings td.member, .provenance td {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: .8125rem;
  white-space: nowrap;
}
.findings td:last-child { width: 100%; white-space: normal; }
.provenance th { width: 11rem; color: var(--muted); font-weight: 400; }
.provenance td { white-space: normal; word-break: break-all; }
.summary { background: var(--panel); padding: .5rem; margin: 0; }
.summary th { width: 7rem; padding-left: .6rem; }
.summary td.detail { text-align: right; padding-right: .6rem; color: var(--muted); white-space: nowrap; }
.badge {
  display: inline-block; padding: .05rem .4rem; border: 1px solid currentColor;
  border-radius: 2px; font-size: .6875rem; text-transform: uppercase; letter-spacing: .04em;
  white-space: nowrap;
}
.badge.error { color: var(--error); }
.badge.warning { color: var(--warning); }
.badge.loss { color: var(--loss); }
footer {
  margin-top: 3rem; padding-top: .8rem; border-top: 1px solid var(--rule);
  color: var(--muted); font-size: .8125rem;
}
@media print {
  body { max-width: none; padding: 0; color: #000; background: #fff; }
  h2 { break-after: avoid; } h3 { break-after: avoid; }
  tr { break-inside: avoid; }
  .summary { background: #fff; border: 1px solid #000; }
}
";
    }
}
