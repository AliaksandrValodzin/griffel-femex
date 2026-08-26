using griffel_femex.Geometry;
using griffel_femex.Interop;

namespace griffel_femex.Synthesis
{
    /// <summary>
    /// Turns a pile of absolute points and elevations into a
    /// <see cref="FemexModel"/>'s <see cref="FemexModel.Levels"/> and
    /// <see cref="FemexModel.Nodes"/> — once, against the finished extent.
    ///
    /// <b>Why this exists at all.</b> <c>FEMEX_Adapters.md</c> §6.1 settles the
    /// policy — snap an incoming elevation to an existing level within tolerance,
    /// otherwise create one, and always report an invented level — and then says
    /// outright that it names no helper, because none existed. The repository had
    /// <see cref="FemexModel.GetCoincidenceTolerance"/>, a three-dimensional
    /// <i>node</i> tolerance, and nothing at all for matching a
    /// <see cref="Level.AbsoluteElevation"/>.
    ///
    /// <b>Why it is two-phase, which is the half that is easy to skip.</b>
    /// <see cref="FemexModel.GetCoincidenceTolerance"/> is 1e-6 of the model's
    /// <i>current</i> bounding diagonal, so an import that starts from an empty
    /// model begins at the floor and grows as the model fills: the first nodes are
    /// matched against a far tighter test than the last, and the same native model
    /// read in a different order yields a different node table. That is fatal to
    /// §7.2's round-trip equivalence, and therefore to every conformance test built
    /// on it. So: collect every candidate first, cluster once against the finished
    /// extent, then create. Never stream one element at a time.
    ///
    /// Order-independence is not merely hoped for. Candidates are clustered in a
    /// canonical order of their own — sorted by coordinate, not by arrival — so two
    /// traversals of the same native model that hand over the same points in
    /// different orders produce byte-identical node and level tables, node numbers
    /// included.
    ///
    /// <b>The tolerance is a shape, not a number.</b> §6.1 leaves the number
    /// deliberately unfixed and states the shape: relative to the model's own
    /// extent, floored, in the manner of <c>1e-6 × diagonal</c> over a <c>1e-9</c>
    /// floor — never an absolute millimetre, which means something different in a
    /// metre model and a millimetre one. A caller who has measured a real program
    /// overrides it through <see cref="SynthesisOptions"/> and owns the consequence.
    ///
    /// Typical use, from an importer:
    /// <code>
    /// var synthesis = new GeometrySynthesis();
    /// int start = synthesis.AddPoint(x1, y1, z1);
    /// int end   = synthesis.AddPoint(x2, y2, z2);
    /// // ... every point in the native model, then:
    /// SynthesisResult result = synthesis.Build(model);
    /// bar.StartNodeId = result.NodeFor(start).NodeNumber;
    /// messages.AddRange(result.Messages);
    /// </code>
    /// </summary>
    public sealed class GeometrySynthesis
    {
        /// <summary>1e-6 of the finished extent, matching <see cref="FemexModel.GetCoincidenceTolerance"/>.</summary>
        private const double RelativeGeometricTolerance = 1e-6;

        /// <summary>The floor under both scaled tolerances, for degenerate models.</summary>
        private const double MinimumGeometricTolerance = 1e-9;

        private readonly SynthesisOptions _options;
        private readonly List<double> _declaredElevations = new List<double>();
        private readonly List<string?> _declaredNames = new List<string?>();
        private readonly List<double[]> _points = new List<double[]>();
        private bool _built;

        public GeometrySynthesis(SynthesisOptions? options = null)
        {
            _options = options ?? new SynthesisOptions();
        }

        /// <summary>
        /// An elevation the native model actually states — a storey, a floor, a
        /// reference plane. Returns a ticket to read the resulting
        /// <see cref="Level"/> back with.
        ///
        /// A level declared here is <b>never</b> reported as invented, whether or
        /// not anything ends up sitting on it: the native model said it, so FEMEX
        /// did not make it up.
        /// </summary>
        public int AddLevel(double absoluteElevation, string? name = null)
        {
            RequireNotBuilt();

            _declaredElevations.Add(absoluteElevation);
            _declaredNames.Add(name);
            return _declaredElevations.Count - 1;
        }

        /// <summary>
        /// A point some element needs a node at, in absolute coordinates with Z up —
        /// §6.3's normalisation has already happened by the time anything reaches
        /// here. Returns a ticket to read the resulting <see cref="Node"/> back with.
        ///
        /// The same point handed over twice returns two tickets and yields one node,
        /// which is the whole purpose: FEMEX's unit of connectivity is the shared
        /// node, and an importer that transcribes a native node list either loses
        /// connectivity silently or invents it silently.
        /// </summary>
        public int AddPoint(double x, double y, double z)
        {
            RequireNotBuilt();

            _points.Add(new[] { x, y, z });
            return _points.Count - 1;
        }

