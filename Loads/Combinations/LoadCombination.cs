using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Loads.Combinations
{
    /// <summary>
    /// A factored sum of load cases — the thing a design is actually checked
    /// against. Loads live in a load case; a combination combines load cases, and
    /// nothing else: a <see cref="LoadCombinationTerm"/> names a
    /// <see cref="LoadCase.Number"/>, never another combination, so the structure
    /// is flat and needs no resolution pass.
    ///
    /// Three rules a consumer can otherwise get wrong:
    ///
    /// 1. <b>Terms repeating a load case add.</b> Two terms naming case 1 at 0.9
    ///    and 0.5 mean 1.4 times case 1, as in ETABS.
    ///    <see cref="FemexModel.GetTotalFactor"/> is that rule made executable.
    /// 2. <b>Combinations are enveloped within their own limit state.</b>
    ///    <see cref="IncludeInDesignEnvelope"/> gates participation, and
    ///    <see cref="FemexModel.GetDesignEnvelope"/> implements the rule so that a
    ///    consumer and the format cannot disagree about it. An envelope mixing
    ///    ULS and SLS results would be meaningless, which is why the limit state
    ///    partitions it.
    /// 3. <b>The two members with "envelope" in their meaning are unrelated.</b>
    ///    <see cref="LoadCombinationType.Envelope"/> says how <i>this
    ///    combination's own load-case terms</i> combine.
    ///    <see cref="IncludeInDesignEnvelope"/> says whether this combination
    ///    takes part in the per-limit-state design envelope. A combination can be
    ///    <see cref="LoadCombinationType.LinearAdd"/> and in the envelope, or
    ///    <see cref="LoadCombinationType.Envelope"/> and out of it.
    ///
    /// Combinations are stated explicitly, as factors. There is deliberately no
    /// "generate per code" mode and no standard name: code combinations do not
    /// round-trip as factor lists, so the explicit form is needed regardless, and
    /// a second way to say the same thing is reserved rather than built.
    /// </summary>
    public class LoadCombination : IIdentified, IExtensible
    {
        // Unique identifier, in its own id space — separate from load case
        // numbers. An exporter targeting a program where cases and combinations
        // share one number space remaps.
        public int Number { get; set; }

        // Optional round-trip identity. Null means this combination has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this combination was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        // Optional human-readable name, e.g. "1.2G + 1.5Q". Robot, ETABS and SAF
        // key combinations by name, so a missing or repeated one is reported by
        // Validate().
        public string? Label { get; set; }

        public LimitState LimitState { get; set; }

        // Named LoadCombinationType but exposed as CombinationType: "type" is the
        // polymorphic discriminator key elsewhere in FEMEX, and this entity is not
        // polymorphic.
        public LoadCombinationType CombinationType { get; set; } = LoadCombinationType.LinearAdd;

        // Whether this combination takes part in the design envelope of its limit
        // state. False for one that exists only for reporting or checking.
        public bool IncludeInDesignEnvelope { get; set; } = true;

        // The factored load cases. Repeats are legal and add.
        public List<LoadCombinationTerm> Terms { get; set; } = new List<LoadCombinationTerm>();

        // Parameterless constructor for serialization
        public LoadCombination() { }

        // Convenience constructor
        public LoadCombination(int number, string? label, LimitState limitState)
        {
            Number = number;
            Label = label;
            LimitState = limitState;
        }

        public override string ToString()
        {
            return $"[{Number}] {Label} ({LimitState})";
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
