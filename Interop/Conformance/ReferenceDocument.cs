namespace griffel_femex.Interop.Conformance
{
    /// <summary>
    /// The "native model" of the reference adapter: a deliberately impoverished
    /// structural format, invented for this repository, that exists so §7.5's
    /// conformance harness has something to run against.
    ///
    /// Every one of its limitations is chosen to force one category of §4 into the
    /// open, and each is noted where it bites:
    /// <list type="bullet">
    /// <item>no regions on a panel — <see cref="LossCategory.Dropped"/>;</item>
    /// <item>supports are fixed or free, with no stiffness and no sense —
    /// <see cref="LossCategory.Approximated"/>;</item>
    /// <item>a section is a name and an area, with no shape —
    /// <see cref="LossCategory.Approximated"/>;</item>
    /// <item>a mandatory unit system and gravity, which FEMEX need not state —
    /// <see cref="LossCategory.Invented"/>;</item>
    /// <item>a per-member stiffness modifier FEMEX has no noun for —
    /// <see cref="LossCategory.Unmapped"/>;</item>
    /// <item>no storeys at all, so an import must synthesise every
    /// <c>Level</c> — <see cref="LossCategory.Invented"/> again, through
    /// <c>GeometrySynthesis</c>.</item>
    /// </list>
    ///
    /// It carries a <c>Uid</c> on every object, as SAF carries an <c>Id</c> column,
    /// which is what gives a round trip the full uid coverage §7.2 requires. A
    /// format without one could not be round-trip-tested at all, and a reference
    /// adapter that could not be round-trip-tested would prove nothing.
    /// </summary>
    public sealed class ReferenceDocument
    {
        /// <summary>Its own format version, unrelated to FEMEX's.</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Mandatory, and this is the point of it: FEMEX's five typed unit enums are
        /// not one flag, and a model may state none of them. An exporter has to write
        /// something here, which is the textbook <see cref="LossCategory.Invented"/>
        /// — everything worked, a value was produced, and nobody said it was a guess.
        /// </summary>
        public string UnitSystem { get; set; } = string.Empty;

        /// <summary>
        /// Mandatory. FEMEX's own default is metre-specific and a millimetre model
        /// that accepts it is 1000x light, so an adapter that copies it without
        /// saying so has produced a confidently wrong answer.
        /// </summary>
        public double GravityAcceleration { get; set; }

        public List<ReferenceMaterial> Materials { get; set; } = new List<ReferenceMaterial>();

        public List<ReferenceSection> Sections { get; set; } = new List<ReferenceSection>();

        public List<ReferenceNode> Nodes { get; set; } = new List<ReferenceNode>();

        public List<ReferenceMember> Members { get; set; } = new List<ReferenceMember>();

        public List<ReferencePanel> Panels { get; set; } = new List<ReferencePanel>();

        public List<ReferenceSupport> Supports { get; set; } = new List<ReferenceSupport>();
    }

    /// <summary>A named material with the three numbers this format has room for.</summary>
    public sealed class ReferenceMaterial
    {
        public Guid Uid { get; set; }

        public string Name { get; set; } = string.Empty;

        public double ModulusOfElasticity { get; set; }

        public double PoissonsRatio { get; set; }

        public double Density { get; set; }
    }

    /// <summary>
    /// A section with an area and no shape. Everything FEMEX knows about the profile
    /// — the discriminator, the dimensions, the catalogue entry, the second moments —
    /// has nowhere to go, which is what makes a section
    /// <see cref="LossCategory.Approximated"/> rather than lost outright: a receiver
    /// can still build a member with the stated stiffness.
    /// </summary>
    public sealed class ReferenceSection
    {
        public Guid Uid { get; set; }

        public string Name { get; set; } = string.Empty;

        public double Area { get; set; }
    }

    /// <summary>A point in space. No storey, which is why an import synthesises levels.</summary>
    public sealed class ReferenceNode
    {
        public Guid Uid { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }

    /// <summary>A line member between two nodes.</summary>
    public sealed class ReferenceMember
    {
        public Guid Uid { get; set; }

        public Guid StartNode { get; set; }

        public Guid EndNode { get; set; }

        public Guid Section { get; set; }

        public Guid Material { get; set; }

        public double Rotation { get; set; }

        /// <summary>
        /// The concept FEMEX has no noun for. Review §5.4 lists stiffness modifiers
        /// as unmapped, and this is that inventory made concrete so that
        /// <see cref="LossCategory.Unmapped"/> has something to be about.
        /// </summary>
        public double? StiffnessModifier { get; set; }
    }

    /// <summary>
    /// A panel of one thickness. It has no notion of a region, so a drop panel and a
    /// void are both simply absent — the canonical
    /// <see cref="LossCategory.Dropped"/>, and the case where FEMEX's priority rule
    /// is better than what it is crossing into and therefore has nowhere to land.
    /// </summary>
    public sealed class ReferencePanel
    {
        public Guid Uid { get; set; }

        public List<Guid> Nodes { get; set; } = new List<Guid>();

        public double Thickness { get; set; }

        public Guid? Material { get; set; }
    }

    /// <summary>
    /// Six booleans. No stiffness, so a spring arrives fixed or free; no sense, so a
    /// compression-only bearing arrives resisting uplift as well.
    /// </summary>
    public sealed class ReferenceSupport
    {
        public Guid Uid { get; set; }

        public List<Guid> Nodes { get; set; } = new List<Guid>();

        public bool Ux { get; set; }

        public bool Uy { get; set; }

        public bool Uz { get; set; }

        public bool Rx { get; set; }

        public bool Ry { get; set; }

        public bool Rz { get; set; }
    }
}