        /// <summary>
        /// Phase two: cluster everything collected against the finished extent, add
        /// the levels and nodes that are missing to <paramref name="model"/>, and
        /// return the map from tickets to what was created.
        ///
        /// Levels and nodes already on the model are seeds, never moved and never
        /// renumbered — an import into a model that already has geometry snaps to
        /// what is there, which is the same rule §6.1 states for levels applied to
        /// both.
        ///
        /// Callable once. A second phase-one after a phase-two would be exactly the
        /// growing-tolerance bug this class exists to prevent, so it throws rather
        /// than quietly doing it.
        /// </summary>
        public SynthesisResult Build(FemexModel model)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            RequireNotBuilt();
            _built = true;

            double levelTolerance = _options.LevelTolerance ?? DeriveLevelTolerance(model);
            double nodeTolerance = _options.NodeTolerance ?? DeriveNodeTolerance(model);

            List<Level> levels = BuildLevels(model, levelTolerance, out List<Level> invented,
                                             out Level[] declaredLevels);
            Node[] nodes = BuildNodes(model, levels, levelTolerance, nodeTolerance);

            return new SynthesisResult(declaredLevels, nodes, invented, levelTolerance, nodeTolerance,
                                       BuildMessages(invented));
        }

        // ----- Tolerances, derived once from everything collected -----

        /// <summary>
        /// The vertical extent of every elevation in play — the model's own levels,
        /// the declared ones, and the Z of every point — because a tolerance derived
        /// from a subset is a tolerance that changes as the subset grows, which is
        /// the bug.
        /// </summary>
        private double DeriveLevelTolerance(FemexModel model)
        {
            double min = double.MaxValue, max = double.MinValue;
            bool any = false;

            foreach (Level level in model.Levels)
            {
                min = Math.Min(min, level.AbsoluteElevation);
                max = Math.Max(max, level.AbsoluteElevation);
                any = true;
            }

            foreach (double elevation in _declaredElevations)
            {
                min = Math.Min(min, elevation);
                max = Math.Max(max, elevation);
                any = true;
            }

            foreach (double[] point in _points)
            {
                min = Math.Min(min, point[2]);
                max = Math.Max(max, point[2]);
                any = true;
            }

            if (!any)
                return MinimumGeometricTolerance;

            return Math.Max(RelativeGeometricTolerance * (max - min), MinimumGeometricTolerance);
        }

        /// <summary>
        /// 1e-6 of the bounding diagonal of every point in play, which is
        /// <see cref="FemexModel.GetCoincidenceTolerance"/>'s rule computed once
        /// against the finished model instead of repeatedly against a growing one.
        /// </summary>
        private double DeriveNodeTolerance(FemexModel model)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;
            bool any = false;

            foreach (double[] point in _points)
            {
                minX = Math.Min(minX, point[0]); maxX = Math.Max(maxX, point[0]);
                minY = Math.Min(minY, point[1]); maxY = Math.Max(maxY, point[1]);
                minZ = Math.Min(minZ, point[2]); maxZ = Math.Max(maxZ, point[2]);
                any = true;
            }

            foreach (Node node in model.Nodes)
            {
                if (!TryGetExistingPoint(model, node, out double x, out double y, out double z))
                    continue;

                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
                any = true;
            }

