using System.Collections.Generic;
using System.Linq;
using griffel_femex.Interop;

namespace griffel_femex.Reporting
{
    /// <summary>
    /// One direction of one crossing: what an adapter was asked to do, whether it
    /// happened, and what it cost.
    /// </summary>
    public sealed class TransferLeg
    {
        public TransferLeg(TransferDirection direction, AdapterInfo adapter,
                           string? sourceName, string? destinationName,
                           bool succeeded, IEnumerable<TransferMessage> messages)
        {
            if (adapter is null)
                throw new ArgumentNullException(nameof(adapter));
            if (messages is null)
                throw new ArgumentNullException(nameof(messages));

            Direction = direction;
            Adapter = adapter;
            SourceName = sourceName;
            DestinationName = destinationName;
            Succeeded = succeeded;
            Messages = new List<TransferMessage>(messages);
        }

        /// <summary>
        /// Import or Export — never <see cref="TransferDirection.Both"/>. Decision 4
        /// is that the two are different transfers with different failure modes, and
        /// a leg that claimed to be both would be a leg whose losses could not be
        /// attributed to either.
        /// </summary>
        public TransferDirection Direction { get; }

        /// <summary>
        /// Who did it, and which schema they were built against — the field that
        /// makes a <see cref="LossCategory.Stale"/> message mean something, and part
        /// of what C3 binds the report to.
        /// </summary>
        public AdapterInfo Adapter { get; }

        public string? SourceName { get; }

        public string? DestinationName { get; }

        /// <summary>
        /// Whether a model came out. <c>TransferResult.Succeeded</c>, carried across
        /// rather than re-derived from the messages: a transfer that reports fifty
        /// losses and produces a model succeeded, and one that reports none and
        /// produces nothing did not.
        /// </summary>
        public bool Succeeded { get; }

        public IReadOnlyList<TransferMessage> Messages { get; }

        /// <summary>
        /// The losses — every message carrying a category, which by
        /// <see cref="TransferMessage"/>'s own invariant is every message that is
        /// not a failure.
        /// </summary>
        public IReadOnlyList<TransferMessage> Losses =>
            Messages.Where(m => m.Category.HasValue).ToList();

        /// <summary>
        /// The failures: Error severity, no category, and a null model beside them.
        /// Listed apart from the losses because they are a different statement — the
        /// transfer did not happen, rather than it happened and cost something.
        /// </summary>
        public IReadOnlyList<TransferMessage> Failures =>
            Messages.Where(m => !m.Category.HasValue).ToList();

        public int LossCount => Messages.Count(m => m.Category.HasValue);

        public int CountOf(LossCategory category) =>
            Messages.Count(m => m.Category == category);

        /// <summary>
        /// The loss categories present, in the enum's own order — which is the order
        /// <c>FEMEX_Adapters.md</c> §4 defines them in, and puts <i>Invented</i>
        /// third rather than last.
        /// </summary>
        public IReadOnlyList<LossCategory> Categories
        {
            get
            {
                return Messages.Where(m => m.Category.HasValue)
                               .Select(m => m.Category!.Value)
                               .Distinct()
                               .OrderBy(c => (int)c)
                               .ToList();
            }
        }

        public IReadOnlyList<TransferMessage> OfCategory(LossCategory category) =>
            Messages.Where(m => m.Category == category).ToList();

        public string Label => Direction == TransferDirection.Import ? "Import" : "Export";

        /// <summary>
        /// What the leg was about, as the report's own subheading:
        /// <c>Import  (steel-hall.xlsx)</c>.
        /// </summary>
        public string Subject => SourceName ?? DestinationName ?? Adapter.TargetProgram;

        public string Summary()
        {
            if (!Succeeded)
                return "did not complete";

            if (LossCount == 0)
                return "no losses";

            return LossCount == 1 ? "1 loss" : LossCount + " losses";
        }
    }

    /// <summary>
    /// The third section, and Claim 3: what a crossing did to the model.
    ///
    /// <b>Two sections, Import and Export</b>, per decision 4 — because they are
    /// different transfers with different failure modes (§1 notes <i>Invented</i> is
    /// overwhelmingly an import one and <i>Dropped</i> an export one), and because a
    /// difference appearing only on export is an exporter bug rather than an
    /// importer one. Fusing them into one list of losses would throw away the one
    /// piece of information that tells a reader which half to go and look at.
    ///
    /// Either leg may be absent: a plain <c>femex convert</c> in one direction has
    /// only the leg it ran.
    /// </summary>
    public sealed class TransferSection
    {
        public TransferSection(string route, TransferLeg? import = null, TransferLeg? export = null)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("A transfer says what crossed what.", nameof(route));
            if (import is null && export is null)
                throw new ArgumentException("A transfer section has at least one leg.", nameof(import));

            Route = route;
            Import = import;
            Export = export;
        }

        /// <summary>
        /// The crossing in one phrase — <c>SAF → FEMEX</c>, or
        /// <c>SAF → FEMEX → SAF</c> for a round trip.
        /// </summary>
        public string Route { get; }

        public TransferLeg? Import { get; }

        public TransferLeg? Export { get; }

        public IReadOnlyList<TransferLeg> Legs
        {
            get
            {
                var legs = new List<TransferLeg>();
                if (Import is not null)
                    legs.Add(Import);
                if (Export is not null)
                    legs.Add(Export);

                return legs;
            }
        }

        public int LossCount => Legs.Sum(leg => leg.LossCount);

        /// <summary>Whether every leg produced what it was asked for.</summary>
        public bool Succeeded => Legs.All(leg => leg.Succeeded);

        public string Summary()
        {
            if (!Succeeded)
                return "did not complete";

            if (LossCount == 0)
                return "no losses";

            return LossCount == 1 ? "1 loss" : LossCount + " losses";
        }
    }
}
