namespace griffel_femex.Loads
{
    /// <summary>
    /// What kind of action a <see cref="LoadGroup"/> collects — SAF's
    /// <c>StructuralLoadGroup.Load group type</c> set exactly, confirmed against the
    /// reference workbook, which uses all five
    /// (<c>Claude/FEMEX_SAF_Corpus_Notes.md</c> §3.9).
    ///
    /// Closed, where <see cref="LoadGroup.Name"/> beside it is free text, and the
    /// line is the one <see cref="Materials.MaterialType"/> draws against a grade:
    /// what a group is *called* is a project's business and unbounded, whereas the
    /// set of categories a design code combines by is small and has been the same
    /// five for as long as anyone has written one.
    ///
    /// <b><see cref="Tensioning"/> has no <see cref="LoadNature"/> equivalent</b>,
    /// and that asymmetry is the reason the two fields can disagree at all.
    /// <c>LoadNature</c> is FEMEX's own seven-value category, stated on the case;
    /// this is the category the group states. Every other value here has a nature
    /// that corresponds to it — <c>Dead → Permanent</c>,
    /// <c>Live | Wind | Snow | Temperature → Variable</c>,
    /// <c>Accidental → Accidental</c>, <c>Seismic → Seismic</c> — and
    /// <see cref="FemexModel.Validate()"/> warns when a case and its group say
    /// different things, because combination factors are exactly what that changes.
    /// A prestressing group is the one case where the disagreement is structural
    /// rather than an author's slip, and it is warned about in its own words.
    /// </summary>
    public enum LoadGroupType
    {
        /// <summary>Always present, always the same: self weight, finishes, services.</summary>
        Permanent,

        /// <summary>Imposed, wind, snow, thermal — present sometimes, at a factored value.</summary>
        Variable,

        /// <summary>Impact, explosion, fire — the accidental design situation.</summary>
        Accidental,

        /// <summary>Earthquake, which most codes combine under rules of its own.</summary>
        Seismic,

        /// <summary>
        /// Prestress and post-tensioning. FEMEX's <see cref="LoadNature"/> has no
        /// member for it, so a case in a group of this type states a nature that
        /// cannot agree with it; see the type-level remarks.
        /// </summary>
        Tensioning,
    }
}
