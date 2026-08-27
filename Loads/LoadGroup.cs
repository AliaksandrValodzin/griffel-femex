using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Loads
{
    /// <summary>
    /// A named set of load cases that a design code treats as one action — SAF's
    /// <c>StructuralLoadGroup</c>, and the object behind the mandatory
    /// <c>StructuralLoadCase.Load group</c> column that FEMEX had nowhere to put.
    ///
    /// <b>Why an entity and not a string.</b> <see cref="LoadCase.Nature"/> already
    /// says what kind of action a case is, and if a group said only that it would be
    /// a duplicate. It says one thing more: <see cref="Relation"/> — whether the
    /// cases in the group may act together, must act together, or exclude one
    /// another. That is a statement about a *set* of cases, so it needs somewhere a
    /// set exists.
    ///
    /// <b>What it does not do.</b> Nothing in this library combines by it: load
    /// combinations remain explicit lists of factored cases
    /// (<see cref="Combinations.LoadCombination"/>), and a group changes no number
    /// FEMEX computes. It is what a receiving program's own combination generator
    /// reads, and what an exporter writes into a column it cannot leave blank.
    ///
    /// A case names its group; a group does not list its cases. That direction is
    /// SAF's and it is the one that cannot go stale — a case belongs to exactly one
    /// group, so there is exactly one place to state it.
    /// </summary>
    public class LoadGroup : IIdentified, IExtensible
    {
        /// <summary>
        /// The group's key in its own id space, referenced by
        /// <see cref="LoadCase.LoadGroupId"/>.
        /// </summary>
        public int Id { get; set; }

        // Optional round-trip identity. Null means this group has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this group was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        /// <summary>
        /// What the group is called. SAF keys load groups by name and a duplicate
        /// within the sheet is fatal there, so <see cref="FemexModel.Validate()"/>
        /// warns about a blank or repeated one — the same treatment sections,
        /// materials and load cases already get.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Which category of action the group collects. Non-nullable with no
        /// initializer, so a group that says nothing is <see cref="LoadGroupType.Permanent"/> —
        /// the value a group of unstated kind is least dangerous read as, since a
        /// permanent action is present in every combination and is the one a
        /// receiver will not quietly drop.
        /// </summary>
        public LoadGroupType Type { get; set; }

        /// <summary>
        /// Whether the cases in this group may act at once. See
        /// <see cref="LoadGroupRelation"/>, which records that the two producers in
        /// the SAF reference corpus disagree about this value for the same kind of
        /// group.
        /// </summary>
        public LoadGroupRelation Relation { get; set; }

        // Parameterless constructor for serialization
        public LoadGroup() { }

        public LoadGroup(int id, string? name, LoadGroupType type,
                         LoadGroupRelation relation = LoadGroupRelation.Standard)
        {
            Id = id;
            Name = name;
            Type = type;
            Relation = relation;
        }

        public override string ToString()
        {
            return $"[{Id}] {Name} ({Type}, {Relation})";
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
