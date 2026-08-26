namespace griffel_femex.Interop
{
    /// <summary>
    /// What a <see cref="TransferMessage"/> is about. Free-text messages are the
    /// default outcome and the wrong one: a transfer report's audience includes a
    /// UI that must highlight the object and a test that must assert coverage.
    ///
    /// <b>Both <see cref="Id"/> and <see cref="Uid"/>, not one.</b> The integer id
    /// is what the file's own references use — it is what makes a message
    /// actionable against the model in hand. The uid is what survives the crossing,
    /// and is what §7.2's round-trip equivalence matches on. Either alone leaves
    /// one of the two consumers unable to use the message.
    /// </summary>
    public readonly struct ObjectRef : IEquatable<ObjectRef>
    {
        public ObjectRef(FemexEntity entity, int? id = null, Guid? uid = null)
        {
            Entity = entity;
            Id = id;
            Uid = uid;
        }

        public FemexEntity Entity { get; }

        /// <summary>
        /// The key this file's own references use — <c>Bar.Id</c>,
        /// <c>Node.NodeNumber</c>, <c>LoadCase.Number</c>. Null where the message is
        /// about the entity kind rather than about one object, which is the shape
        /// <see cref="LossCategory.Unmapped"/> asks for.
        /// </summary>
        public int? Id { get; }

        /// <summary>
        /// The key that survives the crossing. Null is a real answer: a
        /// hand-authored object has no round-trip identity, and §7.2 says so
        /// plainly — a model whose uid coverage is partial cannot be
        /// round-trip-tested at all.
        /// </summary>
        public Guid? Uid { get; }

        public bool Equals(ObjectRef other)
        {
            return Entity == other.Entity && Id == other.Id && Uid == other.Uid;
        }

        public override bool Equals(object? obj)
        {
            return obj is ObjectRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Entity;
                hash = (hash * 397) ^ Id.GetHashCode();
                hash = (hash * 397) ^ Uid.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            if (Id.HasValue)
                return $"{Entity} {Id.Value}";

            return Uid.HasValue ? $"{Entity} {Uid.Value}" : Entity.ToString();
        }
    }
}