            if (!any)
                return MinimumGeometricTolerance;

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            double diagonal = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            return Math.Max(RelativeGeometricTolerance * diagonal, MinimumGeometricTolerance);
        }

        // ----- Levels -----

        /// <summary>
        /// Every elevation in play resolved to a level: snapped to one that exists,
        /// or to one this call creates. Ordered by elevation, so the numbering of
        /// invented levels does not depend on the order anything arrived in.
        /// </summary>
        private List<Level> BuildLevels(FemexModel model, double tolerance,
                                        out List<Level> invented, out Level[] declaredLevels)
        {
            invented = new List<Level>();

            // Seed with what the model already has, then with what the caller
            // declared. Both are known levels; the difference is only that a
            // declared one may still have to be created.
            var known = new List<Level>(model.Levels);
            var declared = new Level[_declaredElevations.Count];

            for (int i = 0; i < _declaredElevations.Count; i++)
            {
                double elevation = _declaredElevations[i];
                Level? match = FindLevel(known, elevation, tolerance);
                if (match is null)
                {
                    match = CreateLevel(model, elevation, _declaredNames[i]);
                    known.Add(match);
                }

                declared[i] = match;
            }

            // Every elevation a point sits at that no known level covers becomes an
            // invented level. Clustered in ascending order so that two points either
            // side of one gap always resolve the same way.
            var orphans = new List<double>();
            foreach (double[] point in _points)
            {
                if (FindLevel(known, point[2], tolerance) is null)
                    orphans.Add(point[2]);
            }

            orphans.Sort();

            foreach (double elevation in orphans)
            {
                if (FindLevel(known, elevation, tolerance) is not null)
                    continue;

                Level created = CreateLevel(model, elevation, NameFor(elevation));
                known.Add(created);
                invented.Add(created);
            }

            declaredLevels = declared;
            return known;
        }

        private Level CreateLevel(FemexModel model, double elevation, string? name)
        {
            int number = NextLevelNumber(model);
            var level = new Level(number, name, elevation, elevation);
            model.Levels.Add(level);
            return level;
        }

        private string? NameFor(double elevation)
        {
            string? prefix = _options.InventedLevelNamePrefix;
            if (string.IsNullOrEmpty(prefix))
                return null;

            return $"{prefix} {elevation:+0.000;-0.000;0.000}";
        }

        private static int NextLevelNumber(FemexModel model)
        {
            int highest = -1;
            bool any = false;

            foreach (Level level in model.Levels)
            {
                if (!any || level.LevelNumber > highest)
                {
                    highest = level.LevelNumber;
                    any = true;
                }
            }

            return any ? highest + 1 : 0;
        }

        /// <summary>
        /// The nearest level within tolerance, or null. Nearest rather than first:
        /// with two levels a hair over a tolerance apart, "first" would depend on
        /// list order and "nearest" does not.
        /// </summary>
        private static Level? FindLevel(List<Level> levels, double elevation, double tolerance)
        {
            Level? best = null;
            double bestDistance = double.MaxValue;

            foreach (Level level in levels)
            {
                double distance = Math.Abs(level.AbsoluteElevation - elevation);
                if (distance > tolerance)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = level;
                }
            }

            return best;
        }

        // ----- Nodes -----

        /// <summary>
        /// Every collected point resolved to a node, matched against the nodes the
        /// model already had and against each other.
        ///
        /// The canonical order is the point of the method: points are clustered
        /// sorted by coordinate rather than by arrival, so the node table and its
        /// numbering are a function of the geometry alone. Two importers walking the
        /// same native model in different orders produce the same file.
        /// </summary>
        private Node[] BuildNodes(FemexModel model, List<Level> levels, double levelTolerance,
                                  double nodeTolerance)
        {
            var order = new int[_points.Count];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;

            List<double[]> points = _points;
            Array.Sort(order, (a, b) =>
            {
                int compared = points[a][0].CompareTo(points[b][0]);
                if (compared != 0) return compared;
                compared = points[a][1].CompareTo(points[b][1]);
                if (compared != 0) return compared;
                return points[a][2].CompareTo(points[b][2]);
            });

            // The nodes already on the model are seeds: an import into a model with
            // geometry joins onto it rather than laying a second table beside it.
            var placed = new List<(double X, double Y, double Z, Node Node)>();
            foreach (Node existing in model.Nodes)
            {
                if (TryGetExistingPoint(model, existing, out double x, out double y, out double z))
                    placed.Add((x, y, z, existing));
            }

            var resolved = new Node[_points.Count];
            double toleranceSquared = nodeTolerance * nodeTolerance;

            foreach (int index in order)
            {
                double[] point = _points[index];
                Node? match = FindNode(placed, point, toleranceSquared);

                if (match is null)
                {
                    Level level = FindLevel(levels, point[2], levelTolerance)
                                  ?? throw new InvalidOperationException(
                                      $"No level was synthesised for elevation {point[2]}.");

                    match = new Node(model.NextNodeNumber(), point[0], point[1], level.LevelNumber,
                                     point[2] - level.AbsoluteElevation);
                    model.Nodes.Add(match);
                    placed.Add((point[0], point[1], point[2], match));
                }

                resolved[index] = match;
            }

            return resolved;
        }

        private static Node? FindNode(List<(double X, double Y, double Z, Node Node)> placed,
                                      double[] point, double toleranceSquared)
        {
            Node? best = null;
            double bestDistance = double.MaxValue;

            foreach (var candidate in placed)
            {
                double ex = candidate.X - point[0];
                double ey = candidate.Y - point[1];
                double ez = candidate.Z - point[2];
                double distance = ex * ex + ey * ey + ez * ez;

                if (distance > toleranceSquared)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate.Node;
                }
            }

            return best;
        }

        private static bool TryGetExistingPoint(FemexModel model, Node node,
                                                out double x, out double y, out double z)
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

        // ----- Reporting -----

        /// <summary>
        /// §6.1's other half: <i>always</i> emit an <see cref="LossCategory.Invented"/>
        /// message for a level the native model did not have. One per level rather
        /// than one per node sitting on it — the invention is the level.
        /// </summary>
        private static IReadOnlyList<TransferMessage> BuildMessages(List<Level> invented)
        {
            if (invented.Count == 0)
                return new TransferMessage[0];

            var messages = new List<TransferMessage>(invented.Count);
            foreach (Level level in invented)
            {
                messages.Add(TransferMessage.Loss(
                    LossCategory.Invented,
                    new ObjectRef(FemexEntity.Level, level.LevelNumber, level.Uid),
                    $"Level {level.LevelNumber} at elevation {level.AbsoluteElevation} was created to " +
                    "hold geometry the native model gave no storey; it is not a storey the native model " +
                    "states."));
            }

            return messages;
        }

        private void RequireNotBuilt()
        {
            if (_built)
            {
                throw new InvalidOperationException(
                    "This synthesis has already been built. Collect every candidate before building, " +
                    "and build once: a second pass would cluster against a different extent, which is " +
                    "the order-dependence two-phase synthesis exists to remove.");
            }
        }
    }
}
