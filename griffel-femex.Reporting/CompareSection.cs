using System.Collections.Generic;
using System.Linq;
using griffel_femex.Comparison;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// The second section, and Claim 2: what changed between this model and another
    /// one.
    ///
    /// Renders <see cref="ModelDiff"/> and adds nothing to it. The uid-keyed
    /// equivalence of §7.2 is the whole of what this report can honestly say today,
    /// and the geometric-and-topological matching that would let it compare two
    /// models from different programs is gated on a document nobody has written.
    /// A compare section that quietly guessed at matches would be the
    /// confidently-incorrect answer the product exists to catch, produced by the
    /// product.
    /// </summary>
    public sealed class CompareSection
    {
        public CompareSection(SourceFile baseline, IEnumerable<ModelDifference> differences)
        {
            if (baseline is null)
                throw new ArgumentNullException(nameof(baseline));
            if (differences is null)
                throw new ArgumentNullException(nameof(differences));

            Baseline = baseline;
            Differences = new List<ModelDifference>(differences);
        }

        /// <summary>What the model was compared against.</summary>
        public SourceFile Baseline { get; }

        /// <summary>
        /// Every difference, in <see cref="ModelDiff"/>'s own order — which is
        /// deterministic, so two runs of the same comparison produce the same
        /// document.
        /// </summary>
        public IReadOnlyList<ModelDifference> Differences { get; }

        public int Count => Differences.Count;

        public bool IsIdentical => Differences.Count == 0;

        /// <summary>
        /// The kinds present, in the enum's own order, so the section groups the
        /// same way twice.
        /// </summary>
        public IReadOnlyList<DifferenceKind> Kinds
        {
            get
            {
                return Differences.Select(d => d.Kind).Distinct().OrderBy(k => (int)k).ToList();
            }
        }

        public IReadOnlyList<ModelDifference> OfKind(DifferenceKind kind)
        {
            return Differences.Where(d => d.Kind == kind).ToList();
        }

        public int CountOf(DifferenceKind kind)
        {
            return Differences.Count(d => d.Kind == kind);
        }

        /// <summary>
        /// <c>Compare     vs steel-hall-2026-08-14.femex       6 differences</c>.
        /// </summary>
        public string Summary()
        {
            if (IsIdentical)
                return "no differences";

            return Count == 1 ? "1 difference" : Count + " differences";
        }
    }
}
