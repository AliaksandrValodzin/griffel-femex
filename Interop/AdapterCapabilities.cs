namespace griffel_femex.Interop
{
    /// <summary>
    /// What an adapter can carry, entity by entity and direction by direction — so a
    /// host knows what to offer <i>before</i> offering it, and a user is not shown
    /// "Export to X" for a program that cannot receive plates.
    ///
    /// A declaration with no fixed vocabulary is unfalsifiable, which is why
    /// <see cref="FemexEntity"/> exists: §7.3's capability-honesty test asserts the
    /// declaration matches what the adapter actually produces and consumes, and that
    /// test cannot be written against free-form strings.
    ///
    /// An entity absent from <see cref="Entities"/> is
    /// <see cref="TransferDirection.None"/> — saying nothing about an entity is the
    /// same claim as saying it does not cross, and there is no third state.
    /// </summary>
    public sealed class AdapterCapabilities
    {
        private readonly Dictionary<FemexEntity, TransferDirection> _entities;

        public AdapterCapabilities(IEnumerable<KeyValuePair<FemexEntity, TransferDirection>> entities)
        {
            if (entities is null)
                throw new ArgumentNullException(nameof(entities));

            _entities = new Dictionary<FemexEntity, TransferDirection>();
            foreach (var pair in entities)
            {
                if (pair.Value != TransferDirection.None)
                    _entities[pair.Key] = pair.Value;
            }
        }

        public IReadOnlyDictionary<FemexEntity, TransferDirection> Entities => _entities;

        /// <summary>
        /// Which directions this adapter declares for one entity;
        /// <see cref="TransferDirection.None"/> when it declares nothing.
        /// </summary>
        public TransferDirection For(FemexEntity entity)
        {
            return _entities.TryGetValue(entity, out TransferDirection direction)
                ? direction
                : TransferDirection.None;
        }

        /// <summary>
        /// Whether this adapter claims to carry the entity that way. The
        /// capability-honesty test reads this, and so should a host deciding what to
        /// put in a menu.
        /// </summary>
        public bool Supports(FemexEntity entity, TransferDirection direction)
        {
            if (direction == TransferDirection.None)
                throw new ArgumentException("Ask about a real direction.", nameof(direction));

            return (For(entity) & direction) == direction;
        }

        /// <summary>
        /// The same set declared the other way round, for the ordinary adapter that
        /// carries most things both ways: everything named here is
        /// <see cref="TransferDirection.Both"/> and everything else is
        /// <see cref="TransferDirection.None"/>.
        /// </summary>
        public static AdapterCapabilities Both(params FemexEntity[] entities)
        {
            if (entities is null)
                throw new ArgumentNullException(nameof(entities));

            var pairs = new List<KeyValuePair<FemexEntity, TransferDirection>>(entities.Length);
            foreach (FemexEntity entity in entities)
                pairs.Add(new KeyValuePair<FemexEntity, TransferDirection>(entity, TransferDirection.Both));

            return new AdapterCapabilities(pairs);
        }
    }
}
