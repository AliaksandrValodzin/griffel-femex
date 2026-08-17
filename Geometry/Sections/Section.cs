using System.Text.Json;
using System.Text.Json.Serialization;

namespace griffel_femex.Geometry.Sections
{
    /// <summary>
    /// Abstract base for bar cross-sections, stored separately and referenced by id.
    ///
    /// A section is <b>three orthogonal layers</b>, any subset of which it may carry
    /// and at least one of which it must: an optional <see cref="Catalogue"/> naming
    /// it in some program's library, the <c>type</c> discriminator and its
    /// dimensions, and an optional <see cref="Properties"/> stating its resolved
    /// stiffness. They are layers on one section rather than sibling subtypes
    /// because a real IPE300 is all three at once, and a catalogue name the receiver
    /// cannot resolve that carried no numbers would be exactly the loss the numbers
    /// exist to prevent.
    ///
    /// A receiver takes the richest layer it can act on: <b>resolve the catalogue
    /// name; else build the parametric shape; else build a member with the stated
    /// stiffness.</b> So a section is never lost, only degraded — and that is a
    /// property of the JSON as read by an adapter, not forward compatibility for an
    /// older build of this library, which refuses an unrecognised version first.
    ///
    /// Reserved future discriminators (not implemented):
    ///  - "tapered"    — a section whose dimensions vary along the member.
    ///  - "asymmetric" — a singly-symmetric or monosymmetric I.
    ///  - "compound"   — two or more profiles battened or laced into one member.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Rectangle), "rectangle")]
    [JsonDerivedType(typeof(Circle), "circle")]
    [JsonDerivedType(typeof(TSection), "tshape")]
    [JsonDerivedType(typeof(ISection), "ishape")]
    [JsonDerivedType(typeof(Channel), "channel")]
    [JsonDerivedType(typeof(Angle), "angle")]
    [JsonDerivedType(typeof(Box), "box")]
    [JsonDerivedType(typeof(Pipe), "pipe")]
    [JsonDerivedType(typeof(GenericSection), "generic")]
    public abstract class Section : IIdentified, IExtensible
    {
        public int Id { get; set; }

        // Optional round-trip identity. Null means this section has none; see
        // IIdentified.
        public Guid? Uid { get; set; }

        // Robot and ETABS key sections by name, so a blank or repeated one is
        // reported by FemexModel.Validate() as a warning.
        public string? Name { get; set; }

        /// <summary>
        /// What library this section came out of and what it is called there. Null
        /// means it is not a catalogue profile, or that the producing program did
        /// not say. See <see cref="SectionCatalogue"/>.
        /// </summary>
        public SectionCatalogue? Catalogue { get; set; }

        /// <summary>
        /// The section's resolved numbers, authoritative over anything the
        /// dimensions give. Null means it states none, and the shape is all there
        /// is. See <see cref="SectionProperties"/>.
        /// </summary>
        public SectionProperties? Properties { get; set; }

        // Cross-sectional area from this section's dimensions alone, ignoring
        // anything Properties states. GetArea() is what a consumer wants.
        public abstract double CalculateArea();

        /// <summary>
        /// The area to build a member with: the stated one where the section carries
        /// it, the parametric one otherwise. A tabulated area includes root fillets
        /// that no idealisation carries, so where both exist the stated one is the
        /// measured one and wins.
        /// </summary>
        public double GetArea() => Properties?.Area ?? CalculateArea();

        // Members this build does not know; see IExtensible. The "type"
        // discriminator above is not one of them: System.Text.Json consumes it
        // before extension data is populated.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnknownMembers { get; set; }
    }
}
