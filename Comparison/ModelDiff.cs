using griffel_femex.Geometry;
using griffel_femex.Interop;

namespace griffel_femex.Comparison
{
    /// <summary>
    /// Two FEMEX models, and every way they are not the same model — under the
    /// equivalence <c>FEMEX_Adapters.md</c> §7.2 fixes:
    ///
    /// <blockquote>Objects are matched by <b>Uid</b>, never by <c>Id</c>. Lists
    /// compare as <b>sets</b> under that key, not as sequences. Geometry compares
    /// within <b>GetCoincidenceTolerance</b>. Anything left over — an object on one
    /// side and not the other, or a matched pair differing in any field — is a
    /// difference.</blockquote>
    ///
    /// <b>This is a product surface, not test infrastructure</b>, which is why it
    /// lives here and not under <c>Interop/Conformance/</c>. Two consumers want it:
    /// the round-trip assertion of §7.1, which is the only reason to believe an
    /// adapter reports its losses; and the model comparison
    /// <c>FEMEX_BusinessModel.md</c> §3 calls Claim 2. Promoting it later should not
    /// be a move.
    ///
    /// <b>It stays uid-keyed, deliberately.</b> Matching two models from different
    /// programs, where uids never survived, wants geometric and topological
    /// heuristics — the largest piece of new engineering in the business model's own
    /// plan, and one that should get its own document before any of it is written.
    /// Conformance needs no such fallback, because §7.2 says outright that a model
    /// with partial uid coverage cannot be round-trip-tested at all. So the fallback
    /// is absent rather than half-built, and partial coverage is
    /// <see cref="DifferenceKind.Unkeyed"/> — loud, not silent.
    ///
    /// <b>Where FEMEX's one lead over SAF becomes visible.</b>
    /// <c>StructuralSurfaceMemberRegion</c> carries no precedence field of any kind,
    /// so overlapping SAF regions are undefined behaviour, while FEMEX's rule —
    /// highest <see cref="PlateRegion.Priority"/> wins, ties broken by kind and then
    /// by list order — is total and deterministic. A diff is where a reader sees
    /// that: two models that agree everywhere except a priority are two different
    /// structures, and this says so.
    /// </summary>
    public static class ModelDiff
    {
        /// <summary>
        /// Every difference between two models, ordered by entity and then by member
        /// so that two runs of the same comparison produce the same list.
        /// </summary>
        public static IReadOnlyList<ModelDifference> Compare(FemexModel left, FemexModel right,
                                                             ModelDiffOptions? options = null)
        {
            if (left is null)
                throw new ArgumentNullException(nameof(left));
            if (right is null)
                throw new ArgumentNullException(nameof(right));

            options ??= new ModelDiffOptions();

            double tolerance = options.GeometricTolerance
                               ?? Math.Max(left.GetCoincidenceTolerance(), right.GetCoincidenceTolerance());

            var differences = new List<ModelDifference>();
            var leftIndex = new EntityIndex(left);
            var rightIndex = new EntityIndex(right);
            var comparer = new MemberComparer(options, leftIndex, rightIndex, tolerance, differences);

            CompareModelFacts(left, right, options, comparer, differences);
            CompareEntities(left, right, comparer, differences);

            return differences;
        }

        /// <summary>
        /// Whether the two models are equivalent — the same question, asked when only
        /// the answer is wanted.
        /// </summary>
        public static bool AreEquivalent(FemexModel left, FemexModel right,
                                         ModelDiffOptions? options = null)
        {
            return Compare(left, right, options).Count == 0;
        }

        // ----- The model itself -----

        /// <summary>
        /// The facts that belong to no entity: the schema version, the unit
        /// convention, gravity, and the default grids. §3.3 keeps units and gravity
        /// out of <see cref="FemexEntity"/> on purpose, so a difference in either is
        /// anchored to nothing — the same null subject
        /// <see cref="TransferMessage.ModelLoss"/> uses, so the two can be matched.
        /// </summary>
        private static void CompareModelFacts(FemexModel left, FemexModel right, ModelDiffOptions options,
                                              MemberComparer comparer, List<ModelDifference> differences)
        {
            if (!string.Equals(left.SchemaVersion, right.SchemaVersion, StringComparison.Ordinal))
            {
                comparer.Report(DifferenceKind.MemberDiffers, null, nameof(FemexModel.SchemaVersion),
                                left.SchemaVersion, right.SchemaVersion,
                                $"The model is FEMEX {left.SchemaVersion ?? "unversioned"} on the left and " +
                                $"{right.SchemaVersion ?? "unversioned"} on the right.");
            }

            comparer.CompareMembers(null, left.Gravity, right.Gravity, nameof(FemexModel.Gravity));

            CompareOptionalBlock(comparer, nameof(FemexModel.Units), left.Units, right.Units);

            if (options.CompareMetadata)
                CompareOptionalBlock(comparer, nameof(FemexModel.Metadata), left.Metadata, right.Metadata);

            if (options.CompareMesh)
                CompareOptionalBlock(comparer, nameof(FemexModel.Mesh), left.Mesh, right.Mesh);

            CompareDefaultGrids(left, right, comparer);

            if (options.CompareUnknownMembers)
                comparer.CompareMembers(null, new RootMembers(left), new RootMembers(right), null);
        }

