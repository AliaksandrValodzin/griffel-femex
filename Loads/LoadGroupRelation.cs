namespace griffel_femex.Loads
{
    /// <summary>
    /// How the cases in one <see cref="LoadGroup"/> act with respect to each other —
    /// SAF's <c>StructuralLoadGroup.Relation</c>, and <b>the part
    /// <see cref="LoadNature"/> cannot express</b>. It is the whole reason a group is
    /// an entity rather than a string on the case: a nature says what a load is, and
    /// this says which of several loads of that kind may be present at once.
    ///
    /// Three values, taken from the format rather than invented, and confirmed in
    /// the reference corpus. Note what that corpus also shows: its two producers
    /// <b>disagree</b> about which relation a wind or snow group takes — the HOUSE
    /// file writes <see cref="Standard"/> where the HALL file writes
    /// <see cref="Exclusive"/> (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.9). So an
    /// exporter that has to invent this value is guessing something two real
    /// programs already answer differently, which is why the invention is reported
    /// rather than assumed.
    /// </summary>
    public enum LoadGroupRelation
    {
        /// <summary>
        /// No constraint: every case in the group may act at the same time as every
        /// other. What a group with nothing to say about its members means.
        /// </summary>
        Standard,

        /// <summary>
        /// At most one case in the group acts in any one combination — wind from one
        /// direction at a time.
        /// </summary>
        Exclusive,

        /// <summary>
        /// All or none: the cases in the group act as a set, or none of them does.
        /// </summary>
        Together,
    }
}
