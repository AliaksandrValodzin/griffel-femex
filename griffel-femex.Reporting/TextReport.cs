using System.Collections.Generic;
using System.Text;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// The opening block, for a terminal — C1's sketch, rendered:
    ///
    /// <code>
    /// Model Assurance Report · steel-hall.femex · 2026-08-21 · femex 0.1.0 · sha256 3f9a…
    ///
    ///   Check       Validate()                          14 findings — 2 error · 12 warning
    ///   Compare     vs steel-hall-2026-08-14.femex      6 differences
    ///   Transfer    SAF → FEMEX → SAF                   14 losses
    /// </code>
    ///
    /// The third view of one <see cref="AssuranceReport"/>, and the reason the
    /// summary is data on the report rather than strings built by a writer: a
    /// terminal line that disagreed with the document it just wrote would be the
    /// cheapest possible way to lose a reader's trust in both.
    ///
    /// <b>Not a report.</b> It is the receipt for one, and <c>--out</c> is how the
    /// document itself is obtained.
    /// </summary>
    public static class TextReport
    {
        /// <param name="findings">
        /// Whether to list what was found under the summary. A run with no
        /// <c>--out</c> asked to be told rather than to be given a document, so it
        /// gets the lines; a run that wrote a report gets the receipt alone.
        /// </param>
        public static string Render(AssuranceReport report, bool findings = false)
        {
            if (report is null)
                throw new ArgumentNullException(nameof(report));

            var text = new StringBuilder();
            text.Append(report.Headline()).Append('\n');

            IReadOnlyList<ReportSummaryRow> rows = report.Summary();
            if (rows.Count > 0)
                text.Append('\n');

            foreach (ReportSummaryRow row in rows)
            {
                text.Append("  ").Append(Pad(row.Section, 12));
                text.Append(Pad(row.Subject, 36));
                text.Append(row.Detail).Append('\n');
            }

            if (!findings)
                return text.ToString();

            // Judgement first, as in the document — for the same reason, which is
            // that a terminal window shows the top of a list and a reader scrolls
            // for the rest.
            if (report.Check is not null && !report.Check.IsClean)
            {
                text.Append('\n');
                foreach (ValidationCategory category in report.Check.Categories)
                {
                    foreach (ValidationMessage finding in report.Check.OfCategory(category))
                    {
                        text.Append("  ").Append(Pad(finding.Severity.ToString().ToLowerInvariant(), 9));
                        text.Append(Pad(finding.Category.ToString().ToLowerInvariant(), 13));
                        text.Append(finding.Text).Append('\n');
                    }
                }
            }

            if (report.Compare is not null && !report.Compare.IsIdentical)
            {
                text.Append('\n');
                foreach (Comparison.ModelDifference difference in report.Compare.Differences)
                {
                    text.Append("  ").Append(Pad(difference.Kind.ToString().ToLowerInvariant(), 15));
                    text.Append(difference.Text).Append('\n');
                }
            }

            if (report.Transfer is not null)
            {
                foreach (TransferLeg leg in report.Transfer.Legs)
                {
                    if (leg.Messages.Count == 0)
                        continue;

                    text.Append('\n').Append("  ").Append(leg.Label).Append("  ").Append(leg.Subject).Append('\n');

                    foreach (Interop.TransferMessage message in leg.Messages)
                    {
                        string category = message.Category?.ToString().ToLowerInvariant() ?? "failure";
                        text.Append("  ").Append(Pad(category, 15));
                        text.Append(Pad(message.Subject.HasValue ? message.Subject.Value.ToString() : "model", 20));
                        text.Append(message.Text).Append('\n');
                    }
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// Left-aligned in a fixed column, and never truncated: a file name cut off
        /// at 36 characters is a file name a reader cannot look up, and a ragged
        /// column is a smaller problem than a wrong one.
        /// </summary>
        private static string Pad(string text, int width)
        {
            if (text.Length >= width)
                return text + " ";

            return text + new string(' ', width - text.Length);
        }
    }
}