        /// <summary>
        /// A stand-in for the model's own extension data, so that a root-level member
        /// this build has no property for is compared by the same code as one on a
        /// bar. Losing it across a crossing is the <see cref="LossCategory.Stale"/>
        /// loss <c>IExtensible</c> exists to make visible, and a diff that walked
        /// only the declared properties would be blind to exactly the thing that
        /// preserve-and-warn was built for.
        /// </summary>
        private sealed class RootMembers
        {
            internal RootMembers(FemexModel model)
            {
                UnknownMembers = model.UnknownMembers;
            }

            public Dictionary<string, System.Text.Json.JsonElement>? UnknownMembers { get; }
        }

        private static void CompareOptionalBlock(MemberComparer comparer, string name,
                                                 object? left, object? right)
        {
            if (left is null && right is null)
                return;

            if (left is null || right is null)
            {
                comparer.Report(DifferenceKind.MemberDiffers, null, name,
                                left is null ? null : "present", right is null ? null : "present",
                                $"The model states {name} on the {(left is null ? "right" : "left")} only.");
                return;
            }

            comparer.CompareMembers(null, left, right, name);
        }

        /// <summary>
        /// The grids every level inherits, compared as a set of what they point at
        /// rather than as a list of ids — the same treatment
        /// <see cref="Level.GridIds"/> gets, because it is the same reference.
        /// </summary>
        private static void CompareDefaultGrids(FemexModel left, FemexModel right, MemberComparer comparer)
        {
            var wrapper = new DefaultGrids(left);
            var other = new DefaultGrids(right);

            // Anchored to the grid kind rather than to the model: it is a statement
            // about grids, and a message declaring that grids do not cross should
            // cover it.
            comparer.CompareMembers(new ObjectRef(FemexEntity.Grid), wrapper, other, null);
        }

        /// <summary>
        /// A stand-in whose one member carries the reference-table entry
        /// <c>FemexModel.DefaultGridIds</c>, so the model's own grid list is resolved
        /// through exactly the same code path as every other reference rather than
        /// through a second copy of it.
        /// </summary>
        private sealed class DefaultGrids
        {
            internal DefaultGrids(FemexModel model)
            {
                DefaultGridIds = model.DefaultGridIds;
            }

            public List<int> DefaultGridIds { get; }
        }

        // ----- The entities -----

        /// <summary>
        /// Matches every uid-carrying object on both sides and compares the pairs.
        ///
        /// Matching is model-wide rather than per list, which costs nothing and is
        /// what a uid means: uniqueness is model-wide — that is what a GUID is — and
        /// a receiver merging by uid does not care which list an object came from.
        /// A pair whose runtime types differ is reported as such rather than walked,
        /// because there are no shared members to walk.
        /// </summary>
        private static void CompareEntities(FemexModel left, FemexModel right, MemberComparer comparer,
                                            List<ModelDifference> differences)
        {
            Dictionary<Guid, Entry> leftKeyed = Index(left, out Dictionary<FemexEntity, int> leftUnkeyed);
            Dictionary<Guid, Entry> rightKeyed = Index(right, out Dictionary<FemexEntity, int> rightUnkeyed);

            ReportUnkeyed(comparer, leftUnkeyed, "left");
            ReportUnkeyed(comparer, rightUnkeyed, "right");

            var uids = new List<Guid>(leftKeyed.Keys);
            foreach (Guid uid in rightKeyed.Keys)
            {
                if (!leftKeyed.ContainsKey(uid))
                    uids.Add(uid);
            }

            uids.Sort();

            foreach (Guid uid in uids)
            {
                bool inLeft = leftKeyed.TryGetValue(uid, out Entry leftEntry);
                bool inRight = rightKeyed.TryGetValue(uid, out Entry rightEntry);

                if (inLeft && !inRight)
                {
                    comparer.Report(DifferenceKind.OnlyInLeft, leftEntry.Ref, null, leftEntry.Owner, null,
                                    $"{leftEntry.Owner} is on the left and nothing on the right carries " +
                                    $"uid {uid}.");
                    continue;
                }

                if (!inLeft && inRight)
                {
                    comparer.Report(DifferenceKind.OnlyInRight, rightEntry.Ref, null, null, rightEntry.Owner,
                                    $"{rightEntry.Owner} is on the right and nothing on the left carries " +
                                    $"uid {uid}.");
                    continue;
                }

                Type leftType = leftEntry.Entity.GetType();
                Type rightType = rightEntry.Entity.GetType();

                if (leftType != rightType)
                {
                    comparer.Report(DifferenceKind.TypeDiffers, leftEntry.Ref, null,
                                    leftType.Name, rightType.Name,
                                    $"{leftEntry.Owner} is a {leftType.Name} on the left and a " +
                                    $"{rightType.Name} on the right.");
                    continue;
                }

                if (leftEntry.Entity is Node leftNode && rightEntry.Entity is Node rightNode)
                    ComparePosition(comparer, leftEntry, left, leftNode, right, rightNode);

                comparer.CompareMembers(leftEntry.Ref, leftEntry.Entity, rightEntry.Entity, null);
            }
        }

