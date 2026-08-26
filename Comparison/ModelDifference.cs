using griffel_femex.Interop;

namespace griffel_femex.Comparison
{
    /// <summary>How two models fail to be the same model.</summary>
    public enum DifferenceKind
    {
        /// <summary>An object on the left that the right has no object for.</summary>
        OnlyInLeft,

        /// <summary>An object on the right that the left has no object for.</summary>
        OnlyInRight,

        /// <summary>
        /// A matched pair whose runtime types differ — a rectangle that came back a
        /// circle. Reported instead of a member walk, because there are no shared
        /// members to walk.
        /// </summary>
        TypeDiffers,

        /// <summary>A matched pair that disagree about one member.</summary>
        MemberDiffers,

        /// <summary>
        /// Objects carrying no uid, which is not a difference between the models so
        /// much as a statement that they cannot be compared. §7.2: a model whose uid
        /// coverage is partial cannot be round-trip-tested at all, because half its
        /// objects have no matching key. Reported once per entity kind per side, with
        /// a count, rather than once per object.
        /// </summary>
        Unkeyed,
    }

    /// <summary>
    /// One way in which two models are not the same model, under the equivalence
    /// <c>FEMEX_Adapters.md</c> §7.2 defines.
    ///
    /// The point of the type is §7.1: <i>every</i> difference between a model and
    /// its round trip must be covered by a reported <see cref="TransferMessage"/>.
    /// An undeclared difference is a bug; a declared one is the adapter working as
    /// designed. That assertion needs a difference to name the object it is about in
    /// the same vocabulary a message does, which is why
    /// <see cref="Subject"/> is an <see cref="ObjectRef"/> and not prose.
    /// </summary>
    public sealed class ModelDifference
    {
        public ModelDifference(DifferenceKind kind, ObjectRef? subject, string? member,
                               string? left, string? right, string text)
        {
            Kind = kind;
            Subject = subject;
            Member = member;
            Left = left;
            Right = right;
            Text = text;
        }

        public DifferenceKind Kind { get; }

        /// <summary>
        /// What the difference is about, and null when it is about the model itself —
        /// its schema version, its units, its gravity. Those are the same three
        /// model-level facts <see cref="TransferMessage.ModelLoss"/> exists for, and
        /// they line up deliberately: a difference and the message that declares it
        /// have to be anchored the same way to be matched at all.
        /// </summary>
        public ObjectRef? Subject { get; }

        /// <summary>
        /// Which member disagrees, dotted for nested values — <c>Ux.Stiffness</c>,
        /// <c>Catalogue.Profile</c>. Null for a whole-object difference.
        /// </summary>
        public string? Member { get; }

        /// <summary>The left value, rendered. Null where there is no left object.</summary>
        public string? Left { get; }

        /// <summary>The right value, rendered. Null where there is no right object.</summary>
        public string? Right { get; }

        public string Text { get; }

        public override string ToString() => Text;
    }
}
