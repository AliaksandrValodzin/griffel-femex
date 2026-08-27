namespace griffel_femex.Geometry
{
    /// <summary>
    /// A straight one-dimensional member between two nodes.
    ///
    /// <b>Local axes</b>, the ETABS/SAP convention, with
    /// <see cref="RotationAngle"/> zero and the global frame right-handed and Z up:
    ///
    /// <list type="bullet">
    /// <item>local <b>x</b> = the unit vector from <see cref="StartNodeId"/> to
    /// <see cref="EndNodeId"/>.</item>
    /// <item><b>Non-vertical bar:</b> local <b>y</b> = normalize(Ẑ − (Ẑ·x̂) x̂) — the
    /// vertical direction with the axial part removed, so it lies in the vertical
    /// plane through the member and points upward. Local <b>z</b> = x̂ × ŷ, which is
    /// horizontal.</item>
    /// <item><b>Vertical bar</b>, where that construction degenerates: local
    /// <b>y</b> = global <b>+X</b>, and local <b>z</b> = x̂ × ŷ, which is global
    /// <b>+Y</b> for a bar drawn upward.</item>
    /// </list>
    ///
    /// <see cref="RotationAngle"/> then rolls y and z about local x.
    /// <c>FemexModel.TryGetBarLocalAxes</c> is this rule, executable, and is what a
    /// consumer should use rather than reimplementing it.
    ///
    /// <b>That same local y and z is the frame everything 1.10 added is measured
    /// in.</b> <see cref="Alignment"/> names a line of the cross-section in it,
    /// <see cref="Eccentricity"/> offsets from that line in it, and a
    /// <c>TemperatureLoad</c>'s two gradients are stated along it — and it is the
    /// frame a <c>Hinge</c> on this member releases in, so <c>ux</c> is the axial
    /// release and <c>rz</c> the one that pins a beam end. Nothing new is
    /// invented by any of them: the section's own dimensions are already in this
    /// frame, and a member that says nothing about alignment or eccentricity means
    /// what every bar written before 1.10 meant — the centroid, with no offset.
    /// </summary>
    public class Bar : Element
    {
        // References Node.NodeNumber
        public int StartNodeId { get; set; }
        public int EndNodeId { get; set; }

        // References Section.Id
        public int SectionId { get; set; }

        /// <summary>
        /// The section at the end node (references <c>Section.Id</c>), when the
        /// member is tapered. Null — every bar before 1.10 — is prismatic; when set,
        /// the section varies <b>linearly</b> from <see cref="SectionId"/> at the
        /// start node to this one at the end node.
        ///
        /// <see cref="SectionId"/> stays what a receiver that ignores the taper
        /// builds from, which is the degrade-don't-lose rule sections already follow.
        /// The <c>tapered</c> discriminator reserved on <c>Section</c> stays reserved
        /// and unimplemented: a taper is a property of the <i>member</i>, not a kind
        /// of section, and two bars can share one section while only one of them
        /// tapers.
        ///
        /// <b>This downgrades a silent wrong answer; it does not close it.</b> SAF
        /// states a varying member as <i>spans</i>, each with its own section and
        /// alignment, relative spans summing to 1.0 — the reference workbook's one
        /// example has three spans and a section pair on the middle one
        /// (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.5). A single linear taper
        /// carries the one-span case exactly and turns the rest into a reported
        /// approximation: a rafter haunched at <i>both</i> ends still arrives with the
        /// wrong moment distribution, now with a message attached. That is worth
        /// having and it is not the same as being right.
        /// </summary>
        public int? EndSectionId { get; set; }

        // References Material.Id
        public int MaterialId { get; set; }

        // Roll of the local y and z axes about local x, in degrees, right-hand
        // rule about local +x. Local x itself is fixed by the two nodes and no
        // angle can change it.
        public double RotationAngle { get; set; }

        /// <summary>
        /// Which actions the member carries in analysis. Null means
        /// <see cref="BarBehaviour.Standard"/>, which is exactly what a pre-1.10 file
        /// means; nullable rather than defaulted so no existing file gains
        /// <c>"behaviour": "Standard"</c> on every bar.
        /// </summary>
        public BarBehaviour? Behaviour { get; set; }

        /// <summary>
        /// Which line of the cross-section the two nodes lie on. Null means
        /// <see cref="BarAlignment.Centre"/>, for the reason
        /// <see cref="Behaviour"/> gives.
        /// </summary>
        public BarAlignment? Alignment { get; set; }

        /// <summary>
        /// Offsets from the system line at each end, structural and analysis kept
        /// apart. Null means none. See <see cref="BarEccentricity"/> for why the two
        /// families are not one.
        /// </summary>
        public BarEccentricity? Eccentricity { get; set; }

        // Parameterless constructor for serialization
        public Bar() { }

        // Convenience constructor
        public Bar(int id, int startNodeId, int endNodeId, int sectionId, int materialId, double rotationAngle = 0.0)
        {
            Id = id;
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            SectionId = sectionId;
            MaterialId = materialId;
            RotationAngle = rotationAngle;
        }

        public override IEnumerable<int> GetNodeIds()
        {
            return new[] { StartNodeId, EndNodeId };
        }
    }
}
