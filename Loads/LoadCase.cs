using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Loads
{
    /// <summary>
    /// A distinct group of loads that act together under one environmental or usage
    /// condition, referenced by <see cref="Load.LoadCaseNumber"/> and factored by
    /// <see cref="Combinations.LoadCombination"/>.
    ///
    /// A case is more than its entries in the model's load list: it also says, in
    /// <see cref="SelfWeightFactor"/>, how much of the structure's own weight acts
    /// in it. A case with no loads of its own is therefore not an empty case.
    /// </summary>
    public class LoadCase : IIdentified, IExtensible
    {
        public int Number { get; set; }

        // Optional round-trip identity. Null means this case has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Optional provenance: what this case was derived from. See IIdentified.
        public Guid? ParentUid { get; set; }

        // Robot and ETABS key load cases by name, so a blank or repeated one is
        // reported by FemexModel.Validate() as a warning.
        public string? Label { get; set; }

        public LoadNature Nature { get; set; }

        /// <summary>
        /// The <see cref="LoadGroup"/> this case belongs to (references
        /// <c>LoadGroup.Id</c>), new in 1.9. Null — the state of every case in every
        /// file written before it — means the case names no group, and an exporter
        /// writing SAF, which marks the column mandatory, has to invent one.
        ///
        /// The group is not a second spelling of <see cref="Nature"/>. It carries
        /// <see cref="LoadGroupRelation"/>, which is a statement about a <i>set</i> of
        /// cases and which a nature cannot express. What the two <i>do</i> overlap on
        /// is the category — <see cref="LoadGroupType"/> against
        /// <see cref="LoadNature"/> — and because they can therefore disagree,
        /// <see cref="FemexModel.Validate()"/> checks them against a stated
        /// compatibility map and warns when they say different things. Combination
        /// factors are exactly what that disagreement changes, so it is not cosmetic.
        /// </summary>
        public int? LoadGroupId { get; set; }

        /// <summary>
        /// How much of the structure's own weight acts in this case. <c>1.0</c> is
        /// normal gravity along the model's <see cref="FemexModel.Gravity"/> vector;
        /// <c>0</c> — the default — is none. Dimensionless, and multiplied into a
        /// weight the model computes for itself from each material's density and
        /// each element's section or thickness.
        ///
        /// Written on every load case in every file, never omitted:
        /// <c>"selfWeightFactor": 0</c> is the positive statement "no self-weight
        /// here", and a case that simply left it out would be silent about the one
        /// thing this field exists to settle.
        ///
        /// FEMEX reserves no self-weight case and gives <see cref="LoadNature.Dead"/>
        /// no special meaning: any case may carry the factor, and more than one doing
        /// so is legal but warned about, since a combination naming two of them
        /// counts the weight twice.
        ///
        /// A case's own loads are <b>additional to</b> its self-weight, never a
        /// substitute for it. An author-computed area load standing in for the slab's
        /// weight in a case that also carries a factor is applied on top of it.
        /// </summary>
        public double SelfWeightFactor { get; set; }

        public LoadCase(int number, string? label, LoadNature nature)
        {
            Number = number;
            Label = label;
            Nature = nature;
        }

        public LoadCase(int number, string? label, LoadNature nature, double selfWeightFactor)
            : this(number, label, nature)
        {
            SelfWeightFactor = selfWeightFactor;
        }

        // Default constructor for serialization or simple instantiation
        public LoadCase() { }

        public override string ToString()
        {
            return $"[{Number}] {Label} ({Nature})";
        }

        // Members this build does not know; see IExtensible.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
