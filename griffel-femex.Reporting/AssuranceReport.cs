using System.Collections.Generic;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// One line of the report's opening block — the section, what it was run
    /// against, and the count.
    /// </summary>
    public sealed class ReportSummaryRow
    {
        public ReportSummaryRow(string section, string subject, string detail)
        {
            Section = section;
            Subject = subject;
            Detail = detail;
        }

        /// <summary>Check, Compare or Transfer.</summary>
        public string Section { get; }

        /// <summary>What it was run against — <c>Validate()</c>, a baseline file, a route.</summary>
        public string Subject { get; }

        /// <summary>The count, already worded — <c>14 findings — 2 error · 12 warning</c>.</summary>
        public string Detail { get; }
    }

    /// <summary>
    /// What <c>femex</c> produces: one model, said three ways.
    ///
    /// <b>C1's three sections map to the three claims of
    /// <c>FEMEX_BusinessModel.md</c> §3</b> — Check is what is wrong with this
    /// model, Compare is what changed in it, Transfer is what a crossing did to it —
    /// and each is present only when it was asked for. A report of one section is
    /// the ordinary case; a report of three is a migration engagement.
    ///
    /// <b>This type renders nothing.</b> It is the document as data, and
    /// <see cref="HtmlReport"/> and <see cref="JsonReport"/> are two views of it. The
    /// alternative — a writer that walks a model and builds strings as it goes —
    /// makes the JSON form a second implementation of the same document, free to
    /// disagree with the HTML about what the report says, which is the failure the
    /// <c>--format json</c> option exists to avoid rather than to cause.
    ///
    /// <b>Decision 9 governs every string that reaches it.</b> The report states
    /// findings and provenance; it does not offer an engineering opinion, and the
    /// word <i>certify</i> does not appear in it — not until the professional
    /// indemnity question has an answer.
    /// </summary>
    public sealed class AssuranceReport
    {
        public const string DefaultTitle = "Model Assurance Report";

        public AssuranceReport(ReportProvenance provenance, CheckSection? check = null,
                               CompareSection? compare = null, TransferSection? transfer = null,
                               string? title = null)
        {
            if (provenance is null)
                throw new ArgumentNullException(nameof(provenance));
            if (check is null && compare is null && transfer is null)
                throw new ArgumentException("A report says something.", nameof(check));

            Provenance = provenance;
            Check = check;
            Compare = compare;
            Transfer = transfer;
            Title = title ?? DefaultTitle;
        }

        public string Title { get; }

        public ReportProvenance Provenance { get; }

        /// <summary>Claim 1 — <c>Validate()</c>, rendered.</summary>
        public CheckSection? Check { get; }

        /// <summary>Claim 2 — the uid-keyed diff, present when a baseline was supplied.</summary>
        public CompareSection? Compare { get; }

        /// <summary>Claim 3 — the loss report, present when a conversion produced one.</summary>
        public TransferSection? Transfer { get; }

        /// <summary>
        /// What the model is called in the header line: the first source's name, or
        /// the title where there is no file at all.
        /// </summary>
        public string SubjectName => Provenance.Subject?.Name ?? Title;

        /// <summary>
        /// Whether there is anything to act on: no findings, no differences, and no
        /// transfer that failed to complete.
        ///
        /// <b>Declared losses do not count.</b> An adapter reporting fourteen losses
        /// is the adapter working as designed — decision 10 — and a tool whose exit
        /// code punished it would be a tool that taught every adapter author to
        /// report fewer of them.
        /// </summary>
        public bool IsClean
        {
            get
            {
                if (Check is not null && !Check.IsClean)
                    return false;
                if (Compare is not null && !Compare.IsIdentical)
                    return false;
                if (Transfer is not null && !Transfer.Succeeded)
                    return false;

                return true;
            }
        }

        /// <summary>
        /// The opening block C1 sketches, as data — so the HTML table, the JSON and
        /// the line the CLI prints to a terminal are three renderings of one answer
        /// rather than three answers.
        /// </summary>
        public IReadOnlyList<ReportSummaryRow> Summary()
        {
            var rows = new List<ReportSummaryRow>();

            if (Check is not null)
                rows.Add(new ReportSummaryRow("Check", "Validate()", Check.Summary()));

            if (Compare is not null)
                rows.Add(new ReportSummaryRow("Compare", "vs " + Compare.Baseline.Name, Compare.Summary()));

            if (Transfer is not null)
                rows.Add(new ReportSummaryRow("Transfer", Transfer.Route, Transfer.Summary()));

            return rows;
        }

        /// <summary>
        /// The header line, in one string:
        /// <c>Model Assurance Report · steel-hall.femex · 2026-08-21 · femex 0.1.0 · sha256 3f9a…</c>
        /// </summary>
        public string Headline()
        {
            var parts = new List<string>
            {
                Title,
                SubjectName,
                Provenance.Date,
                Provenance.ToolName + " " + Provenance.ToolVersion,
            };

            string? hash = Provenance.Subject?.ShortHash;
            if (hash is not null)
                parts.Add("sha256 " + hash);

            return string.Join(" · ", parts);
        }
    }
}
