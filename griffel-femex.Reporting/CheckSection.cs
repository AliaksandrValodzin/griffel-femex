using System.Collections.Generic;
using System.Linq;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// The first of the report's three sections, and the one that maps to Claim 1:
    /// what <see cref="FemexModel.Validate()"/> says about this model.
    ///
    /// <b>It carries <see cref="ValidationMessage"/> itself rather than a copy of
    /// it.</b> A reporting-side finding type with the same two fields would be a
    /// second statement of what a finding is, free to fall behind the engine that
    /// produces them — and decision 8 is that the engine is authoritative. The
    /// grouping below is presentation; the findings are the engine's.
    ///
    /// <b>The category split is the point.</b> §4 of
    /// <c>FEMEX_BusinessModel.md</c> is right that the referential half is table
    /// stakes and the judgement half is the product, so a section that listed all
    /// fourteen findings in engine order would bury the two an engineer is paying to
    /// be told about under twelve an engineer's own program would have caught.
    /// </summary>
    public sealed class CheckSection
    {
        public CheckSection(IEnumerable<ValidationMessage> findings)
        {
            if (findings is null)
                throw new ArgumentNullException(nameof(findings));

            Findings = new List<ValidationMessage>(findings);
        }

        /// <summary>
        /// Everything <see cref="FemexModel.Validate()"/> returned, in the order it
        /// returned it — which is fixed, so two runs over the same model produce the
        /// same document.
        /// </summary>
        public IReadOnlyList<ValidationMessage> Findings { get; }

        public int Count => Findings.Count;

        public int ErrorCount => Findings.Count(f => f.Severity == ValidationSeverity.Error);

        public int WarningCount => Findings.Count(f => f.Severity == ValidationSeverity.Warning);

        /// <summary>
        /// Whether the model is one a consumer can be expected to make sense of —
        /// which is what an Error means, and what an exporter's "no second gate"
        /// rule tests.
        /// </summary>
        public bool IsClean => Findings.Count == 0;

        public IReadOnlyList<ValidationMessage> OfCategory(ValidationCategory category)
        {
            return Findings.Where(f => f.Category == category).ToList();
        }

        public int CountOf(ValidationCategory category)
        {
            return Findings.Count(f => f.Category == category);
        }

        /// <summary>
        /// The categories present, in the order the report shows them: judgement
        /// first, because it is the half that is worth reading, then referential,
        /// then what the file says about itself. Empty categories are absent rather
        /// than shown as zero — a heading with nothing under it is noise in a
        /// document somebody has to read a hundred of.
        /// </summary>
        public IReadOnlyList<ValidationCategory> Categories
        {
            get
            {
                var order = new[]
                {
                    ValidationCategory.Judgement,
                    ValidationCategory.Referential,
                    ValidationCategory.Provenance,
                };

                return order.Where(c => CountOf(c) > 0).ToList();
            }
        }

        /// <summary>
        /// The one-line summary C1 sketches: <c>14 findings   2 error · 12 warning</c>.
        /// </summary>
        public string Summary()
        {
            if (IsClean)
                return "no findings";

            string findings = Count == 1 ? "1 finding" : Count + " findings";
            return $"{findings} — {ErrorCount} error · {WarningCount} warning";
        }

        /// <summary>
        /// A section holding one Error that says the file could not be read.
        ///
        /// This is P4 of the plan arriving where it bites. A <c>.femex</c> from a
        /// later schema carrying one unrecognised enum value throws on read, and C4
        /// requires that such a file be <b>a 1 with a finding, not a 2 with a stack
        /// trace</b>. The finding is <see cref="ValidationCategory.Provenance"/>
        /// because that is exactly what it is: a statement about the file rather
        /// than about a structure, since there is no structure to make one about.
        /// </summary>
        public static CheckSection Unreadable(string name, string reason)
        {
            return new CheckSection(new[]
            {
                ValidationMessage.Error(
                    $"{name} could not be read as FEMEX {ReportTool.SchemaVersion}: {reason}",
                    ValidationCategory.Provenance),
            });
        }
    }
}