        /// <summary>
        /// A node's geometry is where it is, not the three numbers that say so. Two
        /// nodes at the same absolute point are in the same place whether one of them
        /// reaches it through a level and an offset and the other does not, and that
        /// re-expression is exactly what an importer that synthesises its own levels
        /// does. The level itself is still compared, as the reference it also is.
        /// </summary>
        private static void ComparePosition(MemberComparer comparer, Entry entry,
                                            FemexModel leftModel, Node left,
                                            FemexModel rightModel, Node right)
        {
            if (!TryGetPoint(leftModel, left, out double lx, out double ly, out double lz) ||
                !TryGetPoint(rightModel, right, out double rx, out double ry, out double rz))
            {
                // A node on an unknown level has no position to compare;
                // Validate() reports that separately and it is not a difference.
                return;
            }

            if (comparer.NumbersEqual(lx, rx, geometric: true) &&
                comparer.NumbersEqual(ly, ry, geometric: true) &&
                comparer.NumbersEqual(lz, rz, geometric: true))
            {
                return;
            }

            string leftText = $"({lx}, {ly}, {lz})";
            string rightText = $"({rx}, {ry}, {rz})";
            comparer.Report(DifferenceKind.MemberDiffers, entry.Ref, "Position", leftText, rightText,
                            $"{entry.Owner} is at {leftText} on the left and {rightText} on the right.");
        }

        private static bool TryGetPoint(FemexModel model, Node node, out double x, out double y, out double z)
        {
            x = y = z = 0.0;

            Level? level = model.Levels.Find(l => l.LevelNumber == node.LevelNumber);
            if (level is null)
                return false;

            x = node.X;
            y = node.Y;
            z = level.AbsoluteElevation + node.VerticalOffset;
            return true;
        }

        private static Dictionary<Guid, Entry> Index(FemexModel model,
                                                     out Dictionary<FemexEntity, int> unkeyed)
        {
            var keyed = new Dictionary<Guid, Entry>();
            unkeyed = new Dictionary<FemexEntity, int>();

            foreach (var (entity, reference, owner) in model.EnumerateIdentified())
            {
                if (entity.Uid is not Guid uid || uid == Guid.Empty)
                {
                    unkeyed.TryGetValue(reference.Entity, out int count);
                    unkeyed[reference.Entity] = count + 1;
                    continue;
                }

                // A uid naming two objects is an error Validate() reports. Here the
                // first wins and the second becomes an unmatched object, which is
                // the honest reading: one of them has no key of its own.
                if (!keyed.ContainsKey(uid))
                    keyed[uid] = new Entry(entity, reference, owner);
            }

            return keyed;
        }

        private static void ReportUnkeyed(MemberComparer comparer, Dictionary<FemexEntity, int> unkeyed,
                                          string side)
        {
            var kinds = new List<FemexEntity>(unkeyed.Keys);
            kinds.Sort();

            foreach (FemexEntity kind in kinds)
            {
                int count = unkeyed[kind];
                comparer.Report(DifferenceKind.Unkeyed, new ObjectRef(kind), null,
                                side == "left" ? count.ToString() : null,
                                side == "right" ? count.ToString() : null,
                                $"{count} {kind} object(s) on the {side} carry no uid, so nothing on the " +
                                "other side can be matched to them. Uid coverage is a precondition of " +
                                "comparison, not a nicety.");
            }
        }

        private readonly struct Entry
        {
            internal Entry(IIdentified entity, ObjectRef reference, string owner)
            {
                Entity = entity;
                Ref = reference;
                Owner = owner;
            }

            internal IIdentified Entity { get; }

            internal ObjectRef Ref { get; }

            internal string Owner { get; }
        }
    }
}
