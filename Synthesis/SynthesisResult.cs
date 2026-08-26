using griffel_femex.Geometry;
using griffel_femex.Interop;

namespace griffel_femex.Synthesis
{
    /// <summary>
    /// What <see cref="GeometrySynthesis.Build"/> created, and how to reach it: a
    /// ticket handed out during collection resolves to the level or node that
    /// clustering settled on.
    /// </summary>
    public sealed class SynthesisResult
    {
        private readonly Level[] _levels;
        private readonly Node[] _nodes;

        internal SynthesisResult(Level[] levels, Node[] nodes, IReadOnlyList<Level> inventedLevels,
                                 double levelTolerance, double nodeTolerance,
                                 IReadOnlyList<TransferMessage> messages)
        {
            _levels = levels;
            _nodes = nodes;
            InventedLevels = inventedLevels;
            LevelTolerance = levelTolerance;
            NodeTolerance = nodeTolerance;
            Messages = messages;
        }

        /// <summary>
        /// The levels the native model did not state, in ascending elevation. §6.1
        /// requires one <see cref="LossCategory.Invented"/> message each, and
        /// <see cref="Messages"/> already holds them written.
        /// </summary>
        public IReadOnlyList<Level> InventedLevels { get; }

        /// <summary>
        /// The <see cref="LossCategory.Invented"/> messages for
        /// <see cref="InventedLevels"/>, ready to be added to the transfer report.
        ///
        /// Returned already written rather than left to the adapter, because an
        /// invented level is the case §4.3 describes exactly: from inside the
        /// adapter it does not feel like a loss, it feels like the geometry
        /// working.
        /// </summary>
        public IReadOnlyList<TransferMessage> Messages { get; }

        /// <summary>What elevations were snapped with, after derivation.</summary>
        public double LevelTolerance { get; }

        /// <summary>What points were matched with, after derivation.</summary>
        public double NodeTolerance { get; }

        /// <summary>The level a <see cref="GeometrySynthesis.AddLevel"/> ticket resolved to.</summary>
        public Level LevelFor(int levelTicket)
        {
            if (levelTicket < 0 || levelTicket >= _levels.Length)
                throw new ArgumentOutOfRangeException(nameof(levelTicket));

            return _levels[levelTicket];
        }

        /// <summary>
        /// The node a <see cref="GeometrySynthesis.AddPoint"/> ticket resolved to.
        /// Two tickets for coincident points return the same node, which is the
        /// connectivity FEMEX is made of.
        /// </summary>
        public Node NodeFor(int pointTicket)
        {
            if (pointTicket < 0 || pointTicket >= _nodes.Length)
                throw new ArgumentOutOfRangeException(nameof(pointTicket));

            return _nodes[pointTicket];
        }
    }
}
