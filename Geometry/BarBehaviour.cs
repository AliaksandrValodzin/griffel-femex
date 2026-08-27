namespace griffel_femex.Geometry
{
    /// <summary>
    /// Which actions a bar carries in analysis — SAF's
    /// <c>StructuralCurveMember.Behaviour in analysis</c> set exactly, and one of the
    /// eight concepts <c>FEMEX_SAF_Fit.md</c> §4 records as crossing FEMEX
    /// <i>silently wrong</i>. A brace that should carry axial force only, read as a
    /// full frame member, gives a model that opens, validates, solves, and
    /// distributes moment through members that have no moment connection.
    ///
    /// <b>It is not a corner case.</b> The reference workbook populates the column on
    /// every one of its forty-two members, and thirty-three of them are
    /// <c>Axial force only</c> (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.4). A 1.9
    /// import of that one file would have got thirty-three members wrong.
    ///
    /// Closed, and small enough that the argument barely needs making: these are the
    /// four states every program FEMEX targets offers, and there is no fifth.
    /// <see cref="PlateBehaviour"/> is the sibling concept on surfaces, and
    /// <see cref="PlateBehaviour.CompressionOnly"/> is the one value the two sets
    /// share.
    ///
    /// <b>Null is <see cref="Standard"/></b>, and the two are deliberately not the
    /// same value — a pre-1.10 file states nothing because the concept did not
    /// exist, where a 1.10 file writing <c>Standard</c> is an author saying so. The
    /// same distinction <see cref="BoundaryConditions.RestraintSense"/> draws, and it
    /// is what keeps a file that does not use the feature byte-identical.
    /// </summary>
    public enum BarBehaviour
    {
        /// <summary>
        /// Axial force, shear, bending and torsion — an ordinary frame member. What
        /// every bar written before 1.10 means.
        /// </summary>
        Standard,

        /// <summary>
        /// Axial force only, in both directions: a truss member or a pin-ended
        /// brace. No moment is attracted to it whatever its stiffness.
        /// </summary>
        AxialOnly,

        /// <summary>Axial force in compression only — a strut that goes slack in tension.</summary>
        CompressionOnly,

        /// <summary>Axial force in tension only — a cable or a rod brace that buckles out.</summary>
        TensionOnly,
    }
}
