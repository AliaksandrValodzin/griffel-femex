using griffel_femex.Geometry;

namespace griffel_femex.Comparison
{
    /// <summary>What an integer reference in the file points at.</summary>
    internal enum RefTarget
    {
        Grid,
        Level,
        Node,
        Section,
        SurfaceProperty,
        Material,
        Bar,
        Plate,

        /// <summary>A bar or a plate — the id space <c>Element</c> shares.</summary>
        Element,

        LoadCase,

        /// <summary>Scoped by the plate that owns it, so it needs a sibling id too.</summary>
        Region,
    }

    /// <summary>
    /// One model's integer reference keys resolved to the uids they stand for.
    ///
    /// This is what turns §7.2 from an aspiration into a comparison. The definition
    /// says objects match by uid and never by id, and the reason is that the native
    /// program renumbers on the way out and again on the way back. That renumbering
    /// does not stop at the objects: <c>Bar.StartNodeId</c> is an integer that names
    /// a node, and comparing two of them across a crossing compares two node
    /// numbering schemes rather than two structures. Resolving each reference to the
    /// uid of what it points at is the only way a bar can be said to still join the
    /// same two nodes.
    ///
    /// Built once per model per comparison. The library's own helpers resolve with a
    /// linear <c>List.Find</c>, which is right for them and quadratic here.
    /// </summary>
    internal sealed class EntityIndex
    {
        private readonly Dictionary<RefTarget, Dictionary<int, IIdentified>> _targets =
            new Dictionary<RefTarget, Dictionary<int, IIdentified>>();

        private readonly Dictionary<int, Dictionary<int, PlateRegion>> _regions =
            new Dictionary<int, Dictionary<int, PlateRegion>>();

        internal EntityIndex(FemexModel model)
        {
            Add(RefTarget.Grid, model.Grids, g => g.Id);
            Add(RefTarget.Level, model.Levels, l => l.LevelNumber);
            Add(RefTarget.Node, model.Nodes, n => n.NodeNumber);
            Add(RefTarget.Section, model.Sections, s => s.Id);
            Add(RefTarget.SurfaceProperty, model.SurfaceProperties, s => s.Id);
            Add(RefTarget.Material, model.Materials, m => m.Id);
            Add(RefTarget.Bar, model.Bars, b => b.Id);
            Add(RefTarget.Plate, model.Plates, p => p.Id);
            Add(RefTarget.LoadCase, model.LoadCases, c => c.Number);

            Dictionary<int, IIdentified> elements = Bucket(RefTarget.Element);
            foreach (Bar bar in model.Bars)
                elements[bar.Id] = bar;
            foreach (Plate plate in model.Plates)
                elements[plate.Id] = plate;

            foreach (Plate plate in model.Plates)
            {
                var byId = new Dictionary<int, PlateRegion>();
                foreach (PlateRegion region in plate.Regions)
                    byId[region.Id] = region;

                _regions[plate.Id] = byId;
            }
        }

        /// <summary>
        /// What the reference stands for, as a token two models can be compared on.
        ///
        /// Four answers, and each of them means something different:
        /// <list type="bullet">
        /// <item>a uid — the reference resolved, and this is what it points at;</item>
        /// <item><c>?</c> — it resolved to an object carrying no uid, so the
        /// reference has no comparable identity. That is not the same as equal, and
        /// it is why <see cref="DifferenceKind.Unkeyed"/> is reported separately: a
        /// model with no uids compares clean here and is loudly uncomparable
        /// there;</item>
        /// <item><c>missing:N</c> — it resolved to nothing. A dangling reference is
        /// a real and comparable fact about the model;</item>
        /// <item><c>none</c> — the reference was null, which for an optional one is
        /// a statement.</item>
        /// </list>
        /// </summary>
        internal string Token(RefTarget target, int? id, int? scopeId)
        {
            if (!id.HasValue)
                return "none";

            IIdentified? resolved = target == RefTarget.Region
                ? ResolveRegion(scopeId, id.Value)
                : Resolve(target, id.Value);

            if (resolved is null)
                return $"missing:{id.Value}";

            return resolved.Uid.HasValue ? resolved.Uid.Value.ToString() : "?";
        }

        private IIdentified? Resolve(RefTarget target, int id)
        {
            return _targets.TryGetValue(target, out Dictionary<int, IIdentified>? bucket)
                   && bucket.TryGetValue(id, out IIdentified? entity)
                ? entity
                : null;
        }

        private PlateRegion? ResolveRegion(int? plateId, int regionId)
        {
            if (!plateId.HasValue)
                return null;

            return _regions.TryGetValue(plateId.Value, out Dictionary<int, PlateRegion>? byId)
                   && byId.TryGetValue(regionId, out PlateRegion? region)
                ? region
                : null;
        }

        private void Add<T>(RefTarget target, List<T> entities, Func<T, int> key) where T : IIdentified
        {
            Dictionary<int, IIdentified> bucket = Bucket(target);
            foreach (T entity in entities)
                bucket[key(entity)] = entity;
        }

        private Dictionary<int, IIdentified> Bucket(RefTarget target)
        {
            if (!_targets.TryGetValue(target, out Dictionary<int, IIdentified>? bucket))
            {
                bucket = new Dictionary<int, IIdentified>();
                _targets[target] = bucket;
            }

            return bucket;
        }
    }
}
